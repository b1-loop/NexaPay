# NexaPay – Komplett kodbasguide

> Förklarar varje mapp, fil och flöde steg för steg.  
> Målgrupp: alla som vill förstå hur systemet är uppbyggt och hur en request rör sig genom alla lager.

---

## Innehållsförteckning

1. [Lösningsstruktur – de 5 projekten](#1-lösningsstruktur)
2. [NexaPay.Domain – affärslogikens kärna](#2-nexapaydomain)
3. [NexaPay.Application – use cases och pipeline](#3-nexapayapplication)
4. [NexaPay.Infrastructure – databas och externa tjänster](#4-nexapayinfrastructure)
5. [NexaPay.API – HTTP-lagret](#5-nexapayapi)
6. [NexaPay.Tests – testprojektet](#6-nexapaytests)
7. [Steg-för-steg-flöden](#7-steg-för-steg-flöden)

---

## 1. Lösningsstruktur

```
NexaPay.sln
├── NexaPay.Domain          ← Entiteter, value objects, events, interfaces
├── NexaPay.Application     ← Handlers, validators, behaviors, DTOs
├── NexaPay.Infrastructure  ← EF Core, repositories, Identity, JWT, Redis
├── NexaPay.API             ← Controllers, middleware, Program.cs
└── NexaPay.Tests           ← 159 unit- och integrationstester
```

**Beroenderegel (Clean Architecture):**  
Pilarna pekar alltid inåt. Domain känner inte till något annat projekt.  
Application känner till Domain. Infrastructure och API känner till Application och Domain.

```
API  →  Application  →  Domain
Infrastructure  →  Application  →  Domain
```

---

## 2. NexaPay.Domain

Det innersta lagret. Innehåller inga externa NuGet-beroenden – bara ren C#.

```
NexaPay.Domain/
├── Entities/
│   ├── BaseEntity.cs
│   ├── Account.cs
│   ├── Card.cs
│   └── Transaction.cs
├── ValueObjects/
│   └── Money.cs
├── Enums/
│   ├── AccountStatus.cs
│   ├── AccountType.cs
│   ├── CardStatus.cs
│   ├── Currency.cs
│   └── TransactionType.cs
├── Events/
│   ├── IDomainEvent.cs
│   ├── AccountClosed.cs
│   ├── CardBlocked.cs
│   ├── MoneyDeposited.cs
│   ├── MoneyTransferred.cs
│   └── MoneyWithdrawn.cs
├── Interfaces/
│   ├── IAccountRepository.cs
│   ├── ICardRepository.cs
│   ├── ITransactionRepository.cs
│   └── IUnitOfWork.cs
├── Exceptions/
│   └── ConcurrencyException.cs
└── Policy/
    └── TransactionPolicy.cs
```

---

### Entities/BaseEntity.cs

Basklass som alla entiteter ärver från. Innehåller:
- `Id` (Guid) – unikt primärnyckel
- `CreatedAt` (DateTime) – när posten skapades
- `UpdatedAt` (DateTime?) – senast ändrad
- `DomainEvents` (lista) – samlar domänhändelser som ska dispatcha efter save
- `AddDomainEvent()` / `ClearDomainEvents()` – lägger till och rensar events

---

### Entities/Account.cs

Rot-aggregat för kontot. All kontoaffärslogik sitter här.

**Egenskaper:**
| Egenskap | Typ | Beskrivning |
|---|---|---|
| `AccountNumber` | string | Unikt 10-siffrigt nummer |
| `AccountName` | string | Kontots visningsnamn |
| `Balance` | Money | Saldo som value object (belopp + valuta) |
| `AccountType` | AccountType | Checking / Savings |
| `Status` | AccountStatus | Open / Frozen / Closed |
| `OwnerId` | string | Identity-användarens ID |
| `RowVersion` | byte[] | Optimistisk concurrens-token |
| `Transactions` | List | Alla transaktioner kopplade till kontot |
| `Cards` | List | Alla kort kopplade till kontot |

**Fabriksmetod:**
```
Account.Open(accountNumber, accountName, accountType, ownerId, currency)
```
Skapar ett nytt konto med status Open och nollsaldo. Anropas alltid via fabriken, aldrig via `new Account()` direkt.

**Domänmetoder – affärsregler lever här:**

| Metod | Vad den gör |
|---|---|
| `Deposit(amount)` | Kontrollerar att kontot är Open, adderar beloppet, skapar Transaction-post, lägger till `MoneyDeposited`-event |
| `Withdraw(amount)` | Kontrollerar Open + tillräckligt saldo, subtraherar, skapar Transaction-post, lägger till `MoneyWithdrawn`-event |
| `TransferTo(receiver, amount)` | Anropar `Withdraw()` på this och `Deposit()` på receiver, skapar en `Transfer`-transaktion, lägger till `MoneyTransferred`-event |
| `Freeze()` | Kräver Open-status, sätter Status = Frozen |
| `Unfreeze()` | Kräver Frozen-status, sätter Status = Open |
| `Close()` | Kräver nollsaldo, sätter Status = Closed, lägger till `AccountClosed`-event |

---

### Entities/Card.cs

Representerar ett bankkort. PAN (kortnumret) lagras aldrig – bara ett opakt token och sista 4 siffror.

**Egenskaper:**
| Egenskap | Typ | Beskrivning |
|---|---|---|
| `CardToken` | string | 32-tecken hex-token (ersätter PAN i databasen) |
| `Last4Digits` | string | Visas i UI: "\*\*\*\* \*\*\*\* \*\*\*\* 1234" |
| `CardHolderName` | string | Kortinnehavarens namn (versaler) |
| `ExpiryDate` | DateOnly | Utgångsdatum (3 år från skapandet) |
| `Status` | CardStatus | Inactive / Active / Blocked |
| `AccountId` | Guid | FK till Account |

**Domänmetod:**
- `Block()` – sätter Status = Blocked, lägger till `CardBlocked`-event

---

### Entities/Transaction.cs

Oföränderlig post i transaktionsregistret. Skapas av Account-metoderna, aldrig direkt.

**Egenskaper:**
| Egenskap | Typ | Beskrivning |
|---|---|---|
| `Amount` | Money | Transaktionens belopp |
| `Type` | TransactionType | Deposit / Withdrawal / Transfer / InvoicePayment |
| `Description` | string | Fritext-beskrivning |
| `BalanceAfterTransaction` | Money | Kontosaldot direkt efter transaktionen |
| `AccountId` | Guid | FK till kontot |
| `ReceiverAccountId` | Guid? | FK till mottagarkonto (vid Transfer) |
| `IdempotencyKey` | Guid? | Förhindrar dubbla transaktioner |

---

### ValueObjects/Money.cs

Sealed klass som representerar ett penningvärde. Kan aldrig vara negativt.

**Regler:**
- Konstruktorn kastar `ArgumentOutOfRangeException` om `amount < 0`
- Alla aritmetikoperatorer (`+`, `-`, `>`, `<`) kräver samma valuta via `EnforceSameCurrency()`
- Belopp avrundas alltid till 2 decimaler vid skapandet
- `Money.Zero(currency)` skapar ett nollobjekt

---

### Events/

Domänhändelser publiceras av entiteterna och dispatchar av UnitOfWork **efter** lyckad databassparning.

| Event | Utlösare | Payload |
|---|---|---|
| `MoneyDeposited` | `Account.Deposit()` | AccountId, Amount, OwnerId |
| `MoneyWithdrawn` | `Account.Withdraw()` | AccountId, Amount, OwnerId |
| `MoneyTransferred` | `Account.TransferTo()` | FromAccountId, ToAccountId, Amount, OwnerId |
| `AccountClosed` | `Account.Close()` | AccountId, OwnerId |
| `CardBlocked` | `Card.Block()` | CardId, AccountId |

`IDomainEvent` ärver från MediatRs `INotification` – det gör att event handlers är vanliga `INotificationHandler<T>`.

---

### Interfaces/IUnitOfWork.cs

Definierar kontraktet för UnitOfWork. Infrastructure-lagret implementerar det.

```
IUnitOfWork
├── Accounts    → IAccountRepository
├── Cards       → ICardRepository
├── Transactions→ ITransactionRepository
└── SaveChangesAsync() → sparar till DB + dispatchar domain events
```

---

### Interfaces/IAccountRepository.cs

Kontraktet för kontorepositoryt. Viktiga metoder:

| Metod | Beskrivning |
|---|---|
| `GetByIdAsync(id)` | Hämtar konto med navigationsegenskaper |
| `GetAllAsync()` | Alla konton (staff) |
| `GetByOwnerIdAsync(ownerId)` | Konton för en specifik användare |
| `AccountExistsAsync(id)` | Bool-koll om konto finns (lightweight) |
| `AccountOwnedByAsync(id, ownerId)` | Bool-koll om användaren äger kontot (lightweight) |
| `AccountNumberExistsAsync(number)` | Kollisionskontroll vid kontonummergenerering |

---

### Policy/TransactionPolicy.cs

Centraliserar affärsgränser för transaktioner.

```csharp
MaxTransactionAmount = 1_000_000m   // Max belopp per transaktion
MaxDescriptionLength = 500          // Max tecken i beskrivning
```

Används av FluentValidation-validators för att hålla gränser på ett ställe.

---

## 3. NexaPay.Application

Orchestrerar use cases via MediatR CQRS-mönstret. Känner bara till Domain – aldrig Infrastructure.

```
NexaPay.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs
│   │   ├── ValidationBehavior.cs
│   │   ├── ConcurrencyRetryBehavior.cs
│   │   └── AuditBehavior.cs
│   ├── Constants/
│   │   └── Roles.cs
│   ├── Exceptions/
│   │   └── ValidationException.cs
│   ├── Interfaces/
│   │   ├── IAppSettings.cs
│   │   ├── IAuditService.cs
│   │   ├── IAuthService.cs
│   │   ├── IJwtService.cs
│   │   ├── INotificationService.cs
│   │   └── ITokenDenylist.cs
│   ├── Models/
│   │   ├── Result.cs
│   │   ├── Result{T}.cs
│   │   └── PagedResult.cs
│   └── Policies/
│       └── StaffEmailPolicy.cs
├── DTOs/
│   ├── AccountDto.cs
│   ├── AuthDto.cs
│   ├── CardDto.cs
│   ├── CreateCardResponse.cs
│   └── TransactionDto.cs
├── Features/
│   ├── Accounts/
│   │   ├── Commands/
│   │   │   ├── CreateAccount/
│   │   │   ├── DeleteAccount/
│   │   │   ├── FreezeAccount/
│   │   │   └── UnfreezeAccount/
│   │   └── Queries/
│   │       ├── GetAccountById/
│   │       └── GetAllAccounts/
│   ├── Auth/
│   │   └── Commands/
│   │       ├── Login/
│   │       └── Register/
│   ├── Cards/
│   │   └── Commands/
│   │       ├── BlockCard/
│   │       └── CreateCard/
│   └── Transactions/
│       ├── Commands/
│       │   ├── Deposit/
│       │   ├── Transfer/
│       │   └── Withdraw/
│       └── Queries/
│           └── GetTransactionsByAccount/
├── Mappings/
│   └── MappingProfile.cs
└── DependencyInjection.cs
```

---

### MediatR-pipeline – ordningen är kritisk

Varje request passerar dessa 4 lager i ordning:

```
Request
  ↓
[1] LoggingBehavior      ← loggar att request kom in
  ↓
[2] ValidationBehavior   ← kör FluentValidation, loggar fel till AuditLogs
  ↓
[3] ConcurrencyRetryBehavior ← fångar DbUpdateConcurrencyException, försöker igen
  ↓
[4] AuditBehavior        ← loggar lyckat/misslyckat kommando till AuditLogs
  ↓
Handler                  ← affärslogiken körs
  ↓
Response
```

---

### Common/Behaviors/LoggingBehavior.cs

**Vad den gör:** Loggar requestens namn när den kommer in och hur lång tid den tog.

**Speciell regel:** Om requesten implementerar `ISensitiveRequest` (t.ex. `LoginCommand`) loggas inga detaljer – bara namnet. Lösenord syns aldrig i loggar.

---

### Common/Behaviors/ValidationBehavior.cs

**Vad den gör:** Hämtar alla registrerade `IValidator<TRequest>` från DI och kör dem parallellt.

**Om validering misslyckas:**
1. Loggar felet till `AuditLogs`-tabellen (via `IAuditService`) för kommandon
2. Kastar `ValidationException` – handleren körs aldrig
3. `ExceptionMiddleware` i API-lagret fångar undantaget och returnerar HTTP 400

---

### Common/Behaviors/ConcurrencyRetryBehavior.cs

**Vad den gör:** Fångar `DbUpdateConcurrencyException` (uppstår när två requests ändrar samma Account-rad samtidigt tack vare `RowVersion`).

**Flöde vid konflikt:**
1. Fångar undantaget
2. Rensar EF:s ChangeTracker via `UnitOfWork` (viktigt – annars försöker nästa attempt spara gammal data)
3. Försöker igen upp till `MaxRetries = 2` gånger
4. Om alla försök misslyckas – kastar `ConcurrencyException`

---

### Common/Behaviors/AuditBehavior.cs

**Vad den gör:** Loggar varje kommando (ej queries) till `ILogger` och `AuditLogs`-tabellen.

**Loggar:**
- Kommandots namn
- UserId (hämtas via reflektion från request-objektet)
- Om det lyckades (`result.IsSuccess`)
- Tidsstämpel

Queries auditeras inte – de ändrar inget tillstånd.

---

### Common/Models/Result.cs och Result{T}.cs

Alla handlers returnerar `Result<T>` istället för att kasta undantag. Möjliga tillstånd:

| Tillstånd | HTTP-kod | Skapad av |
|---|---|---|
| `Result.Success(value)` | 200/201 | Lyckad operation |
| `Result.Failure("msg")` | 400 | Affärsregel bruten |
| `Result.NotFound("msg")` | 404 | Entitet hittades inte |

`AuditBehavior` kan kontrollera `result.IsSuccess` utan reflektion eftersom `Result` implementerar `IResult`-interfacet.

---

### Common/Models/PagedResult{T}.cs

Wrapper för paginerade listor. Innehåller:
- `Items` – entiteterna för aktuell sida
- `TotalCount` – totalt antal poster i databasen
- `Page`, `PageSize`, `TotalPages`
- `HasNextPage`, `HasPreviousPage`

---

### Features/ – CQRS-strukturen

Varje feature-mapp följer samma mönster:

```
FeatureName/
├── FeatureNameCommand.cs   ← dataklass med request-parametrar (record)
├── FeatureNameHandler.cs   ← affärslogiken, implementerar IRequestHandler
└── FeatureNameValidator.cs ← FluentValidation-regler
```

**Queries** returnerar data utan att ändra tillstånd.  
**Commands** ändrar tillstånd och auditeras.

---

### Features/Accounts/Commands/CreateAccount/

**CreateAccountCommand:** `AccountName`, `AccountType`, `Currency`, `OwnerId`

**CreateAccountHandler – flöde:**
1. Försöker generera ett unikt kontonummer (upp till 5 försök via `AccountNumberExistsAsync`)
2. Anropar `Account.Open(...)` fabriksmetoden
3. Sparar via `UnitOfWork.SaveChangesAsync()`
4. Mappar till `AccountDto` och returnerar `Result.Success`

---

### Features/Transactions/Commands/Deposit, Withdraw, Transfer/

Alla tre följer samma grundmönster:

1. **Idempotens-koll** – om `IdempotencyKey` skickats med, kontrollera att den inte redan finns i databasen
2. **Hämta konto** – via `GetByIdAsync`
3. **Ägarskaps-koll** – staff kan agera på alla konton, vanlig user bara sina egna
4. **Anropa domänmetod** – `account.Deposit()`, `account.Withdraw()`, `account.TransferTo()`
5. **Spara** – `UnitOfWork.SaveChangesAsync()` sparar transaktionen och dispatchar domain events
6. **Returnera** – `Result.Success(transactionDto)`

**Transfer** hämtar dessutom mottagarkontot och kontrollerar att valutorna matchar.

---

### Features/Auth/Commands/

**LoginCommand → LoginHandler:**
1. Delegerar till `IAuthService.LoginAsync(email, password)`
2. AuthService (i Infrastructure) sköter Identity-logiken
3. Returnerar `AuthDto` med JWT-token

**RegisterCommand → RegisterHandler:**
1. Kontrollerar via `StaffEmailPolicy` att personalroller bara ges till `@nexapay.com`-adresser
2. Delegerar till `IAuthService.RegisterAsync(email, password, role)`

---

### Mappings/MappingProfile.cs

AutoMapper-profil som definierar alla mappningar:
- `Account → AccountDto`
- `Card → CardDto`
- `Transaction → TransactionDto`

Används av handlers för att konvertera domänobjekt till DTO:er som skickas till klienten.

---

## 4. NexaPay.Infrastructure

Implementerar alla interfaces från Application och Domain. Känner till externa bibliotek (EF Core, Redis, Identity).

```
NexaPay.Infrastructure/
├── Identity/
│   ├── AuthService.cs
│   ├── JwtService.cs
│   ├── InMemoryTokenDenylist.cs
│   └── RedisTokenDenylist.cs
├── Migrations/
│   └── (EF Core-migrationer)
├── Notifications/
│   └── LoggingNotificationService.cs
├── Persistence/
│   ├── ApplicationDbContext.cs
│   ├── AuditLog.cs
│   ├── EfAuditService.cs
│   ├── UnitOfWork.cs
│   └── Repositories/
│       ├── AccountRepository.cs
│       ├── CardRepository.cs
│       └── TransactionRepository.cs
│   └── Configurations/
│       ├── AccountConfiguration.cs
│       ├── CardConfiguration.cs
│       └── TransactionConfiguration.cs
├── Settings/
│   └── AppSettings.cs
└── DependencyInjection.cs
```

---

### Persistence/ApplicationDbContext.cs

Ärver från `IdentityDbContext` – ger automatiskt alla ASP.NET Identity-tabeller (`AspNetUsers`, `AspNetRoles` osv.).

**Egna DbSets:**
- `Accounts`, `Cards`, `Transactions`, `AuditLogs`

**Global query filter:**
```csharp
modelBuilder.Entity<Account>()
    .HasQueryFilter(a => a.Status != AccountStatus.Closed);
```
Alla queries mot `Accounts` filtrerar automatiskt bort stängda konton. Staff anropar `IgnoreQueryFilters()` för full synlighet.

---

### Persistence/UnitOfWork.cs

Samlar alla repositories under ett tak och hanterar domain event dispatching.

**SaveChangesAsync – det viktigaste steget:**
1. Samlar alla domain events från alla entiteter i ChangeTracker **innan** save
2. Anropar `DbContext.SaveChangesAsync()` – sparar till databasen
3. Om save lyckades: dispatchar alla events via MediatR `IPublisher`
4. Events dispatchar **efter** save – om save misslyckas dispatchar inga events

---

### Persistence/Repositories/AccountRepository.cs

Implementerar `IAccountRepository`. Alla read-only-metoder använder `.AsNoTracking()` för bättre prestanda.

**Viktiga metoder:**
- `GetByIdAsync` – hämtar med `.Include(a => a.Transactions)` och `.Include(a => a.Cards)`
- `AccountExistsAsync` – `AnyAsync(a => a.Id == id)` (läser inte hela entiteten)
- `AccountOwnedByAsync` – `AnyAsync(a => a.Id == id && a.OwnerId == ownerId)` (ägarskaps-koll utan entity load)
- `AccountNumberExistsAsync` – kollisionskontroll vid kontonummergenerering

---

### Persistence/EfAuditService.cs

Implementerar `IAuditService`. Injectar `ApplicationDbContext` direkt (inte via UnitOfWork) för att undvika att audit-posten påverkas av concurrency-mekanismer i UnitOfWork.

**LogAsync:** Skapar en ny `AuditLog`-post och anropar `SaveChangesAsync()`.

---

### Persistence/AuditLog.cs

Enkel entitet som lagrar revisionsspår:
- `Command` – vilket kommando kördes
- `UserId` – vem körde det
- `IsSuccess` – lyckades det?
- `Timestamp` – när

---

### Configurations/AccountConfiguration.cs

EF Core Fluent API-konfiguration för `Account`-tabellen:
- `Balance` är ett owned entity (Money) – lagras som två kolumner: `Balance` (decimal) och `AccountCurrency` (int/enum)
- `RowVersion` konfigureras som concurrency token
- `Transactions`-relationen: `OnDelete(Restrict)` – transaktioner kan aldrig raderas av misstag
- `Cards`-relationen: `OnDelete(Cascade)` – kort tas bort när kontot tas bort

---

### Identity/AuthService.cs

Implementerar `IAuthService`. Använder ASP.NET Identity (`UserManager`, `RoleManager`).

**RegisterAsync:**
1. Kontrollerar att e-posten inte redan används
2. Validerar att rollen är giltig (Admin, BankManager, Teller, Auditor, User)
3. Skapar `IdentityUser` och hashar lösenordet via `CreateAsync`
4. Skapar rollen om den inte finns
5. Kopplar rollen till användaren
6. Genererar JWT-token via `IJwtService`

**LoginAsync:**
1. Hämtar användaren via e-post
2. Kontrollerar lockout **innan** lösenordsverifiering (förhindrar timing oracle)
3. Verifierar lösenordet
4. Vid misslyckat: ökar `AccessFailedCount` (låser vid 5 försök i 15 min)
5. Vid lyckat: återställer räknaren, genererar JWT-token

---

### Identity/JwtService.cs

Implementerar `IJwtService`. Genererar JWT-tokens via `JsonWebTokenHandler` (modern implementation).

**GenerateToken:**
1. Läser `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryHours` från konfigurationen
2. Loggar varning och faller tillbaka på 24 h om `ExpiryHours` saknas
3. Skapar claims: `sub` (userId), `email`, `role`, `jti` (unikt token-ID för denylist)
4. Signerar med `HmacSha256`
5. Returnerar `TokenResult` med token-strängen och `ExpiresAt`

---

### Identity/RedisTokenDenylist.cs

Implementerar `ITokenDenylist` med Redis som backend. Stöder horisontell skalning.

**Revoke(jti, expiry):**
- Skriver `jti → 1` med TTL = tid kvar till token-expiry
- Om Redis är nere: loggar varning, kastar inte undantag (logout returnerar ändå 200 OK)

**IsRevoked(jti):**
- Kontrollerar om nyckeln finns i Redis
- Om Redis är nere: fail-open – returnerar `false` med varning (blockerar inte alla autentiserade requests)

---

### Identity/InMemoryTokenDenylist.cs

Fallback om Redis inte är konfigurerat. Lagrar revokerade JTI:s i en `ConcurrentDictionary`.
- En bakgrundstimer rensar ut utgångna tokens var 5:e minut
- Implementerar `IDisposable` – timern stoppas korrekt vid shutdown
- Stöder **inte** horisontell skalning – välj Redis i produktion

---

### Notifications/LoggingNotificationService.cs

Placeholder-implementation av `INotificationService`. Loggar notifieringar istället för att skicka riktiga mail/SMS. Byt mot en riktig implementation (t.ex. SMTP) i `DependencyInjection.cs` utan att ändra något annat.

---

### DependencyInjection.cs

Registrerar alla Infrastructure-tjänster i DI-containern:

| Tjänst | Livslängd | Implementering |
|---|---|---|
| `ApplicationDbContext` | Scoped | EF Core SQL Server |
| `IUnitOfWork` | Scoped | `UnitOfWork` |
| `IJwtService` | Scoped | `JwtService` |
| `IAuditService` | Scoped | `EfAuditService` |
| `INotificationService` | Scoped | `LoggingNotificationService` |
| `IAuthService` | Scoped | `AuthService` |
| `ITokenDenylist` | Singleton | `RedisTokenDenylist` eller `InMemoryTokenDenylist` |

**JWT-validering konfigureras här:**
- Validerar issuer, audience, lifetime, signatur
- `ValidAlgorithms = [HmacSha256]` – förhindrar algoritmbytes-attacker
- `ClockSkew = Zero` – strikt tidvalidering
- `OnTokenValidated`-event kontrollerar denylist vid varje request

---

## 5. NexaPay.API

HTTP-lagret. Tar emot requests, delegerar till MediatR, returnerar svar.

```
NexaPay.API/
├── Controllers/
│   ├── AuthController.cs
│   ├── AccountsController.cs
│   ├── CardsController.cs
│   ├── TransactionsController.cs
│   └── AdminController.cs
├── Contracts/
│   ├── CreateAccountRequest.cs
│   ├── CreateCardRequest.cs
│   ├── DepositRequest.cs
│   ├── WithdrawRequest.cs
│   └── TransferRequest.cs
├── Extensions/
│   ├── ClaimsPrincipalExtensions.cs
│   └── ControllerExtensions.cs
├── Middleware/
│   └── ExceptionMiddleware.cs
├── ServiceExtensions.cs
├── DatabaseExtensions.cs
└── Program.cs
```

---

### Program.cs

Startpunkten. Tre steg:
1. Registrera tjänster: `AddApplication()`, `AddInfrastructure()`, `AddIdentityServices()`, `AddApiServices()`
2. Initalisera databas: `InitialiseDatabaseAsync()` – kör migrationer + seed-data
3. Konfigurera middleware: `UseApiMiddleware()`

---

### ServiceExtensions.cs – UseApiMiddleware()

Middleware-pipeline i exakt ordning:

```
1. Säkerhetsheaders (X-Content-Type-Options, X-Frame-Options, Referrer-Policy)
2. HSTS (endast produktion)
3. ExceptionMiddleware (global felhantering)
4. UseHttpsRedirection
5. UseCors("CorsPolicy")
6. UseRateLimiter
7. UseAuthentication
8. UseAuthorization
9. MapControllers
10. MapHealthChecks("/health")
```

Ordningen är viktig: fel i ett tidigt steg (t.ex. felaktig request) ska aldrig nå controllers.

---

### Middleware/ExceptionMiddleware.cs

Fångar **alla** ej hanterade undantag från hela pipeline och returnerar strukturerade JSON-svar:

| Undantag | HTTP-kod | Svar |
|---|---|---|
| `ValidationException` | 400 | Lista med valideringsfel |
| `ConcurrencyException` | 409 | Conflict-meddelande |
| Övriga `Exception` | 500 | Generiskt felmeddelande (detaljer loggas, visas ej för klient) |

---

### Extensions/ClaimsPrincipalExtensions.cs

Extension methods på `ClaimsPrincipal` (dvs. `User` i controllers):

- `GetUserId()` – hämtar `sub`-claim (Identity userId)
- `IsStaff()` – returnerar `true` om användaren har Admin, BankManager, Teller eller Auditor-roll

Används i varje controller för att skicka med `UserId` och `IsStaff` till handlers.

---

### Extensions/ControllerExtensions.cs

`ToErrorResponse(result)` – konverterar ett misslyckat `Result` till rätt HTTP-svar:
- `NotFound` → `404 Not Found`
- `Failure` → `400 Bad Request`

---

### Controllers/AuthController.cs

| Metod | Endpoint | Beskrivning |
|---|---|---|
| `Register` | POST `/api/auth/register` | Registrerar ny användare med roll |
| `Login` | POST `/api/auth/login` | Loggar in, returnerar JWT-token |
| `Logout` | POST `/api/auth/logout` | Revokerar JWT-token via denylist |

Rate limiting: `[EnableRateLimiting("auth")]` – 5 req/min per IP.

---

### Controllers/AccountsController.cs

| Metod | Endpoint | Roll |
|---|---|---|
| `GetAll` | GET `/api/accounts` | Alla inloggade |
| `GetById` | GET `/api/accounts/{id}` | Alla inloggade |
| `Create` | POST `/api/accounts` | Alla utom Auditor |
| `Freeze` | PUT `/api/accounts/{id}/freeze` | Admin, BankManager, Teller |
| `Unfreeze` | PUT `/api/accounts/{id}/unfreeze` | Admin, BankManager, Teller |
| `Delete` | DELETE `/api/accounts/{id}` | Admin, BankManager, User |

---

### Controllers/TransactionsController.cs

| Metod | Endpoint | Roll |
|---|---|---|
| `GetByAccount` | GET `/api/transactions/account/{id}?page=1&pageSize=20` | Alla inloggade |
| `Deposit` | POST `/api/transactions/deposit` | Alla utom Auditor |
| `Withdraw` | POST `/api/transactions/withdraw` | Alla utom Auditor |
| `Transfer` | POST `/api/transactions/transfer` | Admin, BankManager, User |
| `PayInvoice` | POST `/api/transactions/invoice-payment` | Alla utom Auditor |

Idempotency-Key: valfri header `Idempotency-Key: {guid}` – förhindrar dubbla transaktioner vid retry.

---

### Controllers/CardsController.cs

| Metod | Endpoint | Roll |
|---|---|---|
| `Create` | POST `/api/cards` | Alla utom Auditor |
| `Block` | PUT `/api/cards/{id}/block` | Admin, BankManager |

---

### Controllers/AdminController.cs

| Metod | Endpoint | Roll |
|---|---|---|
| `CreateUser` | POST `/api/admin/users` | Admin |

Skapar användare med valfri roll. Personalroller kräver `@nexapay.com`-e-post.

---

### Contracts/

Data Transfer Objects för inkommande requests (request body). Används för att undvika mass assignment.

| Kontrakt | Fält |
|---|---|
| `CreateAccountRequest` | AccountName, AccountType, OwnerEmail (valfri – personal kan skapa konto åt en kund) |
| `CreateCardRequest` | AccountId, CardHolderName |
| `DepositRequest` | AccountId, Amount, Description |
| `WithdrawRequest` | AccountId, Amount, Description |
| `TransferRequest` | FromAccountId, ToAccountId, Amount, Description |
| `PayInvoiceRequest` | AccountId, Amount, Bankgiro, Ocr, Description |

---

## 6. NexaPay.Tests

```
NexaPay.Tests/
├── Application/
│   ├── Features/
│   │   ├── Accounts/
│   │   ├── Cards/
│   │   └── Transactions/
│   └── Validators/
├── Infrastructure/
│   └── Identity/
├── Integration/
│   └── (WebApplicationFactory-tester)
└── TestBase.cs
```

**TestBase.cs** – basklass för alla unit-tester:
- Skapar mock-objekt för `IUnitOfWork`, `IAccountRepository`, `ICardRepository`, `ITransactionRepository`
- Konfigurerar en riktig AutoMapper-instans (inte mockad)
- Hjälpmetoder: `CreateTestAccount()`, `CreateTestCard()`, `CreateTestTransaction()`

**Integration-tester** använder `NexaPayWebApplicationFactory` som:
- Ersätter SQL Server med en InMemory-databas
- Seedar testanvändare och roller
- Startar hela HTTP-stacken inklusive middleware

---

## 7. Steg-för-steg-flöden

### Flöde 1: Registrering

```
POST /api/auth/register
{ email, password, role }
         ↓
[Rate limit] max 5 req/min per IP
         ↓
[AuthController.Register]
  → skickar RegisterCommand via MediatR
         ↓
[LoggingBehavior] loggar "RegisterCommand" (känslig – detaljer utelämnade)
         ↓
[ValidationBehavior] kör RegisterCommandValidator
  • Email får inte vara tom
  • Lösenord minst 8 tecken, versaler, siffror, specialtecken
  • Roll måste vara giltig
  ✗ Misslyckas → loggar till AuditLogs + returnerar 400
  ✓ Passerar → fortsätter
         ↓
[ConcurrencyRetryBehavior] ej relevant för auth
         ↓
[AuditBehavior] väntar på svar
         ↓
[RegisterHandler]
  1. StaffEmailPolicy.Validate() – kräver @nexapay.com för personalroller
  2. AuthService.RegisterAsync()
     a. Kontrollerar e-post inte redan finns
     b. Skapar IdentityUser
     c. Hashar lösenord via UserManager
     d. Skapar roll om den inte finns
     e. Kopplar roll till användaren
     f. Genererar JWT-token (JwtService)
  3. Returnerar Result.Success(AuthDto)
         ↓
[AuditBehavior] loggar "RegisterCommand | Success: true" till ILogger + AuditLogs
         ↓
[AuthController] returnerar 200 OK { token, email, role, expiresAt }
```

---

### Flöde 2: Inloggning

```
POST /api/auth/login
{ email, password }
         ↓
[AuthController.Login]
  → skickar LoginCommand via MediatR
         ↓
[LoggingBehavior] "LoginCommand [känslig – detaljer utelämnade]"
         ↓
[ValidationBehavior] kör LoginCommandValidator
  • Email får inte vara tom
  • Lösenord får inte vara tomt
         ↓
[LoginHandler]
  → AuthService.LoginAsync()
    1. Hämtar användare via e-post
    2. Kontrollerar lockout INNAN lösenordsverifiering
    3. Verifierar lösenord
    4. Vid misslyckat: incrementar AccessFailedCount
       → Vid 5 misslyckanden: konto låst 15 min
    5. Vid lyckat: återställer räknaren
    6. Hämtar roller
    7. JwtService.GenerateToken(userId, email, role)
       → Skapar claims (sub, email, role, jti)
       → Signerar med HS256
       → Returnerar token + expiresAt
  → Returnerar AuthDto
         ↓
200 OK { token, email, role, expiresAt }
```

---

### Flöde 3: Insättning (autentiserad request)

```
POST /api/transactions/deposit
Authorization: Bearer {jwt-token}
{ accountId, amount, description }
Idempotency-Key: {guid} (valfri)
         ↓
[Rate limit] max 20 req/min per IP
         ↓
[UseAuthentication] validerar JWT-token
  1. Kontrollerar signatur, issuer, audience, lifetime
  2. Kontrollerar ValidAlgorithms = HS256
  3. OnTokenValidated: kontrollerar denylist (ITokenDenylist.IsRevoked)
     → Om token är revokerad → 401 Unauthorized
         ↓
[UseAuthorization] kontrollerar [Authorize(Roles = Roles.CanWrite)]
  → Auditor-rollen blockeras → 403 Forbidden
         ↓
[TransactionsController.Deposit]
  → sätter UserId = User.GetUserId() (sub-claim)
  → sätter IsStaff = User.IsStaff()
  → hämtar IdempotencyKey från header
  → skickar DepositCommand via MediatR
         ↓
[LoggingBehavior] loggar "DepositCommand { AccountId, Amount, ... }"
         ↓
[ValidationBehavior] kör DepositCommandValidator
  • AccountId får inte vara empty Guid
  • Amount måste vara > 0 och ≤ 1 000 000
  • Description max 500 tecken
         ↓
[ConcurrencyRetryBehavior] aktiveras om DbUpdateConcurrencyException kastas
         ↓
[AuditBehavior] väntar på svar
         ↓
[DepositHandler]
  1. Om IdempotencyKey skickats med: kontrollerar att nyckeln inte redan finns
     → Dubblettdetekterad → returnerar Result.Failure (400)
  2. Hämtar Account via UnitOfWork.Accounts.GetByIdAsync()
  3. Kontrollerar ägarskap (IsStaff eller OwnerId == UserId)
  4. account.Deposit(amount)
     a. Kontrollerar Status == Open
     b. Adderar beloppet till Balance
     c. Skapar Transaction-post med BalanceAfterTransaction
     d. Lägger till MoneyDeposited-event i account.DomainEvents
  5. UnitOfWork.SaveChangesAsync()
     a. Sparar Account (med nytt saldo) + Transaction till databasen
     b. Dispatchar MoneyDeposited-event via MediatR IPublisher
        → MoneyDepositedHandler.Handle()
           → INotificationService.NotifyTransactionAsync()
              → LoggingNotificationService loggar notifieringen
  6. Returnerar Result.Success(TransactionDto)
         ↓
[AuditBehavior] loggar "DepositCommand | User: {id} | Success: true" till AuditLogs
         ↓
[TransactionsController] returnerar 200 OK { transactionDto }
```

---

### Flöde 4: Utloggning (token-revokering)

```
POST /api/auth/logout
Authorization: Bearer {jwt-token}
         ↓
[AuthController.Logout]
  1. Hämtar JTI-claim från token: User.FindFirst(JwtRegisteredClaimNames.Jti)
  2. Hämtar token-expiry från exp-claim
  3. ITokenDenylist.Revoke(jti, expiry)
     → Redis: skriver jti → 1 med TTL = tid kvar till expiry
     → InMemory: lägger till i ConcurrentDictionary
  4. Returnerar 200 OK
         ↓
[Nästa request med samma token]
  → OnTokenValidated: ITokenDenylist.IsRevoked(jti) → true → 401 Unauthorized
```

---

### Flöde 5: Frysning av konto

```
PUT /api/accounts/{id}/freeze
Authorization: Bearer {jwt-token med Admin/BankManager/Teller-roll}
         ↓
[UseAuthorization] kontrollerar [Authorize(Roles = "Admin,BankManager,Teller")]
  → User-rollen blockeras → 403 Forbidden
         ↓
[AccountsController.Freeze]
  → skickar FreezeAccountCommand { AccountId, UserId, IsStaff }
         ↓
[ValidationBehavior] kontrollerar AccountId och UserId inte är empty Guid
         ↓
[FreezeAccountHandler]
  1. Hämtar Account via GetByIdAsync
  2. account.Freeze()
     a. Kontrollerar Status == Open (kastar InvalidOperationException om Frozen/Closed)
     b. Sätter Status = Frozen
  3. UnitOfWork.SaveChangesAsync()
  4. Returnerar Result.Success()
         ↓
200 OK "Konto frysts framgångsrikt"
```

---

### Flöde 6: Kontonummerkolllision (retry-loop)

```
[CreateAccountHandler]
  for (attempt = 0; attempt < 5; attempt++)
    accountNumber = GenerateAccountNumber() // slumpmässigt 10-siffrigt nummer
    if (!AccountNumberExistsAsync(accountNumber))
      account = Account.Open(accountNumber, ...)
      break
  
  if (account == null)
    return Result.Failure("Kunde inte generera unikt kontonummer")
  
  SaveChangesAsync()
```

---

### Flöde 7: Optimistisk concurrens (RowVersion)

```
[Två parallella Deposit-requests på samma konto]

Request A:                          Request B:
Hämtar Account (RowVersion = 1)     Hämtar Account (RowVersion = 1)
Kör Deposit()                       Kör Deposit()
SaveChangesAsync() ← lyckas         SaveChangesAsync() ← kastar DbUpdateConcurrencyException!
RowVersion = 2                      (RowVersion förväntades vara 1, är nu 2)
                                           ↓
                              [ConcurrencyRetryBehavior]
                              1. Rensar ChangeTracker
                              2. Försöker igen (läser Account med RowVersion = 2)
                              3. Kör Deposit() på ny data
                              4. SaveChangesAsync() ← lyckas denna gång
```

---

### Flöde 8: Domain events efter transfer

```
[TransferHandler]
  fromAccount.TransferTo(toAccount, amount)
    → fromAccount.Withdraw(amount)  → MoneyWithdrawn-event läggs till
    → toAccount.Deposit(amount)     → MoneyDeposited-event läggs till
    → MoneyTransferred-event läggs till

  UnitOfWork.SaveChangesAsync()
    Steg 1: Samla events från fromAccount och toAccount
    Steg 2: DbContext.SaveChangesAsync() → sparar 2 transaktioner + 2 kontosaldon
    Steg 3: Dispatcher events:
      → MoneyWithdrawnHandler → NotifyTransactionAsync (from owner)
      → MoneyDepositedHandler → NotifyTransactionAsync (to owner)
      → MoneyTransferredHandler → NotifyTransactionAsync (from owner)
```

---

*Dokumentation genererad 2026-05-11*
