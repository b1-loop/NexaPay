# NexaPay – Applikationsguide

> **Stack:** .NET 8 · ASP.NET Core · EF Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT  
> **Arkitektur:** Clean Architecture med CQRS via MediatR

---

## Projektstruktur

```
NexaPay.sln
├── NexaPay.Domain          – Entiteter, value objects, domänhändelser, gränssnitt
├── NexaPay.Application     – Handlers, validators, DTOs, pipeline behaviors, mappningar
├── NexaPay.Infrastructure  – EF Core, repositories, JWT, Identity, Redis, SMTP
├── NexaPay.API             – Controllers, middleware, Swagger, Program.cs
└── NexaPay.Tests           – Enhetstester och integrationstester
```

Beroenderiktningen pekar alltid **inåt**:
- API → Application → Domain
- Infrastructure → Application → Domain
- Domain har **inga** externa NuGet-beroenden

---

## NexaPay.Domain

Kärnan i systemet. Inga beroenden till externa ramverk – bara ren C#.

### Entities

#### `BaseEntity`
Basklass för alla domänentiteter. Innehåller `Id` (Guid), `CreatedAt`, `UpdatedAt` och mekaniken för domänhändelser.

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public IReadOnlyList<IDomainEvent> PopDomainEvents() { ... } // returnerar och rensar listan
}
```

#### `Account`
Aggregatrot för bankkonton. All kontologik finns här.

| Metod | Vad den gör |
|---|---|
| `Open(...)` | Statisk fabriksmetod – skapar nytt konto med `AccountStatus.Open` |
| `Deposit(amount, description, idempotencyKey)` | Lägger till pengar, höjer Balance, skapar Transaction, höjer `MoneyDeposited`-event |
| `Withdraw(amount, description, idempotencyKey)` | Drar av pengar, kontrollerar att saldo räcker, skapar Transaction, höjer `MoneyWithdrawn`-event |
| `TransferTo(amount, description, receiver, idempotencyKey)` | Tar pengar från detta konto och lägger på mottagarkontot, returnerar ett par (FromTransaction, ToTransaction), höjer `MoneyTransferred`-event |
| `Freeze()` | Sätter `Status = Frozen` – inga transaktioner möjliga |
| `Unfreeze()` | Sätter `Status = Open` |
| `Close()` | Sätter `Status = Closed` – kräver att saldo är 0, höjer `AccountClosed`-event |

Domänskyddet: `AccountName`, `AccountType`, `OwnerId`, `Balance`, `Status` har `private set` – de kan bara ändras via metoderna ovan, aldrig direkt utifrån.

`RowVersion` (byte[]) används av EF Core för **optimistisk concurrenskontroll** – om två requests sparar samma konto samtidigt kastar EF `DbUpdateConcurrencyException`.

#### `Card`
Representerar ett betalkort knutet till ett konto.

| Metod | Vad den gör |
|---|---|
| `Activate()` | Sätter `Status = Active` (från `Inactive`) |
| `Block()` | Sätter `Status = Blocked`, höjer `CardBlocked`-event |
| `Unblock()` | Sätter `Status = Active` (från `Blocked`) |
| `MarkAsExpired()` | Sätter `Status = Expired` |

Kortdata som lagras: `CardToken` (128-bit random hex, ersätter PAN), `Last4Digits`, `CardHolderName`, `ExpiryDate`. Fullständigt kortnummer (PAN) returneras **bara en gång** vid skapandet och lagras aldrig.

#### `Transaction`
Oföränderlig post om en genomförd transaktion. Alla properties har `init` – de kan aldrig ändras efter skapandet.

Fält: `Amount` (Money), `Type` (Deposit/Withdrawal/Transfer), `Description`, `BalanceAfterTransaction`, `AccountId`, `ReceiverAccountId` (vid transfer), `IdempotencyKey`.

### Value Objects

#### `Money`
Representerar ett penningbelopp med valuta. Är **immutable** – operationer returnerar nya instanser.

```csharp
public sealed class Money : IEquatable<Money>
{
    public decimal Amount { get; private set; }   // avrundas till 2 decimaler
    public Currency Currency { get; private set; }

    // Skyddar mot negativt belopp vid konstruktion
    public Money(decimal amount, Currency currency) { ... }

    // Aritmetik – kontrollerar att valutor matchar
    public static Money operator +(Money a, Money b) { ... }
    public static Money operator -(Money a, Money b) { ... }
    // Jämförelseoperatorer: >, <, >=, <=
}
```

Valutakontroll sker automatiskt i `EnforceSameCurrency()` – blandar du SEK och USD kastas `InvalidOperationException`.

### Enumerations

| Enum | Värden |
|---|---|
| `AccountStatus` | Open, Frozen, Closed |
| `AccountType` | Checking, Savings |
| `CardStatus` | Inactive, Active, Blocked, Expired |
| `TransactionType` | Deposit, Withdrawal, Transfer |
| `Currency` | SEK, USD, EUR |

### Domain Events

Händelser som raised inuti domänentiteterna och dispatched av `UnitOfWork` **efter** lyckad save till databasen.

| Event | Raised av | Innehåller |
|---|---|---|
| `MoneyDeposited` | `Account.Deposit()` | AccountId, OwnerId, Amount, NewBalance, OccurredAt |
| `MoneyWithdrawn` | `Account.Withdraw()` | AccountId, OwnerId, Amount, NewBalance, OccurredAt |
| `MoneyTransferred` | `Account.TransferTo()` | FromAccountId, ToAccountId, OwnerId, Amount, OccurredAt |
| `AccountClosed` | `Account.Close()` | AccountId, OwnerId, OccurredAt |
| `CardBlocked` | `Card.Block()` | CardId, AccountId, OccurredAt |

Alla är `sealed record` som implementerar `IDomainEvent` (= `INotification` från MediatR).

### Domain Interfaces

`IUnitOfWork` – kontraktet som Application-lagret använder för att spara ändringar och komma åt repositories:

```csharp
public interface IUnitOfWork
{
    IAccountRepository Accounts { get; }
    ICardRepository Cards { get; }
    ITransactionRepository Transactions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

---

## NexaPay.Application

Affärslogiken. Känner till Domain men inte Infrastructure eller API.

### CQRS-mönstret

Varje operation är antingen ett **Command** (förändrar tillstånd) eller en **Query** (läser data):

```
Command/Query → MediatR → Pipeline Behaviors → Handler → Result<T>
```

Alla Commands och Queries returnerar `Result<T>` eller `Result` – aldrig exceptions till controllers.

### MediatR Pipeline Behaviors

Registreras i `DependencyInjection.cs` (Application) och körs i denna ordning för **varje** request:

```
Logging → Validation → ConcurrencyRetry → Audit → Handler
```

#### 1. `LoggingBehavior<TRequest, TResponse>`

Loggar alla inkommande requests med Serilog/ILogger. Mäter svarstid med `Stopwatch`.

- Om `TRequest` implementerar `ISensitiveRequest` (t.ex. `LoginCommand`) loggas bara requesttypen – **inte** payloaden (lösenord syns aldrig i loggar)
- Varning loggas om en request tar **> 500 ms**
- Körs för alla requests automatiskt

#### 2. `ValidationBehavior<TRequest, TResponse>`

Hämtar alla `IValidator<TRequest>` som FluentValidation registrerat och kör dem parallellt.

- Inga validators registrerade → request passerar direkt
- Valideringsfel samlas och kastas som `ValidationException` (fångas av `ExceptionMiddleware` → 400 Bad Request)
- Kommandon (inte Queries) med valideringsfel skrivs till `AuditLogs`-tabellen via `IAuditService`

#### 3. `ConcurrencyRetryBehavior<TRequest, TResponse>`

Fångar `ConcurrencyException` (kastad av UnitOfWork vid `DbUpdateConcurrencyException`) och försöker om **upp till 2 gånger**. `UnitOfWork` rensar `ChangeTracker` innan det kastar, så nästa försök läser färsk data från databasen.

```csharp
catch (ConcurrencyException) when (attempt++ < MaxRetries) { /* retry */ }
```

#### 4. `AuditBehavior<TRequest, TResponse>`

Körs **sist** – bara kommandon som passerat validering auditeras. Queries (`requestName.EndsWith("Query")`) hoppas över.

Skriver till `AuditLogs`-tabellen via `IAuditService`: kommandonamn, userId, om det lyckades, timestamp. Inspekterar `IResult.IsSuccess` på svaret typesäkert via interface.

### Application Interfaces

Definerade i `Application/Common/Interfaces/` – implementerade i Infrastructure:

| Interface | Vad det gör |
|---|---|
| `IAuthService` | Registrering, inloggning, bekräfta e-post, återställ lösenord, byt lösenord |
| `IJwtService` | Generera JWT-token |
| `ITokenDenylist` | Revokera och kontrollera tokens (Redis eller InMemory) |
| `INotificationService` | Skicka e-post vid transaktioner, kortblockeringar osv. |
| `IAuditService` | Skriv audit-poster till databasen |
| `IStaffEmailPolicy` | Validera att personalroller har @nexapay.com-e-post |

### Validators (FluentValidation)

Varje Command och Query har en validator som registreras automatiskt. Valideringen körs av `ValidationBehavior` i pipeline.

**Exempel – DepositCommandValidator:**
- `AccountId`: NotEmpty
- `Amount`: GreaterThan(0), LessThanOrEqualTo(TransactionPolicy.MaxAmount)
- `Description`: NotEmpty, MaximumLength(TransactionPolicy.MaxDescriptionLength)
- `UserId`: NotEmpty

**TransactionPolicy** centraliserar gränsvärden – max belopp och max beskrivningslängd på ett ställe.

**TransferValidator** kontrollerar även att `FromAccountId != ToAccountId`.

### Event Handlers

Fem handlers i `Application/Common/EventHandlers/` – lyssnar på domänhändelser och skickar e-postnotifieringar via `INotificationService`:

| Handler | Händelse | E-post skickas om |
|---|---|---|
| `MoneyDepositedHandler` | `MoneyDeposited` | Insättning genomförd |
| `MoneyWithdrawnHandler` | `MoneyWithdrawn` | Uttag genomfört |
| `MoneyTransferredHandler` | `MoneyTransferred` | Överföring genomförd |
| `AccountClosedHandler` | `AccountClosed` | Konto stängt |
| `CardBlockedHandler` | `CardBlocked` | Kort blockerat |

`CardBlockedHandler` slår upp kontot via `IUnitOfWork` för att hitta `OwnerId` och därmed e-postadressen.

### AutoMapper – `MappingProfile`

Konverterar domänentiteter till DTOs:

| Från | Till | Anmärkning |
|---|---|---|
| `Account` | `AccountDto` | `Balance` = `Money.Amount`, `Currency` = `Money.Currency.ToString()`, `AccountType`/`Status` som strängar |
| `Card` | `CardDto` | `MaskedCardNumber` = `"**** **** **** {Last4Digits}"` |
| `Transaction` | `TransactionDto` | `Amount`/`BalanceAfterTransaction` = `Money.Amount`, `Type` som sträng |

### Result<T>-mönstret

Alla handlers returnerar `Result<T>` eller `Result` istället för att kasta exceptions för förväntade fel:

```csharp
// Lyckat svar
return Result<AccountDto>.Success(dto);

// Affärsregelfel → 400 Bad Request
return Result<AccountDto>.Failure("Otillräckligt saldo");

// Entitet hittades ej → 404 Not Found
return Result<AccountDto>.NotFound("Kontot hittades inte");
```

`ResultErrorType` (None, NotFound, BusinessRule) används av `ResultExtensions.ToErrorResponse()` i controllers för att avgöra HTTP-statuskod.

---

## NexaPay.Infrastructure

Tekniska implementationer. Känner till Application och Domain men exponeras aldrig uppåt.

### Entity Framework Core

#### `ApplicationDbContext`

Ärver från `IdentityDbContext<IdentityUser, IdentityRole, string>` för att få alla Identity-tabeller (`AspNetUsers`, `AspNetRoles` osv.) automatiskt.

Egna tabeller: `Accounts`, `Cards`, `Transactions`, `AuditLogs`.

**Global query filter** på `Account`:
```csharp
modelBuilder.Entity<Account>().HasQueryFilter(a => a.Status != AccountStatus.Closed);
```
Stängda konton är osynliga i vanliga queries. Staff-queries anropar `.IgnoreQueryFilters()` för full synlighet.

#### Konfigurationer

I `Persistence/Configurations/` finns en konfigurationsfil per entitet (t.ex. `AccountConfiguration.cs`) som implementerar `IEntityTypeConfiguration<T>`. Dessa registreras automatiskt via `modelBuilder.ApplyConfigurationsFromAssembly(...)`.

- **Money** är konfigurerat som owned type med kolumnerna `Amount` och `Currency`
- **Transaction.Account** är optional relation (nullable foreign key)
- **Idempotency-Key** har filtrerat unikt index: `WHERE IdempotencyKey IS NOT NULL`
- **RowVersion** på `Account` ger EF Core optimistisk concurrens

### UnitOfWork

`UnitOfWork` samlar de tre repositories och hanterar `SaveChangesAsync`:

1. Samlar alla domänhändelser från alla spårade `BaseEntity`-objekt via `PopDomainEvents()`
2. Anropar `_context.SaveChangesAsync()` – om detta kastar är inga händelser dispatchade
3. Om save lyckas dispatchar `IPublisher` (MediatR) alla händelser till sina handlers
4. Om `DbUpdateConcurrencyException` kastas: rensar `ChangeTracker` och kastar `ConcurrencyException` (som `ConcurrencyRetryBehavior` fångar)

### Repositories

`AccountRepository`, `CardRepository`, `TransactionRepository` – implementerar respektive interface från Domain.

Alla **read-only queries** använder `.AsNoTracking()` för att undvika EF Core-overhead.

`AccountRepository` har `AccountNumberExistsAsync()` och `AccountOwnedByAsync()` för effektiva bool-kontroller utan att ladda hela entiteter.

### `AuthService`

Implementerar `IAuthService` med `UserManager<IdentityUser>` och `RoleManager<IdentityRole>` från ASP.NET Identity.

**Lockout kontrolleras FÖRE lösenordsvalidering** – undviker timing oracle-attack.

Skapar användare med `EmailConfirmed = false` vid registrering, genererar bekräftelsetoken och skickar e-post via `INotificationService`.

### `JwtService`

Genererar JWT-tokens med `JsonWebTokenHandler` (modern API). Läser `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryHours` från konfigurationen. Loggar varning och faller tillbaka på 24 h om `ExpiryHours` saknas.

Claims i token: `sub` (userId), `email`, `role`, `jti` (unikt token-ID för denylist).

### Token Denylist

**`RedisTokenDenylist`** – lagrar revokerade token-JTI:er i Redis med TTL = token-expiry. Fail-open vid Redis-avbrott (loggar varning, blockerar inte autentiserade requests).

**`InMemoryTokenDenylist`** – fallback om Redis inte är konfigurerat. Stödjer inte horisontell skalning.

Revokering sker på `/auth/logout` – JTI och expiry extraheras från JWT-claims och sparas i denylist. Vid varje request valideras token av `OnTokenValidated`-eventet i JWT Bearer-middleware.

### `SmtpNotificationService`

Skickar riktiga e-postmeddelanden via Gmail SMTP. Konfigureras i `appsettings.Development.json` (gitignorerad – credentials pushas aldrig).

Metoder:
- `NotifyTransactionAsync(ownerId, subject, body)` – slår upp e-postadress från `UserManager` via `ownerId`
- `NotifyEmailConfirmationAsync(email, token)` – skickar bekräftelsetoken direkt till angiven e-post
- `NotifyPasswordResetAsync(email, token)` – skickar återställningstoken direkt till angiven e-post

Graceful fallback om SMTP inte är konfigurerat – loggar varning och returnerar utan fel.

`MailMessage` kasseras med `using var` för korrekt resurshantering.

### `EfAuditService`

Implementerar `IAuditService`. Skapar `AuditLog`-entiteter och sparar dem i `AuditLogs`-tabellen.

### `StaffEmailPolicy`

Kontrollerar att personalroller (Admin, BankManager, Teller, Auditor) bara kan registreras med e-postadresser på `@nexapay.com`-domänen. Validerar att `StaffDomain` är icke-tom och innehåller en punkt.

---

## NexaPay.API

Det yttersta lagret. HTTP-gränssnitt mot omvärlden.

### Program.cs

Minimal startpunkt – allt ansvar delegeras:

```csharp
builder.Services.AddApplication();         // Application-lagret
builder.Services.AddInfrastructure(...);   // Infrastructure-lagret
builder.Services.AddIdentityServices();    // ASP.NET Identity
builder.Services.AddApiServices(...);      // Controllers, Swagger, CORS, Rate limiting

await app.InitialiseDatabaseAsync();       // Migreringar + rollseeding
app.UseApiMiddleware();                    // Middleware-pipeline
app.Run();
```

### Middleware-pipeline (i ordning)

```
1. Säkerhetsheaders        – X-Content-Type-Options, X-Frame-Options, Referrer-Policy (alla svar)
2. HSTS                    – Strict-Transport-Security (endast Production)
3. ExceptionMiddleware     – Global felhantering (ValidationException → 400, ConcurrencyException → 409, övriga → 500)
4. HTTPS-redirect          – Omdirigerar HTTP → HTTPS
5. CORS                    – Tillåter konfigurerade origins; nekar allt om ingen origin konfigurerad
6. Rate Limiter            – Körs FÖR autentisering för maximal effekt
7. Authentication          – Validerar JWT, kontrollerar denylist via OnTokenValidated
8. Authorization           – Kontrollerar roller och policies
9. MapControllers          – Routar till rätt controller
10. MapHealthChecks /health – Returnerar databas + Redis-status utan autentisering
```

### ServiceExtensions.cs

Innehåller tre extension methods:

**`AddIdentityServices()`** – Konfigurerar ASP.NET Identity med lösenordskrav (8 tecken, versaler, siffror, specialtecken), kontolåsning (5 misslyckanden → 15 min), krav på unik e-post. Återställer DefaultChallengeScheme till JWT Bearer (AddIdentity sätter annars cookie-redirect som ger 404 istället för 401).

**`AddApiServices(configuration)`** – Registrerar Controllers, API-versionshantering (v1.0, stöd för query string `?api-version=1.0` och header `X-API-Version`), rate limiting, health checks, Swagger med JWT-stöd, CORS.

**`UseApiMiddleware()`** – Konfigurerar middleware-pipeline (se ordning ovan).

### DatabaseExtensions.cs

`InitialiseDatabaseAsync()` – Körs vid uppstart:
1. Kör EF Core-migrationer (InMemory → `EnsureCreated`, Production → loggar varning + migrerar, övriga → migrerar)
2. Seedar alla 5 roller (`SeedRolesAsync`)

### API Extensions

#### `ClaimsPrincipalExtensions`

```csharp
user.GetUserId()  // Hämtar NameIdentifier-claim (userId)
user.IsStaff()    // True om Admin, BankManager, Teller eller Auditor
user.IsAdmin()    // True om Admin
```

Används i alla controllers för att extrahera userId och rollinfo från JWT-token.

#### `ResultExtensions`

```csharp
this.ToErrorResponse(result)
// NotFound → 404, BusinessRule → 400
```

Mappar `Result.ErrorType` till rätt HTTP-statuskod utan att duplicera logiken i varje controller.

### Contracts (Request-klasser)

Request-klasser i `Controllers/Contracts/` definierar request-body för varje endpoint:

| Kontrakt | Endpoint | Fält |
|---|---|---|
| `RegisterRequest` | POST /auth/register | Email, Password, Role |
| `LoginRequest` | POST /auth/login | Email, Password |
| `ConfirmEmailRequest` | POST /auth/confirm-email | UserId, Token |
| `ForgotPasswordRequest` | POST /auth/forgot-password | Email |
| `ResetPasswordRequest` | POST /auth/reset-password | Email, Token, NewPassword |
| `ChangePasswordRequest` | POST /auth/change-password | CurrentPassword, NewPassword |
| `CreateAccountRequest` | POST /api/accounts | AccountName, AccountType |
| `DepositRequest` | POST /api/transactions/deposit | AccountId, Amount, Description |
| `WithdrawRequest` | POST /api/transactions/withdraw | AccountId, Amount, Description |
| `TransferRequest` | POST /api/transactions/transfer | FromAccountId, ToAccountId, Amount, Description |
| `CreateCardRequest` | POST /api/cards | AccountId, CardHolderName |
| `BlockCardRequest` | PUT /api/cards/{id}/block | Reason |

### Controllers

#### `AuthController` – `/api/auth`

Rate limiting: `"auth"` (5 req/min per IP) på hela controllern. Bekräfta-e-post, glömt lösenord, återställ och byt lösenord har `[DisableRateLimiting]`.

| Endpoint | Auth | Beskrivning |
|---|---|---|
| POST /register | Nej | Skapar User-konto, skickar bekräftelsemail |
| POST /login | Nej | Returnerar JWT-token om e-post bekräftad |
| POST /confirm-email | Nej | Bekräftar e-postadress med token |
| POST /forgot-password | Nej | Skickar återställningsmail (avslöjar aldrig om adressen finns) |
| POST /reset-password | Nej | Sätter nytt lösenord med token |
| POST /change-password | Ja | Byter lösenord med nuvarande lösenord |
| POST /logout | Ja | Revokerar JWT-token i denylist |

Personalroller (Admin, BankManager, Teller, Auditor) kan **inte** registreras via `/register` – kräver `AdminController`.

#### `AccountsController` – `/api/accounts`

Rate limiting: `"financial"` (20 req/min per IP).

| Endpoint | Roller | Beskrivning |
|---|---|---|
| GET /accounts | Alla inloggade | Staff ser alla, User ser bara sina egna |
| GET /accounts/{id} | Alla inloggade | Staff ser alla, User bara sina egna |
| POST /accounts | CanWrite (ej Auditor) | Skapar konto med ownerId från JWT |
| PUT /accounts/{id}/freeze | CanWriteAccounts (Admin, BankManager, Teller) | Fryser konto |
| PUT /accounts/{id}/unfreeze | CanWriteAccounts | Avfryser konto |
| DELETE /accounts/{id} | CanDelete (Admin, BankManager, User) | Stänger konto (kräver saldo 0) |

#### `TransactionsController` – `/api/transactions`

Rate limiting: `"financial"`. Stödjer idempotens via `Idempotency-Key`-header (valfri GUID).

| Endpoint | Roller | Beskrivning |
|---|---|---|
| GET /transactions/account/{id} | Alla inloggade | Paginerad lista (?page=1&pageSize=20) |
| POST /transactions/deposit | CanWrite (ej Auditor) | Insättning |
| POST /transactions/withdraw | CanWrite (ej Auditor) | Uttag |
| POST /transactions/transfer | CanTransfer (Admin, BankManager, User) | Överföring mellan konton |

#### `CardsController` – `/api/cards`

Rate limiting: `"financial"`.

| Endpoint | Roller | Beskrivning |
|---|---|---|
| GET /cards/account/{id} | Alla inloggade | Hämtar kort för ett konto |
| POST /cards | CanWrite (ej Auditor) | Skapar kort med Luhn-giltig PAN |
| PUT /cards/{id}/activate | CanWrite | Aktiverar inaktivt kort |
| PUT /cards/{id}/block | CanBlockCard (Admin, BankManager) | Blockerar kort |
| PUT /cards/{id}/unblock | CanBlockCard | Avblockerar kort |

#### `AdminController` – `/api/admin`

Kräver `[Authorize(Roles = Roles.Admin)]`. Registrerar personalroller med `StaffEmailPolicy`-kontroll.

---

## Rollsystem

### Rollkonstanter

```csharp
Roles.Admin        = "Admin"
Roles.BankManager  = "BankManager"
Roles.Teller       = "Teller"
Roles.Auditor      = "Auditor"
Roles.User         = "User"
```

### Kombinerade roller för [Authorize]

| Konstant | Inkluderar |
|---|---|
| `CanWrite` | Admin, BankManager, Teller, User |
| `CanWriteAccounts` | Admin, BankManager, Teller |
| `CanTransfer` | Admin, BankManager, User |
| `CanDelete` | Admin, BankManager, User |
| `CanBlockCard` | Admin, BankManager |
| `CanViewAllAccounts` | Admin, BankManager, Teller, Auditor |
| `AllStaff` | Admin, BankManager, Teller, Auditor |

### Rollhierarki

```
Admin        → Full åtkomst till allt
BankManager  → Kan se allt och blockera kort, ej adminoperationer
Teller       → Kan hjälpa kunder (in/uttag) men ej överföringar eller kortblockering
Auditor      → Bara läsning (inga POST/PUT/DELETE)
User         → Ser bara sina egna konton/transaktioner
```

Personalroller kräver `@nexapay.com`-e-postadress (kontrolleras av `StaffEmailPolicy`).

---

## Rate Limiting

Konfigurerat med **Fixed Window** per IP-adress:

| Policy | Gräns | Endpoints |
|---|---|---|
| `"auth"` | 5 req/min | `AuthController` (förhindrar brute-force) |
| `"financial"` | 20 req/min | Accounts, Cards, Transactions |

Överskridna gränser → **HTTP 429 Too Many Requests**.

Logout, confirm-email, forgot-password, reset-password och change-password har `[DisableRateLimiting]`.

---

## Health Checks

`GET /health` (ingen autentisering) – returnerar status för:
- **database** – `SELECT 1` mot SQL Server via `AddDbContextCheck`
- **redis** – ping via `IConnectionMultiplexer` (rapporterar "ej konfigurerat" som Healthy om Redis saknas)

---

## API-versionshantering

Aktuell version: **v1.0**. Versionen kan anges via:
- Query string: `?api-version=1.0`
- Header: `X-API-Version: 1.0`

Anrop utan version antas vara v1.0 (`AssumeDefaultVersionWhenUnspecified = true`).

---

## Swagger

Tillgänglig i Development på `/swagger`. Konfigurerad med JWT Bearer-stöd:

1. Logga in via POST /api/auth/login
2. Kopiera token från svaret
3. Klicka Authorize och skriv: `Bearer {token}`
4. Alla skyddade endpoints är nu tillgängliga

---

## Kompletta flöden

### Registrering + e-postbekräftelse

```
POST /api/auth/register { email, password, role: "User" }
  → AuthController.Register()
    → Kontrollerar att role == "User" (personalroller blockeras)
    → mediator.Send(RegisterCommand)
      → [LoggingBehavior] loggar request
      → [ValidationBehavior] kör RegisterCommandValidator
      → [AuditBehavior] auditerar
      → RegisterCommandHandler
        → AuthService.RegisterAsync()
          → Kontrollerar att e-posten inte redan finns
          → Skapar IdentityUser med EmailConfirmed = false
          → Lägger till roll via RoleManager
          → Genererar bekräftelsetoken (UserManager.GenerateEmailConfirmationTokenAsync)
          → Skickar mail via SmtpNotificationService.NotifyEmailConfirmationAsync()
          → Returnerar AuthDto { RequiresEmailConfirmation = true, Token = "" }
  → 200 OK: "Registrering lyckades. Bekräfta din e-post."

POST /api/auth/confirm-email { userId, token }
  → AuthService.ConfirmEmailAsync()
    → UserManager.ConfirmEmailAsync() → sätter EmailConfirmed = true
  → 200 OK: "E-postadressen har bekräftats."
```

### Inloggning

```
POST /api/auth/login { email, password }
  → LoginCommand → LoginCommandHandler → AuthService.LoginAsync()
    1. Hitta användare (FindByEmailAsync)
    2. Kontrollera lockout (IsLockedOutAsync) – FÖRE lösenordskontroll
    3. Kontrollera lösenord (CheckPasswordAsync)
    4. Kontrollera EmailConfirmed – blockerar om false
    5. Återställ access-failed-räknare (ResetAccessFailedCountAsync)
    6. Hämta roller, generera JWT-token (JwtService.GenerateToken)
  → 200 OK: { token, email, role, expiresAt }
```

### Insättning (Deposit)

```
POST /api/transactions/deposit { accountId, amount, description }
  Header: Authorization: Bearer <token>
  Header: Idempotency-Key: <guid> (valfri)

  → TransactionsController.Deposit()
    → Extraherar userId (GetUserId()), isStaff (IsStaff()), idempotencyKey
    → mediator.Send(DepositCommand)
      → [Logging] loggar
      → [Validation] DepositCommandValidator:
          - amount > 0 och ≤ maxgräns
          - description ej tom, ≤ maxlängd
      → [ConcurrencyRetry] redo att fånga ConcurrencyException
      → [Audit] auditerar efter körning
      → DepositCommandHandler:
          1. Kontrollerar idempotens (hittas befintlig transaktion med nyckeln → returnera den)
          2. Hämtar konto (UnitOfWork.Accounts.GetByIdAsync)
          3. Kontrollerar ägarskap (staff kan deposita på alla konton)
          4. Anropar account.Deposit(amount, description, idempotencyKey)
             → Balance ökas, Transaction skapas, MoneyDeposited raised
          5. Lägger till transaktionen i kontexten
          6. UnitOfWork.SaveChangesAsync()
             → PopDomainEvents() → sparar → dispatchar MoneyDeposited
             → MoneyDepositedHandler: skickar e-postnotifiering
          7. Mappar Transaction → TransactionDto (AutoMapper)
          8. Returnerar Result<TransactionDto>.Success(dto)
  → 200 OK: { transaction }
```

### Uttag (Withdraw)

Identiskt med insättning men anropar `account.Withdraw()` som kontrollerar att saldo räcker.

### Överföring (Transfer)

```
POST /api/transactions/transfer { fromAccountId, toAccountId, amount, description }

  → TransferCommandHandler:
    1. Kontrollerar att from ≠ to (validator)
    2. Hämtar båda konton
    3. Kontrollerar ägarskap (User måste äga from-kontot; staff är undantagna)
    4. Kontrollerar att båda konton har samma valuta (explicit felmeddelande)
    5. Anropar account.TransferTo(amount, description, receiver, idempotencyKey)
       → Returnerar (FromTransaction, ToTransaction)
    6. Sparar båda transaktioner
    7. UnitOfWork.SaveChangesAsync() → dispatchar MoneyTransferred
```

### Lösenordsåterställning

```
POST /api/auth/forgot-password { email }
  → AuthService.ForgotPasswordAsync()
    → Söker efter användare (FindByEmailAsync)
    → Om användare finns OCH EmailConfirmed: genererar token + skickar mail
    → Returnerar ALLTID success (avslöjar inte om e-posten finns)
  → 200 OK (alltid, oavsett om e-posten finns)

POST /api/auth/reset-password { email, token, newPassword }
  → AuthService.ResetPasswordAsync()
    → UserManager.ResetPasswordAsync()
    → Sätter nytt lösenord om token är giltig
  → 200 OK eller 400 Bad Request
```

### Logga ut

```
POST /api/auth/logout
  Header: Authorization: Bearer <token>

  → AuthController.Logout()
    → Extraherar JTI och exp från JWT-claims
    → TokenDenylist.Revoke(jti, expiry)
       → Redis: SETEX {jti} {ttl} "revoked"
       → InMemory: lägger till i ConcurrentDictionary med expiry
    → 200 OK: "Utloggning lyckades"

Nästkommande request med samma token:
  → JWT Bearer middleware validerar token (signatur, expiry, issuer, audience)
  → OnTokenValidated: TokenDenylist.IsRevoked(jti) → true → context.Fail()
  → 401 Unauthorized
```

### Skapa kort

```
POST /api/cards { accountId, cardHolderName }

  → CreateCardHandler:
    1. Verifierar att kontot finns och tillhör användaren
    2. Genererar 128-bit CardToken (RandomNumberGenerator.GetBytes(16) → hex)
    3. Genererar Luhn-giltig 16-siffrig PAN
    4. Sätter ExpiryDate = idag + 4 år
    5. Skapar Card med Status = Inactive
    6. Sparar i databas
    7. Returnerar CreateCardResponse med full PAN (visas bara denna gång)
    → PAN lagras ALDRIG – bara Last4Digits och CardToken
```

### Frysa/Avfrysa konto

```
PUT /api/accounts/{id}/freeze    (kräver CanWriteAccounts)
PUT /api/accounts/{id}/unfreeze  (kräver CanWriteAccounts)

  → FreezeAccountHandler / UnfreezeAccountHandler:
    1. Hämtar konto
    2. Anropar account.Freeze() / account.Unfreeze()
       → Domänmetoden validerar att operationen är giltig
    3. UnitOfWork.SaveChangesAsync()
```

### Blockera/Avblockera kort

```
PUT /api/cards/{id}/block   { reason }  (kräver CanBlockCard: Admin, BankManager)
PUT /api/cards/{id}/unblock             (kräver CanBlockCard)

  → BlockCardHandler:
    1. Hämtar kort
    2. Anropar card.Block() → höjer CardBlocked-event
    3. Sparar → CardBlockedHandler skickar e-postnotifiering

  → UnblockCardHandler:
    1. Hämtar kort
    2. Anropar card.Unblock()
    3. Sparar
```

---

## Databas

### SQL Server-tabeller

| Tabell | Innehåll |
|---|---|
| `Accounts` | Bankkonton med Balance, Status, RowVersion |
| `Cards` | Betalkort med CardToken, Last4Digits |
| `Transactions` | Alla transaktioner med idempotency-nyckel |
| `AuditLogs` | Alla kommandoanrop med userId och outcome |
| `AspNetUsers` | Identity-användare |
| `AspNetRoles` | Roller (Admin, BankManager, Teller, Auditor, User) |
| `AspNetUserRoles` | Koppling användare ↔ roll |
| `__EFMigrationsHistory` | Körda EF Core-migrationer |

### Migrationer

Körs automatiskt vid uppstart via `DatabaseExtensions.InitialiseDatabaseAsync()`. Skapar databasen om den inte finns.

---

## Testprojekt

`NexaPay.Tests` innehåller enhetstester och integrationstester (160 tester).

**Enhetstester** – testar enskilda klasser isolerat med mock-beroenden (Moq).

**Integrationstester** – testar hela HTTP-stacken via `NexaPayWebApplicationFactory`:
- InMemory-databas (EF Core)
- Rollseeding
- Testar middleware, autentisering, rate limiting

`RateLimitingIntegrationTests` använder `[SetUp]`/`[TearDown]` för att ge varje test en färsk factory och klient, vilket isolerar rate limit-buckets.

E-postbekräftelse kringgås i tester med `UserManager.GenerateEmailConfirmationTokenAsync` + `ConfirmEmailAsync` direkt (ingen SMTP nödvändig).
