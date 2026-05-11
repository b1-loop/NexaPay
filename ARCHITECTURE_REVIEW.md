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

## Säkerhetsgranskning

> Fullständig fil-för-fil genomgång utförd 2026-05-11. Varje fynd verifierat mot källkoden.

### Fynd och status

| # | Fil | Allvarlighet | Status |
|---|-----|-------------|--------|
| S1 | `DependencyInjection.cs` | MEDIUM | **Åtgärdat** – `ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }` tillagt i `TokenValidationParameters`. |
| S2 | `AuthService.cs:71` | LÅG | **Accepterat** – `EmailConfirmed = true` kräver fungerande e-posttjänst. Dokumenterat som krav inför produktion. |
| S3 | `AuthService.cs` | LÅG | **Accepterat** – Lösenordsåterställning kräver e-posttjänst. Dokumenterat som krav inför produktion. |
| S4 | `StaffEmailPolicy.cs:27` | LÅG | **Åtgärdat** – Validering att `StaffDomain` är icke-tom och innehåller en punkt tillagd. |
| S5 | `TransactionsController.cs:80–82` | LÅG | **Åtgärdat** – `[Range(1, int.MaxValue)]` på `page` och `[Range(1, 100)]` på `pageSize` tillagt. |
| S6 | `CreateCardHandler.cs` | LÅG | **Åtgärdat** – `CardToken` genereras med `RandomNumberGenerator.GetBytes(16)` (128-bit explicit RNG). |
| S7 | `ServiceExtensions.cs` | LÅG | **Åtgärdat** – Säkerhetsheaders + `UseHsts()` i produktion tillagda. |
| S8 | `ValidationBehavior.cs` | LÅG | **Åtgärdat** – Kommandovalidationsfel loggas nu till `AuditLogs` innan `ValidationException` kastas. |

### Verifierade false positives (ej problem)

| Påstått problem | Varför det inte stämmer |
|---|---|
| Vanlig User kan frysa eget konto | `AccountsController` har `[Authorize(Roles = Roles.CanWriteAccounts)]` = `Admin,BankManager,Teller` – User-rollen blockeras av controllern. |
| `Money`-operator kan returnera negativt belopp | `Money`-konstruktorn kastar `ArgumentOutOfRangeException` om `amount < 0`. Aritmetiken är skyddad på domännivå. |
| CVV skickas i svar (PCI-problem) | CVV genereras och returneras en gång vid kortutfärdande och lagras aldrig. Standardbeteende vid kortutfärdande. |

---

## Kvarvarande problem

### HÖG prioritet

| # | Fil | Problem |
|---|-----|---------|
| H2 | `NexaPay.API/Controllers/AdminController.cs` | AdminController saknar `[EnableRateLimiting]`. Endpoint `POST /api/admin/users` har inga hastighetsbegränsningar – en angripare kan skapa obegränsat antal användare utan att bromsas. **Medvetet utelämnat i detta projekt** för att underlätta testning. I produktion skulle `[EnableRateLimiting("auth")]` läggas till för att skydda mot massregistrering. |

### LÅG prioritet

*Inga kvarvarande låg-prioritetsproblem.*

---

## Saknade funktioner

| # | Beskrivning | Status |
|---|-------------|--------|
| F3 | **Notifieringssystem** – `INotificationService`-interface och `LoggingNotificationService` är på plats. Alla 5 domain event handlers anropar servicen. Byt `LoggingNotificationService` mot en riktig e-post/SMS-implementation i `DependencyInjection.cs`. | Infrastruktur klar – provider-implementation återstår |

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
| L1 | `JwtService` – migrerad från `JwtSecurityTokenHandler` till `JsonWebTokenHandler` (modern, konsekvent med valideringssidan). |
| L2 | `IAccountRepository` utökad med `AccountExistsAsync` och `AccountOwnedByAsync`; `GetTransactionsByAccountHandler` använder nu bool-queries istället för full entity load. |
| L3 | `JwtService` – loggar nu varning om `Jwt:ExpiryHours` saknas i konfigurationen och faller tillbaka på 24 h. |
| F1 | `FreezeAccountCommand/Handler/Validator` + `UnfreezeAccountCommand/Handler/Validator` skapade; `AccountsController` har `PUT /accounts/{id}/freeze` och `PUT /accounts/{id}/unfreeze` med `[Authorize(Roles = Roles.CanWriteAccounts)]`. |
| F2 | `IAuditService` + `EfAuditService` skapad; `AuditLog`-entitet och `AuditLogs`-tabell tillagda; `AuditBehavior` skriver nu till persistant DB och `ILogger` parallellt; EF-migration `AddAuditLog` skapad. |
| W1–W4 | Fyra EF Core modellvalideringsvarningar åtgärdade: `HasQueryFilter` tillagd på `CardConfiguration` (matchar Account-filtret); `Transaction.Account`-navigationen markerad som optional (bevarar transaktionshistorik för stängda konton); `HasDefaultValue(Currency.SEK)` borttagen från alla `Money`-konfigurationer (Currency sätts alltid explicit i kod). EF-migration `FixEfCoreWarnings` skapad. |
| S1 | `DependencyInjection.cs` – `ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }` tillagt i `TokenValidationParameters`; förhindrar algoritmbytes-attacker. |
| S4 | `StaffEmailPolicy` – validerar nu att `StaffDomain` är icke-tom och innehåller en punkt innan domänkontrollen körs. |
| S5 | `TransactionsController` – `[Range(1, int.MaxValue)]` på `page` och `[Range(1, 100)]` på `pageSize`; tydliga 400-svar vid ogiltiga värden. |
| S6 | `CreateCardHandler` – `CardToken` genereras nu med `RandomNumberGenerator.GetBytes(16)` (128-bit explicit entropi) istället för `Guid.NewGuid()`. |
| S7 | `ServiceExtensions.cs` – säkerhetsheaders tillagda i middleware: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `X-Permitted-Cross-Domain-Policies`; `UseHsts()` aktiveras i icke-dev-miljöer. |
| S8 | `ValidationBehavior` – `IAuditService` injiceras nu; kommandovalidationsfel loggas till `AuditLogs`-tabellen innan `ValidationException` kastas, vilket ger komplett revisionsspår. |
| – | Fullständig `README.md` skapad med arkitektur, endpoints, flödesdiagram och driftsättningsinstruktioner. |
