# NexaPay – Arkitekturgenomgång

> **Senast uppdaterad:** 2026-05-09  
> **Branch:** master  
> **Stack:** .NET 8 · ASP.NET Core · EF Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT

---

## Projektöversikt

```
NexaPay.sln
├── NexaPay.Domain          – Entiteter, value objects, interfaces, events. Inga externa NuGet-beroenden
├── NexaPay.Application     – Handlers, validators, DTOs, pipeline behaviors, policies
├── NexaPay.Infrastructure  – EF Core, repositories, Identity, JWT, Redis
├── NexaPay.API             – Controllers, Contracts/, middleware, Swagger, Program.cs
└── NexaPay.Tests           – 159 tester (enhet + integration)
```

---

## Styrkor

### Arkitektur och design
- **Clean Architecture korrekt implementerat** – beroenden pekar bara inåt; Domain har noll externa NuGet-beroenden.
- **Pipeline-ordningen är rätt** – Logging → Validation → ConcurrencyRetry → Audit. Audit sist innebär att bara kommandon som passerat validering auditeras.
- **ConcurrencyRetryBehavior** – `catch (ConcurrencyException) when (attempt++ < MaxRetries)` är en elegant pattern. UnitOfWork rensar ChangeTracker innan det kastar, så nästa försök läser färsk data.
- **UnitOfWork samlar events FÖRE save** – om `SaveChangesAsync` kastar finns inga events att dispatcha; events dispatchar EFTER lyckad save. Korrekt ordning.
- **Result<T> med IResult-interface** – AuditBehavior kan kontrollera `IsSuccess` typesäkert utan reflektion på response-typen.
- **ISensitiveRequest** – `LoginCommand` maskeras i LoggingBehavior; lösenord syns aldrig i loggar.
- **INotificationService** – Interface i Application, `LoggingNotificationService` i Infrastructure som placeholder. Alla 5 domain event handlers anropar servicen. Byt till riktig e-post/SMS-provider med en enda DI-ändring.

### Säkerhet
- **Lockout kontrolleras FÖRE lösenordsvalidering** i `AuthService.LoginAsync` – undviker timing oracle.
- **Token denylist i OnTokenValidated** – revokering kontrolleras vid varje request, inte bara vid middleware-ingången.
- **RedisTokenDenylist fail-open** – vid Redis-avbrott loggar `IsRevoked` en varning och returnerar `false`; autentiserade requests blockeras inte av infrastrukturproblem.
- **CORS nekar allt som default** – `SetIsOriginAllowed(_ => false)` om inga origins är konfigurerade. Tvingar explicit konfiguration i produktion.
- **JWT-nyckel valideras vid uppstart** – kastar `InvalidOperationException` om nyckeln är < 32 bytes.
- **Kortnummer aldrig lagrat** – `CreateCardHandler` lagrar bara `CardToken` (Guid) + `Last4Digits`; full PAN returneras bara en gång i svaret, lagras inte, och är nu Luhn-giltigt.
- **Idempotency-nyckel med filtrerat unikt index** – `WHERE IdempotencyKey IS NOT NULL` förhindrar dubbletter i databasen.
- **Rate limiting på auth och finansiella endpoints** – 5/min respektive 20/min per IP.
- **Lösenordskrav och kontolåsning** – 8 tecken, versaler, siffror, specialtecken; låses vid 5 misslyckanden i 15 minuter.

### Datakvalitet
- **Global query filter döljer stängda konton** – `HasQueryFilter(a => a.Status != AccountStatus.Closed)` i `ApplicationDbContext`; staff använder `IgnoreQueryFilters()` för full synlighet.
- **RowVersion på Account** – optimistisk concurrenshantering direkt i domänmodellen.
- **TransferValidator kontrollerar FromAccountId != ToAccountId** – förhindrar överföring till sig själv på valideringsnivå.
- **Valutavalidering i TransferHandler** – explicit kontroll att båda konton har samma valuta med tydligt felmeddelande innan `Money`-operatorerna anropas.
- **TransactionPolicy centraliserar gränsvärden** – max belopp och max beskrivningslängd på ett ställe.
- **AsNoTracking på alla read-only queries** – `GetAllAccountsAsync`, `GetAccountsByOwnerIdAsync`, `GetCardsByAccountIdAsync`, `GetByAccountNumberAsync`, `GetByCardTokenAsync` m.fl.
- **AccountNumber kollisionsskydd** – `CreateAccountHandler` provar upp till 5 unika nummer via `AccountNumberExistsAsync()` innan save.

### Testning
- **RateLimitingIntegrationTests** använder `[SetUp]`/`[TearDown]` – ger varje test en färsk factory och klient, isolerar rate limit-buckets.
- **RegisterHandlerTests** testar alla 4 personalroller med extern e-post (parametriserat med `[TestCase]`).
- **Integration via NexaPayWebApplicationFactory** – InMemory-databas + rollseeding, testar hela HTTP-stacken inklusive middleware.

---

## Kvarvarande problem

### HÖG prioritet

| # | Fil | Problem |
|---|-----|---------|
| H2 | `NexaPay.API/Controllers/AdminController.cs` | AdminController saknar `[EnableRateLimiting]`. Endpoint `POST /api/admin/users` har inga hastighetsbegränsningar – en angripare kan skapa obegränsat antal användare utan att bromsas. Lägg till `[EnableRateLimiting("auth")]`. |

### LÅG prioritet

| # | Fil | Problem |
|---|-----|---------|
| L1 | `NexaPay.Infrastructure/Identity/JwtService.cs:86–113` | Tokengenereringen använder det äldre `JwtSecurityToken` + `JwtSecurityTokenHandler`. Valideringssidan i `DependencyInjection.cs` använder redan modernare `JsonWebTokenHandler`. Inkonsekvent – bör harmoniseras till `JsonWebTokenHandler` på båda sidor. |
| L2 | `NexaPay.Application/Features/Transactions/Queries/GetTransactionsByAccount/GetTransactionsByAccountHandler.cs:40–41` | `GetByIdAsync` laddar `Account` med change tracking enbart för att kontrollera ägarskap. Byt till en `AccountOwnedByAsync(accountId, userId)` (bool) eller lägg till AsNoTracking. |
| L3 | `NexaPay.Infrastructure/Identity/JwtService.cs:102–103` | Default på 24 timmar är hårdkodat. Logga en varning om `Jwt:ExpiryHours` saknas i konfigurationen. |

---

## Saknade funktioner

| # | Beskrivning | Status |
|---|-------------|--------|
| F1 | **Freeze/Unfreeze API-endpoints** – `Account.Freeze()` och `Account.Unfreeze()` finns i domänen men det finns inga Commands, Handlers eller controllers för dessa operationer. Staff kan inte frysa misstänkta konton via API. | Öppen |
| F2 | **Persistent audit-tabell** – `AuditBehavior` skriver bara till `ILogger`. Loggar kan roteras bort och är inte sökbara via API. Compliance kräver ofta en revisionsspårning som är persistent och querybar. | Öppen |
| F3 | **Notifieringssystem** – `INotificationService`-interface och `LoggingNotificationService` är på plats. Alla 5 domain event handlers anropar servicen. Byt `LoggingNotificationService` mot en riktig e-post/SMS-implementation i `DependencyInjection.cs`. | Infrastruktur klar – implementation återstår |

---

## Konfiguration (sätts vid driftsättning, inte i kod)

| # | Problem |
|---|---------|
| C1 | `ConnectionStrings:Redis` saknas i prod – sätt i miljövariabler/secrets. Varning loggas automatiskt om strängen saknas. Redis-avbrott hanteras nu gracefully (fail-open). |
| C2 | `AllowedHosts` i `appsettings.json` – sätt till faktisk domän när API:et driftsätts. |
| C3 | `MigrateAsync` vid uppstart loggar en varning i Production och kör sedan – bör ersättas av ett separat `dotnet ef database update`-steg i deploy-pipelinen vid horisontell skalning. |

---

## Åtgärdat i denna session

| # | Åtgärd |
|---|--------|
| H1 | `RedisTokenDenylist.Revoke/IsRevoked` – try-catch på `RedisException`; fail-open på `IsRevoked` så Redis-avbrott inte blockerar alla autentiserade requests. |
| M1 | `CreateAccountHandler` – retry-loop med `AccountNumberExistsAsync()`, upp till 5 försök innan `Result.Failure`. |
| M2 | `CardRepository.GetByCardTokenAsync` – lade till `.AsNoTracking()`. |
| M3 | `CreateCardHandler.GeneratePan` – implementerade Luhn-kontrollsiffra via `ComputeLuhnCheckDigit()`; genererade PANs är nu Luhn-giltiga. |
| M4 | `TransferHandler` – explicit valutakontroll med tydligt felmeddelande innan `Money`-operatorerna anropas. |
| M5 | `INotificationService` tillagt i Application; `LoggingNotificationService` i Infrastructure; alla 5 event handlers anropar servicen. `CardBlockedHandler` slår upp kontot via `IUnitOfWork` för att hämta `OwnerId`. |
| – | Fullständig `README.md` skapad med arkitektur, endpoints, flödesdiagram och driftsättningsinstruktioner. |
