# NexaPay – Arkitekturgenomgång

> **Datum:** 2026-05-09  
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
- **Pipeline-ordningen är rätt** – Logging → Validation → ConcurrencyRetry → Audit. Att Audit ligger sist innebär att bara kommandon som passerat validering auditeras.
- **ConcurrencyRetryBehavior** – `catch (ConcurrencyException) when (attempt++ < MaxRetries)` är en elegant pattern. UnitOfWork rensar ChangeTracker innan det kastar, så nästa försök läser färsk data.
- **UnitOfWork samlar events FÖRE save** – om `SaveChangesAsync` kastar finns inga events att dispatcha; events dispatchar EFTER lyckad save. Korrekt ordning.
- **Result<T> med IResult-interface** – AuditBehavior kan kontrollera `IsSuccess` typesäkert utan reflektion på response-typen.
- **ISensitiveRequest** – `LoginCommand` maskeras i LoggingBehavior; lösenord syns aldrig i loggar.

### Säkerhet
- **Lockout kontrolleras FÖRE lösenordsvalidering** i `AuthService.LoginAsync` – undviker timing oracle.
- **Token denylist i OnTokenValidated** – revokering kontrolleras vid varje request, inte bara vid middleware-ingången.
- **CORS nekar allt som default** – `SetIsOriginAllowed(_ => false)` om inga origins är konfigurerade. Tvingar explicit konfiguration i produktion.
- **JWT-nyckel valideras vid uppstart** – kastar `InvalidOperationException` om nyckeln är < 32 bytes.
- **Personnummer aldrig lagrat** – `CreateCardHandler` lagrar bara `CardToken` (Guid) + `Last4Digits`; full PAN returneras bara en gång i svaret och lagras inte.
- **Idempotency-nyckel med filtrerat unikt index** – `WHERE IdempotencyKey IS NOT NULL` förhindrar dubbletter i databasen.
- **Rate limiting på auth och finansiella endpoints** – 5/min respektive 20/min per IP.
- **Lösenordskrav och kontolåsning** – 8 tecken, versaler, siffror, specialtecken; låses vid 5 misslyckanden i 15 minuter.

### Datakvalitet
- **Global query filter döljer stängda konton** – `HasQueryFilter(a => a.Status != AccountStatus.Closed)` i `ApplicationDbContext`; staff använder `IgnoreQueryFilters()` för full synlighet.
- **RowVersion på Account** – optimistisk concurrenshantering direkt i domänmodellen.
- **TransferValidator kontrollerar FromAccountId != ToAccountId** – förhindrar överföring till sig själv på valideringsnivå.
- **TransactionPolicy centraliserar gränsvärden** – max belopp och max beskrivningslängd på ett ställe.
- **AsNoTracking på alla read-only queries** – `GetAllAccountsAsync`, `GetAccountsByOwnerIdAsync`, `GetCardsByAccountIdAsync`, `GetByAccountNumberAsync` etc.

### Testning
- **RateLimitingIntegrationTests** använder `[SetUp]`/`[TearDown]` – ger varje test en färsk factory och klient, isolerar rate limit-buckets.
- **RegisterHandlerTests** testar alla 4 personalroller med extern e-post (parametriserat med `[TestCase]`).
- **Integration via NexaPayWebApplicationFactory** – InMemory-databas + rollseeding, testar hela HTTP-stacken inklusive middleware.

---

## Problem att åtgärda

### HÖG prioritet

| # | Fil | Problem |
|---|-----|---------|
| ~~H1~~ | ~~`NexaPay.Infrastructure/Identity/RedisTokenDenylist.cs`~~ | ~~Åtgärdat 2026-05-09~~ – `Revoke()` och `IsRevoked()` omsluts nu av try-catch på `RedisException`. `Revoke` loggar varning och returnerar utan att kasta (logout ger 200 OK, token löper ut naturligt). `IsRevoked` fail-openar och returnerar `false` med en varning (autentiserade requests blockeras inte av Redis-avbrott). |
| H2 | `NexaPay.API/Controllers/AdminController.cs` | AdminController saknar `[EnableRateLimiting]`. Endpoint `POST /api/admin/users` har inga hastighetsbegränsningar – en angripare kan skapa obegränsat antal användare utan att bromsas. Lägg till `[EnableRateLimiting("auth")]`. |

### MEDEL prioritet

| # | Fil | Problem |
|---|-----|---------|
| ~~M1~~ | ~~`CreateAccountHandler.cs`~~ | ~~Åtgärdat 2026-05-09~~ – `Handle()` provar upp till 5 kontonummer och kontrollerar unikhet via `AccountNumberExistsAsync()` vid varje försök. Returnerar `Result.Failure` om alla 5 kolliderar (extremt osannolikt). |
| ~~M2~~ | ~~`CardRepository.cs`~~ | ~~Åtgärdat 2026-05-09~~ – `GetByCardTokenAsync()` använder nu `.AsNoTracking()` i linje med övriga read-only metoder i repot. |
| ~~M3~~ | ~~`CreateCardHandler.cs`~~ | ~~Åtgärdat 2026-05-09~~ – `GeneratePan()` genererar nu 15 slumpmässiga siffror och beräknar korrekt Luhn-kontrollsiffra via `ComputeLuhnCheckDigit()`. Resulterande 16-siffrigt PAN passerar Luhn-validering. |
| ~~M4~~ | ~~`TransferHandler.cs`~~ | ~~Åtgärdat 2026-05-09~~ – Explicit valutakontroll (`fromAccount.Balance.Currency != toAccount.Balance.Currency`) returnerar ett tydligt felmeddelande innan `Money`-operatorer anropas. |
| ~~M5~~ | ~~`EventHandlers/`~~ | ~~Åtgärdat 2026-05-09~~ – `INotificationService`-interface tillagt i Application. `LoggingNotificationService` (Infrastructure) är placeholder-implementation registrerad via DI – byt mot riktig e-post/SMS-provider utan att ändra Application-lagret. Alla 5 handlers injicerar och anropar servicen. `CardBlockedHandler` slår upp kontot via `IUnitOfWork` för att hämta `OwnerId` (saknas i `CardBlocked`-eventet). |

### LÅG prioritet

| # | Fil | Problem |
|---|-----|---------|
| L1 | `NexaPay.Infrastructure/Identity/JwtService.cs:86–113` | Tokengenereringen använder det äldre `JwtSecurityToken` + `JwtSecurityTokenHandler`. Valideringssidan i `DependencyInjection.cs` använder redan modernare `JsonWebTokenHandler`. Inkonsekvent – bör harmoniseras till `JsonWebTokenHandler` på båda sidor. |
| L2 | `NexaPay.Application/Features/Transactions/Queries/GetTransactionsByAccount/GetTransactionsByAccountHandler.cs:40–41` | `GetByIdAsync` laddar `Account` med change tracking enbart för att kontrollera ägarskap. Byt till en `AccountOwnedByAsync(accountId, userId)` (bool) eller lägg till AsNoTracking. |
| L3 | `NexaPay.Infrastructure/Identity/JwtService.cs:102–103` | Default på 24 timmar är hårdkodat. Logga en varning om `Jwt:ExpiryHours` saknas i konfigurationen. |

---

## Saknade funktioner

| # | Beskrivning | Varför det saknas är ett problem |
|---|-------------|----------------------------------|
| F1 | **Freeze/Unfreeze API-endpoints** – `Account.Freeze()` och `Account.Unfreeze()` finns i domänen men det finns inga Commands, Handlers eller controllers för dessa operationer. | Staff kan inte frysa misstänkta konton via API. Domänen stöder det men det är helt oaccessibelt utifrån. |
| F2 | **Persistent audit-tabell** – `AuditBehavior` skriver bara till `ILogger`. Loggar kan roteras bort och är inte sökbara via API. | Compliance kräver ofta en revisionsspårning som är persistent och querybar – inte bara lograder. |
| F3 | **Notifieringssystem** – inga e-post/SMS vid transaktioner eller kortblockeringar. | Domain events dispatchar till handlers som bara loggar. Kunderna informeras inte om aktivitet på sina konton. |

---

## Konfiguration (sätts vid driftsättning, inte i kod)

| # | Problem |
|---|---------|
| C1 | `ConnectionStrings:Redis` saknas i prod – sätt i miljövariabler/secrets. Varning loggas automatiskt om strängen saknas. OBS: se H1 ovan – lägg till felhantering i RedisTokenDenylist innan prod. |
| C2 | `AllowedHosts` i `appsettings.json` – sätt till faktisk domän när API:et driftsätts. |
| C3 | `MigrateAsync` vid uppstart loggar en varning i Production och kör sedan – bör ersättas av ett separat `dotnet ef database update`-steg i deploy-pipelinen vid horisontell skalning. |
