# NexaPay – Arkitekturgenomgång

> **Senast uppdaterad:** 2026-05-11  
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
└── NexaPay.Tests           – 160 tester (enhet + integration)
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
- **SmtpNotificationService** – Interface i Application, Gmail SMTP-implementation i Infrastructure. Alla 5 domain event handlers anropar servicen. `UserManager` slår upp e-postadress från `ownerId`. Byt provider med en enda DI-ändring.
- **E-postbekräftelse och lösenordsåterställning** – `EmailConfirmed = false` vid registrering; bekräftelsemail skickas via SMTP; login blockeras tills bekräftelse skett. Forgot/reset-password-flöde skyddar mot e-postuppräkning (alltid samma svar oavsett om adressen finns).

### Säkerhet
- **Lockout kontrolleras FÖRE lösenordsvalidering** i `AuthService.LoginAsync` – undviker timing oracle.
- **Token denylist i OnTokenValidated** – revokering kontrolleras vid varje request, inte bara vid middleware-ingången.
- **RedisTokenDenylist fail-open** – vid Redis-avbrott loggar `IsRevoked` en varning och returnerar `false`; autentiserade requests blockeras inte av infrastrukturproblem.
- **CORS nekar allt som default** – `SetIsOriginAllowed(_ => false)` om inga origins är konfigurerade. Tvingar explicit konfiguration i produktion.
- **JWT-nyckel valideras vid uppstart** – kastar `InvalidOperationException` om nyckeln är < 32 bytes.
- **JWT ValidAlgorithms = HS256** – förhindrar algoritmbytes-attacker explicit.
- **Säkerhetsheaders på alla svar** – `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`; HSTS aktiveras i produktion.
- **Kortnummer aldrig lagrat** – `CreateCardHandler` lagrar bara `CardToken` (128-bit RNG hex) + `Last4Digits`; full PAN returneras bara en gång i svaret och är Luhn-giltigt.
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
- **Persistent audit-tabell** – `AuditBehavior` skriver varje kommando (inkl. validationsfel) till `AuditLogs`-tabellen i databasen.

### Testning
- **RateLimitingIntegrationTests** använder `[SetUp]`/`[TearDown]` – ger varje test en färsk factory och klient, isolerar rate limit-buckets.
- **RegisterHandlerTests** testar alla 4 personalroller med extern e-post (parametriserat med `[TestCase]`).
- **Integration via NexaPayWebApplicationFactory** – InMemory-databas + rollseeding, testar hela HTTP-stacken inklusive middleware.

---

## Säkerhetsgranskning

> Fullständig fil-för-fil genomgång utförd 2026-05-11. Varje fynd verifierat mot källkoden.

### Verifierade false positives (ej problem)

| Påstått problem | Varför det inte stämmer |
|---|---|
| Vanlig User kan frysa eget konto | `AccountsController` har `[Authorize(Roles = Roles.CanWriteAccounts)]` = `Admin,BankManager,Teller` – User-rollen blockeras av controllern. |
| `Money`-operator kan returnera negativt belopp | `Money`-konstruktorn kastar `ArgumentOutOfRangeException` om `amount < 0`. Aritmetiken är skyddad på domännivå. |
| CVV skickas i svar (PCI-problem) | CVV genereras och returneras en gång vid kortutfärdande och lagras aldrig. Standardbeteende vid kortutfärdande. |

---

## Kvarvarande problem

| # | Fil | Problem |
|---|-----|---------|
| H2 | `NexaPay.API/Controllers/AdminController.cs` | AdminController saknar `[EnableRateLimiting]`. **Medvetet utelämnat i detta projekt** för att underlätta testning. I produktion skulle `[EnableRateLimiting("auth")]` läggas till för att skydda mot massregistrering. |
| F4 | `SmtpNotificationService.cs` | Bekräftelse- och återställningsmailet exponerar råa tokens och API-endpoints i brödtexten. Kräver frontend-integration. När frontend finns: byt ut brödtexten mot en klickbar länk, t.ex. `https://nexapay.com/confirm-email?userId=X&token=Y`. Frontenden anropar sedan `POST /auth/confirm-email` i bakgrunden – användaren ser bara en knapp. Samma princip för `forgot-password`. |
| F5 | – | Ingen `GET /api/users/me`-endpoint. Behövs inte förrän frontend byggs – om extra profildata utöver JWT-claims (email, roll, userId) behöver visas ska denna endpoint läggas till då. |

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
| M5 | `INotificationService` tillagt i Application; alla 5 event handlers anropar servicen. `CardBlockedHandler` slår upp kontot via `IUnitOfWork` för att hämta `OwnerId`. |
| L1 | `JwtService` – migrerad från `JwtSecurityTokenHandler` till `JsonWebTokenHandler` (modern, konsekvent med valideringssidan). |
| L2 | `IAccountRepository` utökad med `AccountExistsAsync` och `AccountOwnedByAsync`; `GetTransactionsByAccountHandler` använder nu bool-queries istället för full entity load. |
| L3 | `JwtService` – loggar nu varning om `Jwt:ExpiryHours` saknas i konfigurationen och faller tillbaka på 24 h. |
| F1 | `FreezeAccountCommand/Handler/Validator` + `UnfreezeAccountCommand/Handler/Validator` skapade; `AccountsController` har `PUT /accounts/{id}/freeze` och `PUT /accounts/{id}/unfreeze` med `[Authorize(Roles = Roles.CanWriteAccounts)]`. |
| F2 | `IAuditService` + `EfAuditService` skapad; `AuditLog`-entitet och `AuditLogs`-tabell tillagda; `AuditBehavior` skriver till DB och `ILogger` parallellt; EF-migration `AddAuditLog` skapad. |
| F3 | `SmtpNotificationService` implementerad – skickar riktiga mail via Gmail SMTP. `UserManager<IdentityUser>` slår upp e-postadress från `ownerId`. Graceful fallback om SMTP ej konfigurerat. `appsettings.Development.json` gitignorerad så credentials aldrig pushas. |
| S2 | `AuthService.RegisterAsync` – `EmailConfirmed = false` vid registrering; bekräftelsetoken genereras och skickas via `SmtpNotificationService`; login blockeras tills e-posten bekräftats. `POST /auth/confirm-email` bekräftar kontot. |
| S3 | Lösenordsåterställningsflöde implementerat – `POST /auth/forgot-password` genererar reset-token och skickar mail (avslöjar aldrig om e-posten finns); `POST /auth/reset-password` sätter nytt lösenord. |
| W1–W4 | Fyra EF Core modellvalideringsvarningar åtgärdade: `HasQueryFilter` tillagd på `CardConfiguration`; `Transaction.Account`-navigationen markerad som optional; `HasDefaultValue(Currency.SEK)` borttagen från alla `Money`-konfigurationer. EF-migration `FixEfCoreWarnings` skapad. |
| S1 | `DependencyInjection.cs` – `ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }` tillagt; förhindrar algoritmbytes-attacker. |
| S4 | `StaffEmailPolicy` – validerar nu att `StaffDomain` är icke-tom och innehåller en punkt. |
| S5 | `TransactionsController` – `[Range(1, int.MaxValue)]` på `page` och `[Range(1, 100)]` på `pageSize`. |
| S6 | `CreateCardHandler` – `CardToken` genereras med `RandomNumberGenerator.GetBytes(16)` (128-bit explicit entropi). |
| S7 | `ServiceExtensions.cs` – säkerhetsheaders + `UseHsts()` i produktion tillagda. |
| S8 | `ValidationBehavior` – `IAuditService` injiceras; kommandovalidationsfel loggas till `AuditLogs` innan `ValidationException` kastas. |
| – | Fullständig `README.md` och `CODEBASE_GUIDE.md` skapade. |
| – | `Card.Unblock()` tillagd i domänen; `UnblockCardCommand/Handler/Validator` skapade; `PUT /cards/{id}/unblock` tillagd i `CardsController` (kräver Admin/BankManager). |
| – | `IAuthService.ChangePasswordAsync` tillagd; `POST /auth/change-password` kräver inloggning och tar `{ currentPassword, newPassword }`. |
| A1 | `Account.cs` – `AccountName`, `AccountType`, `OwnerId` ändrade till `private set`; domäninvarianterna skyddas nu korrekt. |
| A2 | `SmtpNotificationService.cs` – `MailMessage` kasseras nu med `using var`. |
| A3 | Validators skapade för alla fyra Query-objekt: `GetAccountByIdValidator`, `GetAllAccountsValidator`, `GetCardsByAccountValidator`, `GetTransactionsByAccountValidator`. |
