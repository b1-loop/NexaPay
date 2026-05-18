# NexaPay – Backend (.NET 8 Web API)

Ett modernt bank-API byggt med .NET 8 och **Clean Architecture**. NexaPay hanterar bankkonton, betalkort och finansiella transaktioner med fullständig rollbaserad åtkomstkontroll, JWT-autentisering, idempotenta operationer, domain events, optimistisk samtidighetskontroll och en MediatR-pipeline i fyra steg.

> **Backend-repo:** https://github.com/b1-loop/NexaPay
> **Frontend-repo:** https://github.com/Haval-Jalal/NexaPay-FE

---

## Innehåll

- [Vad NexaPay gör](#vad-nexapay-gör)
- [Snabbstart](#snabbstart)
- [Tech stack](#tech-stack)
- [Arkitektur](#arkitektur)
- [Projektstruktur](#projektstruktur)
- [Domänlager (Domain)](#domänlager-domain)
- [Applikationslager (Application)](#applikationslager-application)
- [Infrastrukturlager (Infrastructure)](#infrastrukturlager-infrastructure)
- [API-lager (API)](#api-lager-api)
- [Autentisering och behörighet](#autentisering-och-behörighet)
- [Request-pipeline](#request-pipeline)
- [Domain Events](#domain-events)
- [API-endpoints](#api-endpoints)
- [Rollbaserad behörighet (RBAC)](#rollbaserad-behörighet-rbac)
- [Idempotens](#idempotens)
- [Databas och migrationer](#databas-och-migrationer)
- [Tester](#tester)
- [Installation](#installation)
- [Konfiguration](#konfiguration)
- [Säkerhet](#säkerhet)
- [Bidra till projektet](#bidra-till-projektet)
- [Diagram och dokumentation](#diagram-och-dokumentation)
- [Licens och författare](#licens-och-författare)

---

## Vad NexaPay gör

NexaPay är ett backend-API för en bank. Det låter:

- **Kunder** registrera sig, logga in, bekräfta e-post, skapa **bankkonton**, beställa **kort** och göra **insättningar**, **uttag**, **överföringar** och **fakturabetalningar**.
- **Bankpersonal** (Admin, BankManager, Teller, Auditor) hantera kunder och se alla konton, med olika behörigheter per roll.
- **Admin** skapa personalkonton med begränsade roller.
- Finansiella operationer utföras **idempotent** – om samma request skickas två gånger genomförs den bara en gång.
- Alla skrivoperationer **auditeras** och **loggas** automatiskt.

---

## Snabbstart

```bash
# 1. Klona repot
git clone https://github.com/b1-loop/NexaPay.git
cd NexaPay

# 2. Återställ paket
dotnet restore

# 3. Konfigurera connection string i appsettings.Development.json
#    (default: Server=localhost;Database=NexaPay;Trusted_Connection=True;)

# 4. Kör API:et (migrations + roller seedas automatiskt)
cd NexaPay.API
dotnet run

# 5. Öppna Swagger
#    http://localhost:5190/swagger
```

API:et startar på `http://localhost:5190` (HTTP) och `https://localhost:7206` (HTTPS).

---

## Tech stack

| Område | Teknik |
|---|---|
| Runtime | **.NET 8 SDK** |
| Webbramverk | **ASP.NET Core 8** |
| ORM | **Entity Framework Core 8** |
| Mediator | **MediatR 14** |
| Validering | **FluentValidation** |
| Object mapping | **AutoMapper 16** |
| Identity | **ASP.NET Core Identity** |
| Autentisering | **JWT Bearer (HS256)** |
| Cache / token-denylist | **Redis** (StackExchange.Redis) eller in-memory |
| API-versionering | **Asp.Versioning.Mvc 8** |
| Rate limiting | ASP.NET Core inbyggd (`RateLimiterMiddleware`) |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` |
| API-dokumentation | **Swagger** (Swashbuckle) + Postman-collection |
| Tester | **NUnit** + **FluentAssertions** + **Moq** |
| Test-host | `Microsoft.AspNetCore.Mvc.Testing` |
| Databas | **SQL Server** (prod) + EF Core InMemory (tester) |

**Totalt antal tester:** **218** (enhetstester + integrationstester).

---

## Arkitektur

NexaPay följer **Clean Architecture**. Beroenden pekar endast inåt – yttre lager beror på inre lager, aldrig tvärtom.

```
┌──────────────────────────────────────────────┐
│                NexaPay.API                   │  ← HTTP, Controllers, Middleware
│  ┌────────────────────────────────────────┐  │
│  │          NexaPay.Application           │  │  ← CQRS, Handlers, Validators, Behaviors
│  │  ┌──────────────────────────────────┐  │  │
│  │  │         NexaPay.Domain           │  │  │  ← Entiteter, Value Objects, Events
│  │  └──────────────────────────────────┘  │  │
│  └────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────┐  │
│  │        NexaPay.Infrastructure          │  │  ← EF Core, Repositories, JWT, Redis
│  └────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

- **Domain** har INGA externa NuGet-paket – det är ren C#-kod.
- **Application** beror endast på Domain. Definierar interface som Infrastructure implementerar.
- **Infrastructure** implementerar interfacen mot konkret teknik (SQL Server, Redis, ASP.NET Identity).
- **API** kopplar ihop allt och exponerar HTTP-endpoints.

Se även `DOMAIN_DIAGRAM.md` (UML klassdiagram) och `USER_FLOW.md` (sekvens- och dataflödesdiagram).

---

## Projektstruktur

```
NexaPay.sln
├── NexaPay.Domain/              – Domänlogik (ren C#, inga externa beroenden)
│   ├── Entities/                – Account, Card, Transaction, BaseEntity
│   ├── ValueObjects/            – Money (immutabel, valuta-säker)
│   ├── Enums/                   – AccountStatus, AccountType, CardStatus, Currency, TransactionType
│   ├── Events/                  – MoneyDeposited, MoneyWithdrawn, MoneyTransferred, CardBlocked, AccountClosed
│   ├── Interfaces/              – IAccountRepository, ICardRepository, ITransactionRepository, IUnitOfWork, IGenericRepository
│   ├── Exceptions/              – ConcurrencyException
│   └── Policy/                  – OcrPolicy (mod-10), TransactionPolicy (gränsvärden)
│
├── NexaPay.Application/         – CQRS-handlers, validators, behaviors, mappings
│   ├── DependencyInjection.cs   – AddApplication(): MediatR + AutoMapper + FluentValidation + behaviors
│   ├── Common/
│   │   ├── Behaviors/           – LoggingBehavior, ValidationBehavior, ConcurrencyRetryBehavior, AuditBehavior
│   │   ├── Constants/Roles.cs   – Rollnamn + kombinerade rollset
│   │   ├── EventHandlers/       – Reagerar på domain events (skickar mail, loggar)
│   │   ├── Interfaces/          – IAuthService, IAuditService, INotificationService, ITokenDenylist, IAppSettings
│   │   ├── Models/              – Result<T>, PagedResult<T>, IResult
│   │   └── Policies/            – StaffEmailPolicy (kräver @nexapay.com för personalroller)
│   ├── DTOs/                    – AccountDto, CardDto, TransactionDto, AuthDto, CreateCardResponse
│   ├── Features/                – Ett mappområde per resurs + Commands/Queries
│   │   ├── Accounts/            – CreateAccount, DeleteAccount, FreezeAccount, UnfreezeAccount, GetAccountById, GetAllAccounts, LookupAccountByNumber
│   │   ├── Auth/                – Register, Login
│   │   ├── Cards/               – CreateCard, ActivateCard, BlockCard, UnblockCard, GetCardsByAccount
│   │   └── Transactions/        – Deposit, Withdraw, Transfer, PayInvoice, GetTransactionsByAccount
│   └── Mappings/MappingProfile.cs
│
├── NexaPay.Infrastructure/      – Persistens + Identity + externa tjänster
│   ├── DependencyInjection.cs   – AddInfrastructure(): EF Core + repositories + JWT + Redis
│   ├── Settings/AppSettings.cs  – Läser konfiguration
│   ├── Identity/
│   │   ├── AuthService.cs       – Implementerar IAuthService med UserManager + RoleManager
│   │   ├── JwtService.cs        – Skapar JWT med claims (sub, jti, email, role)
│   │   ├── InMemoryTokenDenylist.cs
│   │   └── RedisTokenDenylist.cs
│   ├── Notifications/
│   │   ├── SmtpNotificationService.cs – Skickar mail via SMTP (Gmail)
│   │   └── LoggingNotificationService.cs – Loggar istället för att maila (test/dev)
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs    – Ärver IdentityDbContext
│   │   ├── EfAuditService.cs          – Skriver AuditLog-rader
│   │   ├── AuditLog.cs                – Audit-tabell
│   │   ├── Configurations/            – EF Fluent API per entitet
│   │   ├── Repositories/              – Generic Repository<T> + konkreta implementationer
│   │   └── UnitOfWork.cs              – SaveChanges + dispatch av domain events
│   └── Migrations/              – 11 EF Core-migrationer (SQL Server)
│
├── NexaPay.API/                 – HTTP-lagret
│   ├── Program.cs               – Minimal composition root
│   ├── ServiceExtensions.cs     – AddIdentityServices(), AddApiServices(), UseApiMiddleware()
│   ├── DatabaseExtensions.cs    – InitialiseDatabaseAsync() (migrations + seed)
│   ├── ApiResponse.cs           – Standardiserat svarsomslag
│   ├── Contracts/               – Request DTOs (record types)
│   ├── Controllers/             – AccountsController, AuthController, CardsController, TransactionsController, AdminController
│   ├── Extensions/              – ClaimsPrincipalExtensions, ResultExtensions
│   └── Middleware/              – ExceptionMiddleware (global felhantering)
│
├── NexaPay.Tests/               – 218 tester
│   ├── TestBase.cs              – Gemensam mock-setup
│   ├── Application/
│   │   ├── Behaviors/           – Tester på pipeline behaviors
│   │   ├── Features/            – Handler-tester per feature (alla CRUD)
│   │   └── Validators/          – FluentValidation-validator-tester
│   ├── Domain/                  – Account-, Money-, OcrPolicy-tester
│   ├── Infrastructure/          – AuthService-tester
│   └── Integration/             – End-to-end HTTP-tester via test-host
│
└── docs/
    └── NexaPay.postman_collection.json   – 29 endpoints, autosparar JWT
```

---

## Domänlager (Domain)

Domänlagret har **noll** externa NuGet-paket. All affärslogik lever här.

### BaseEntity

```csharp
abstract class BaseEntity
{
    Guid Id;
    DateTime CreatedAt;
    DateTime? UpdatedAt;

    private List<IDomainEvent> _domainEvents;  // intern lista
    IReadOnlyList<IDomainEvent> DomainEvents;  // skrivskyddad vy

    protected void RaiseDomainEvent(IDomainEvent e);  // bara subklasser
    public IReadOnlyList<IDomainEvent> PopDomainEvents();  // anropas av UnitOfWork
}
```

### Account (aggregat-rot)

```csharp
class Account : BaseEntity
{
    string AccountNumber;           // unikt index
    string AccountName;
    Money Balance;                  // privat-set, ändras bara via metoder
    AccountType AccountType;
    AccountStatus Status;           // Open | Frozen | Closed
    string OwnerId;                 // Identity-användarens id
    byte[] RowVersion;              // optimistisk concurrency

    static Account Open(...);                                            // fabriksmetod
    Transaction Deposit(Money, description, idempotencyKey?);            // raises MoneyDeposited
    Transaction Withdraw(Money, description, idempotencyKey?);           // raises MoneyWithdrawn
    Transaction PayInvoice(Money, bankgiro, ocr, ..., idempotencyKey?);  // raises MoneyWithdrawn
    (Transaction, Transaction) TransferTo(amount, ..., receiver, ...);   // raises MoneyTransferred
    void Freeze();
    void Unfreeze();
    void Close();                                                        // raises AccountClosed
}
```

### Money (Value Object)

```csharp
sealed class Money : IEquatable<Money>
{
    decimal Amount;       // alltid 2 decimaler (MidpointRounding.AwayFromZero)
    Currency Currency;    // SEK | EUR | USD

    static Money Zero(currency);
    +, -, >, <, >=, <=    // operatorer – kastar om valutorna är olika
}
```

Money förhindrar att vi blandar valutor: `100 SEK + 50 EUR` kastar `InvalidOperationException`.

### Card

```csharp
class Card : BaseEntity
{
    string CardToken;        // intern token (PAN sparas aldrig)
    string Last4Digits;      // sista 4 siffror (för UI)
    string CardHolderName;
    DateOnly ExpiryDate;
    CardStatus Status;       // Inactive | Active | Blocked | Expired

    void Activate();
    void Block();      // raises CardBlocked
    void Unblock();
    void MarkAsExpired();
}
```

### Transaction (oföränderlig)

Alla properties är `init` – en transaktion ändras aldrig efter att den skapats. Detta uppfyller bankregler om revisionsbarhet.

```csharp
class Transaction : BaseEntity
{
    Money Amount;
    TransactionType Type;             // Deposit | Withdrawal | Transfer | InvoicePayment
    string Description;
    Money BalanceAfterTransaction;    // saldo direkt EFTER händelsen
    Guid? ReceiverAccountId;          // bara för Transfer
    Guid AccountId;
    string? Bankgiro;                 // bara för InvoicePayment
    string? Ocr;                      // bara för InvoicePayment
    Guid? IdempotencyKey;             // filtrerat unikt index i SQL
}
```

---

## Applikationslager (Application)

### CQRS med MediatR

Varje operation är ett **Command** (skriv) eller en **Query** (läs). Controllers anropar aldrig repositories direkt – de skickar ett Command/Query via MediatR som routes till rätt Handler.

```
Controller → IMediator.Send(command) → Pipeline Behaviors → Handler → Repository → DB
```

### Pipeline Behaviors

Fyra behaviors körs i specifik ordning runt varje handler:

```
Request
  └─ 1. LoggingBehavior            (loggar request + tid)
       └─ 2. ValidationBehavior    (kör FluentValidation, kortsluter vid fel)
            └─ 3. ConcurrencyRetryBehavior (försöker igen ≤ 2 ggr vid ConcurrencyException)
                 └─ 4. AuditBehavior  (skriver audit-rad efter handler)
                      └─ Handler (faktisk affärslogik)
```

| Behavior | Funktion |
|---|---|
| `LoggingBehavior` | Loggar request + svarstid. Markerar requests med `ISensitiveRequest` (t.ex. LoginCommand) så att lösenord aldrig hamnar i loggar. Varnar vid > 500 ms. |
| `ValidationBehavior` | Kör alla `IValidator<TRequest>` parallellt. Vid fel kastas `ValidationException` som översätts till HTTP 400. |
| `ConcurrencyRetryBehavior` | Försöker köra handler igen vid `ConcurrencyException` (DB-collision). MaxRetries = 2 (3 totalt försök). |
| `AuditBehavior` | Skriver audit-rad för alla kommandon (inte queries). Körs sist – auditerar slutligt utfall. |

### Result-mönstret

Handlers returnerar `Result<T>` istället för att kasta undantag för förväntade fel:

```csharp
Result<T>.Success(value)        // IsSuccess = true
Result<T>.Failure(error)        // BusinessRule – 400
Result<T>.NotFound(error)       // NotFound – 404
```

`ResultExtensions.ToErrorResponse()` mappar resultat till rätt HTTP-status.

### Domain Events

Events publiceras EFTER lyckad `SaveChanges` – aldrig om transaktionen rullas tillbaka:

```
UnitOfWork.SaveChangesAsync()
  1. Samla in alla events från tracked entities
  2. DbContext.SaveChangesAsync()  ← DB-skrivning
  3. För varje event: IPublisher.Publish(event)  ← MediatR
  4. Rensa events-listan
```

| Event | Triggas av | Handler |
|---|---|---|
| `MoneyDeposited` | `Account.Deposit()` | Loggar + mailar ägaren |
| `MoneyWithdrawn` | `Account.Withdraw()`, `PayInvoice()` | Loggar + mailar ägaren |
| `MoneyTransferred` | `Account.TransferTo()` | Loggar + mailar avsändaren |
| `CardBlocked` | `Card.Block()` | Loggar (Warning) + mailar ägaren |
| `AccountClosed` | `Account.Close()` | Loggar + skickar avslutningsmejl |

---

## Infrastrukturlager (Infrastructure)

### Entity Framework Core

`ApplicationDbContext` ärver `IdentityDbContext<IdentityUser>` och innehåller:
- `DbSet<Account>` – alla konton
- `DbSet<Card>` – alla kort
- `DbSet<Transaction>` – immutable transaktionslogg
- `DbSet<AuditLog>` – audit-spår

**Globalt query-filter:** stängda konton döljs i alla queries om man inte explicit anropar `IgnoreQueryFilters()`.

**Owned types:** `Money` lagras som två kolumner (Amount + Currency) på samma rad som ägar-entiteten.

**Filtered unique index** på `Transactions.IdempotencyKey` – endast non-NULL värden är unika. Hjärtat i dubbla-POST-skyddet.

**Retry policy:** EF Core är konfigurerad med `EnableRetryOnFailure(3, 5s)` för transienta SQL-fel.

### Generic Repository

```csharp
abstract class Repository<T> : IGenericRepository<T>
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
}
```

`AccountRepository`, `CardRepository` och `TransactionRepository` ärver basen och lägger till entitet-specifika queries (t.ex. `GetByAccountNumberAsync`, `GetByIdempotencyKeyAsync`).

`Update` och `Remove` exponeras avsiktligt INTE generiskt – muteringar görs via intention-revealing metoder på aggregaten själva (`Account.Close()`, `Card.Block()`).

### UnitOfWork

```csharp
class UnitOfWork : IUnitOfWork
{
    IAccountRepository Accounts { get; }
    ICardRepository Cards { get; }
    ITransactionRepository Transactions { get; }

    Task<int> SaveChangesAsync(CancellationToken);
}
```

Samordnar alla repositories under en gemensam DbContext-transaktion. Hanterar både `DbUpdateConcurrencyException` (→ `ConcurrencyException`) och dispatch av domain events efter lyckad save.

### Identity och JWT

| Klass | Ansvar |
|---|---|
| `AuthService` | Implementerar `IAuthService` mot `UserManager` + `RoleManager`. Hanterar registrering, inloggning, e-postbekräftelse, lösenordsåterställning. |
| `JwtService` | Skapar HS256-tokens med claims: `sub` (userId), `jti` (token-id för revokering), `email`, `role`, `exp`. Default 24h livstid. |
| `InMemoryTokenDenylist` | ConcurrentDictionary + timer-rensning. Förlorar state vid omstart. |
| `RedisTokenDenylist` | Redis SET med TTL. Skalar över flera serverinstanser. Fail-open vid Redis-fel. |

`OnTokenValidated` i `JwtBearerEvents` kollar denylisten på varje request. Logout revoker token-id (jti).

### Notifications

`SmtpNotificationService` skickar verkliga mejl via SMTP (Gmail-default). Om SMTP inte är konfigurerat loggas bara en varning – appen kraschar inte. `LoggingNotificationService` används i tester.

---

## API-lager (API)

### Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();              // Application
builder.Services.AddInfrastructure(builder.Configuration);  // Infrastructure
builder.Services.AddIdentityServices();         // Identity + JWT scheme fix
builder.Services.AddApiServices(builder.Configuration);     // Controllers, Swagger, CORS, rate limiting

var app = builder.Build();
await app.InitialiseDatabaseAsync();            // migrations + role seed
app.UseApiMiddleware();
app.Run();
```

### Middleware-pipeline

```
1. Security headers          (X-Content-Type-Options, X-Frame-Options, Referrer-Policy)
2. ExceptionMiddleware       (global felhantering)
3. UseCors                   (FE-origins från config)
4. UseHttpsRedirection       (endast i Production)
5. UseRateLimiter            (429 innan auth)
6. UseAuthentication         (JWT-validering + denylist-koll)
7. UseAuthorization          ([Authorize(Roles=…)])
8. MapControllers
9. MapHealthChecks("/health")
```

### Rate Limiting

Två FixedWindow-policies. Gränserna läses från `RateLimiting`-sektionen i appsettings.

| Policy | Endpoints | Default | Dev |
|---|---|---|---|
| `"auth"` | AuthController | 5 req/min/IP | 100 / min |
| `"financial"` | Accounts/Cards/Transactions | 20 req/min/IP | 1000 / min |

Avvisade requests får `429 Too Many Requests`. `POST /api/auth/logout` har `[DisableRateLimiting]`.

### Health checks

`GET /health` – ingen autentisering (används av load balancers):

| Check | Implementation | Healthy |
|---|---|---|
| `database` | `AddDbContextCheck<ApplicationDbContext>` | `SELECT 1` lyckas |
| `redis` | `RedisHealthCheck` | `IConnectionMultiplexer.IsConnected` (eller "ej konfigurerat" = Healthy) |

### API-versionering

Alla controllers är taggade `[ApiVersion("1.0")]`. Version skickas via:
- Query string: `?api-version=1.0`
- Header: `X-API-Version: 1.0`
- Utelämnad: default 1.0

### ApiResponse

Standardiserat svarsomslag:

```json
{ "success": true, "message": "...", "data": { ... }, "timestamp": "..." }
```

Vid fel returneras `errors` som dictionary (vid valideringsfel) eller bara `message` (vid affärsregelbrott).

---

## Autentisering och behörighet

### Registreringsflöde

```
POST /api/auth/register  {email, password, role}
  → RegisterHandler
    → StaffEmailPolicy.Validate(email, role)    [@nexapay.com krävs för personal]
      → IAuthService.RegisterAsync()
        → UserManager.CreateAsync()
        → AddToRoleAsync()
        → SendEmailConfirmationAsync()           [mailar bekräftelselänk]
  → AuthDto { RequiresEmailConfirmation: true }
```

Användaren måste klicka i mejlet (`POST /api/auth/confirm-email`) innan första inloggning.

### Inloggningsflöde

```
POST /api/auth/login  {email, password}
  → LoginHandler → IAuthService.LoginAsync()
    → UserManager.FindByEmailAsync + CheckPasswordAsync
    → Lockout-check (5 misslyckade försök → 15 min låst)
    → EmailConfirmed-check
    → JwtService.GenerateToken(userId, email, role)
  → AuthDto { Token, Email, Role, ExpiresAt }
```

### Logout-flöde

```
POST /api/auth/logout  [Authorize]
  → Läser jti + exp från ClaimsPrincipal
  → ITokenDenylist.Revoke(jti, exp)
```

Alla framtida requests med samma token avvisas av `OnTokenValidated`.

### Lösenordsregler

Konfigurerade i `AddIdentity`:
- Minst 8 tecken
- Måste innehålla siffra, gemen, versal och specialtecken
- Unik e-postadress per användare
- 5 misslyckade inloggningar → 15 minuter låst konto

---

## Request-pipeline

Komplett exempel – `POST /api/transactions/deposit`:

```
HTTP Request
 │
 ├─ Security headers
 ├─ ExceptionMiddleware (wraps everything)
 ├─ UseCors
 ├─ UseRateLimiter → "financial"-policy (20/min/IP)
 ├─ UseAuthentication → validerar JWT + kollar denylist
 ├─ UseAuthorization → kollar [Authorize(Roles = CanWrite)]
 │
 └─ TransactionsController.Deposit()
     │  läser Idempotency-Key från header
     │  läser AccountId, Amount, Description från body
     │  läser userId + IsStaff från ClaimsPrincipal
     │
     └─ IMediator.Send(DepositCommand)
         │
         ├─ LoggingBehavior   (loggar request + tid)
         ├─ ValidationBehavior (kör DepositValidator)
         ├─ ConcurrencyRetryBehavior (wraps, redo att försöka igen)
         ├─ AuditBehavior      (väntar på handler, skriver audit)
         │
         └─ DepositHandler
             ├─ Idempotency-check (om key finns → returnera befintlig transaktion)
             ├─ Ladda Account
             ├─ Verifiera ägarskap (eller IsStaff)
             ├─ account.Deposit(Money, description, idempotencyKey)
             │   ├─ Status == Open?
             │   ├─ Balance += amount
             │   └─ RaiseDomainEvent(MoneyDeposited)
             ├─ Transactions.AddAsync(transaction)
             └─ UnitOfWork.SaveChangesAsync()
                 ├─ DbContext.SaveChangesAsync()  ← SQL-skrivning
                 └─ Publicera MoneyDeposited → MoneyDepositedHandler (mail)
         │
         └─ AuditBehavior skriver audit-rad
     │
     └─ Controller: return Ok(ApiResponse.Ok(result.Value))
```

---

## API-endpoints

Komplett lista (29 endpoints). Se Swagger eller `docs/NexaPay.postman_collection.json` för full dokumentation.

### Auth — `[EnableRateLimiting("auth")]`

| Metod | Path | Auth | Beskrivning |
|---|---|---|---|
| POST | `/api/auth/register` | – | Registrera User-konto |
| POST | `/api/auth/login` | – | Logga in, få JWT |
| POST | `/api/auth/logout` | Bearer | Revoka aktuell token |
| GET  | `/api/auth/me` | Bearer | Hämta inloggad användares profil |
| POST | `/api/auth/confirm-email` | – | Bekräfta e-post via mejllänk |
| POST | `/api/auth/forgot-password` | – | Begär lösenordsåterställning |
| POST | `/api/auth/reset-password` | – | Sätt nytt lösenord med token |
| POST | `/api/auth/change-password` | Bearer | Byt lösenord (inloggad) |

### Accounts — `[EnableRateLimiting("financial")]`

| Metod | Path | Roller | Beskrivning |
|---|---|---|---|
| GET | `/api/accounts` | Alla | Staff: alla konton, User: egna |
| GET | `/api/accounts/{id}` | Alla | Staff: alla, User: egna |
| GET | `/api/accounts/lookup?number={n}` | Alla | Slå upp konto via kontonummer |
| POST | `/api/accounts` | Admin, BankManager, Teller, User | Skapa konto |
| PUT | `/api/accounts/{id}/freeze` | Admin, BankManager, Teller, User | Frys konto |
| PUT | `/api/accounts/{id}/unfreeze` | Admin, BankManager, Teller, User | Avfrys konto |
| DELETE | `/api/accounts/{id}` | Admin, BankManager, User | Stäng konto (saldo måste vara 0) |

### Cards — `[EnableRateLimiting("financial")]`

| Metod | Path | Roller | Beskrivning |
|---|---|---|---|
| GET | `/api/cards/account/{accountId}` | Alla | Lista kort för konto |
| POST | `/api/cards` | Admin, BankManager, Teller, User | Skapa nytt kort |
| PUT | `/api/cards/{id}/activate` | Admin, BankManager, Teller, User | Aktivera kort |
| PUT | `/api/cards/{id}/block` | Admin, BankManager | Blockera kort |
| PUT | `/api/cards/{id}/unblock` | Admin, BankManager | Avblockera kort |

### Transactions — `[EnableRateLimiting("financial")]`

| Metod | Path | Roller | Beskrivning |
|---|---|---|---|
| GET | `/api/transactions/account/{id}?page=1&pageSize=20` | Alla | Paginerad historik |
| POST | `/api/transactions/deposit` | Admin, BankManager, Teller, User | Insättning |
| POST | `/api/transactions/withdraw` | Admin, BankManager, Teller, User | Uttag |
| POST | `/api/transactions/transfer` | Admin, BankManager, User | Överföring |
| POST | `/api/transactions/invoice-payment` | Admin, BankManager, Teller, User | Fakturabetalning |

### Admin — `[Authorize(Roles = "Admin")]`

| Metod | Path | Beskrivning |
|---|---|---|
| POST | `/api/admin/users` | Skapa användare med valfri roll |
| GET | `/api/admin/users` | Lista alla användare |
| DELETE | `/api/admin/users/{id}` | Ta bort användare |

### Health

| Metod | Path | Auth | Beskrivning |
|---|---|---|---|
| GET | `/health` | – | Status för database + Redis |

---

## Rollbaserad behörighet (RBAC)

| Roll | Se alla konton | Skapa konto | Skriv | Överför | Blockera kort | Admin-panel |
|---|---|---|---|---|---|---|
| **Admin** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **BankManager** | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ |
| **Teller** | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ |
| **Auditor** | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **User** | bara egna | ✓ | egna | egna | ✗ | ✗ |

Rollkonstanter i `NexaPay.Application/Common/Constants/Roles.cs`:

```csharp
Roles.Admin            = "Admin"
Roles.BankManager      = "BankManager"
Roles.Teller           = "Teller"
Roles.Auditor          = "Auditor"
Roles.User             = "User"

Roles.AllStaff         = "Admin,BankManager,Teller,Auditor"
Roles.CanWrite         = "Admin,BankManager,Teller,User"
Roles.CanTransfer      = "Admin,BankManager,User"
Roles.CanDelete        = "Admin,BankManager,User"
Roles.CanBlockCard     = "Admin,BankManager"
Roles.CanViewAllAccounts = "Admin,BankManager,Teller,Auditor"
```

**Personalroller** (`Admin`, `BankManager`, `Teller`, `Auditor`) kräver en e-postadress som matchar `StaffDomain` (default `nexapay.com`). Kontrolleras av `StaffEmailPolicy` vid registrering.

---

## Idempotens

Finansiella commands (Deposit, Withdraw, Transfer, PayInvoice) accepterar header:

```
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

Om en transaktion med samma key redan finns returnerar handlern den **ursprungliga** transaktionen utan att skapa en ny. Skyddet bygger på:

1. **Klientens UUID** i headern
2. **Filtrerat unikt index** i SQL: `WHERE IdempotencyKey IS NOT NULL`
3. **Handler-koll**: `await Transactions.GetByIdempotencyKeyAsync(key)` innan vi kallar aggregatet

Detta gör att klienten tryggt kan göra `retry` på nätverksfel utan att riskera dubbla insättningar.

---

## Databas och migrationer

### Tabeller

EF Core skapar:

| Tabell | Innehåll |
|---|---|
| `Accounts` | Bankkonton med RowVersion + ägar-FK |
| `Cards` | Kort knutna till Accounts |
| `Transactions` | Immutable transaktionslogg |
| `AuditLogs` | Audit-spår från AuditBehavior |
| `AspNet*` | ASP.NET Identity-tabeller (Users, Roles, UserRoles, etc.) |

11 migrationer finns i `NexaPay.Infrastructure/Migrations/`.

### Kör migrationer manuellt

```bash
# Skapa ny migration
dotnet ef migrations add MinMigration -p NexaPay.Infrastructure -s NexaPay.API

# Tillämpa
dotnet ef database update -p NexaPay.Infrastructure -s NexaPay.API

# Rulla tillbaka
dotnet ef database update FöregåendeMigration -p NexaPay.Infrastructure -s NexaPay.API
```

I `Development` körs migrationer automatiskt vid uppstart. I `Production` loggas en varning – migrationer bör då köras separat i deploy-pipelinen.

### Seedade data

Vid uppstart skapas:

- **5 roller**: Admin, BankManager, Teller, Auditor, User
- **5 dev-användare** (alla med lösenord `NexaPay1!` och `EmailConfirmed = true`):
  - `admin@nexapay.com` (Admin)
  - `bankmanager@nexapay.com` (BankManager)
  - `teller@nexapay.com` (Teller)
  - `auditor@nexapay.com` (Auditor)
  - `user@test.com` (User)

---

## Tester

**218 tester** över fyra kategorier (`dotnet test` – körs på cirka 10 sekunder):

### Enhetstester – Application (Handlers + Validators)

- Alla handlers för CRUD i Accounts, Cards, Transactions, Auth
- Validator-tester för alla commands
- Pipeline behavior-tester (ConcurrencyRetry)

### Enhetstester – Domain

- `AccountTests` – domäninvarianter (negativt saldo, fryst konto, stängt konto)
- `OcrPolicyTests` – mod-10/Luhn-validering

### Enhetstester – Infrastructure

- `AuthServiceTests` – Identity-flöden med mockade UserManager/RoleManager

### Integrationstester

- `AccountsIntegrationTests` – HTTP-CRUD via real test-host
- `AuthIntegrationTests` – Register/Login/Logout end-to-end
- `TransactionsIntegrationTests` – Deposit/Withdraw/Transfer/PayInvoice + idempotency
- `RateLimitingIntegrationTests` – verifierar 429-svar

```bash
# Kör alla tester
dotnet test

# Kör en specifik kategori
dotnet test --filter "Category=Domain"
dotnet test --filter "Category=Integration"
```

---

## Installation

### Förutsättningar

- **.NET 8 SDK** ([nedladdning](https://dotnet.microsoft.com/download/dotnet/8.0))
- **SQL Server** (lokal eller via Docker) — eller **SQL Server LocalDB** (Windows)
- **Redis** (valfritt — in-memory fallback om inte konfigurerat)

### 1. Klona och återställ

```bash
git clone https://github.com/b1-loop/NexaPay.git
cd NexaPay
dotnet restore
```

### 2. Konfigurera `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=NexaPay;Trusted_Connection=True;TrustServerCertificate=True",
    "Redis": ""
  },
  "Jwt": {
    "Key": "din-superhemliga-nyckel-minst-32-tecken-lång",
    "Issuer": "NexaPay",
    "Audience": "NexaPay",
    "ExpiryHours": 24
  },
  "StaffDomain": "nexapay.com",
  "FrontendUrl": "http://localhost:5173",
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

**Viktigt:** `Jwt:Key` MÅSTE vara minst 32 byte (256 bitar). Appen kraschar vid uppstart om den är för kort.

### 3. Kör API:et

```bash
cd NexaPay.API
dotnet run
```

Migrationer + roller + dev-användare seedas automatiskt. API:et startar på:

- **HTTP:** `http://localhost:5190`
- **HTTPS:** `https://localhost:7206`
- **Swagger:** `http://localhost:5190/swagger`
- **Health:** `http://localhost:5190/health`

### 4. Testa via Swagger

1. `POST /api/auth/login` med `{ "email": "admin@nexapay.com", "password": "NexaPay1!" }`
2. Kopiera `token` från svaret
3. Klicka 🔒 **Authorize** överst i Swagger-UI:t
4. Skriv `Bearer <din-token>` och klicka Authorize
5. Testa nu valfri skyddad endpoint!

### 5. Testa via Postman

Importera `docs/NexaPay.postman_collection.json`. Inställningar:
- Collection-variabler `baseUrl`, `token`, `accountId` etc.
- Login-anropet auto-sparar tokenen via test-script
- `Idempotency-Key` genereras automatiskt med `{{$guid}}`

### 6. Kör testerna

```bash
dotnet test
```

Förväntat: **218 passed, 0 failed**.

---

## Konfiguration

| Nyckel | Beskrivning | Krävs |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | Ja |
| `ConnectionStrings:Redis` | Redis connection (tom = in-memory) | Nej |
| `Jwt:Key` | HS256-nyckel, minst 32 byte | Ja |
| `Jwt:Issuer` | JWT issuer-claim | Ja |
| `Jwt:Audience` | JWT audience-claim | Ja |
| `Jwt:ExpiryHours` | Token-livstid i timmar (default 24) | Nej |
| `StaffDomain` | E-postdomän för personalroller (default `nexapay.com`) | Nej |
| `FrontendUrl` | URL till frontend (för konfirmations/reset-länkar) | Nej |
| `Cors:AllowedOrigins` | Lista över tillåtna CORS-origins | Nej (alla nekas om tom) |
| `RateLimiting:Auth:PermitLimit` | Auth-rate-limit per minut | Nej (default 5) |
| `RateLimiting:Financial:PermitLimit` | Financial-rate-limit per minut | Nej (default 20) |
| `Smtp:Host` | SMTP-server (default Gmail) | Nej |
| `Smtp:Port` | SMTP-port (default 587) | Nej |
| `Smtp:Username` | SMTP-användarnamn (e-postadress) | Nej |
| `Smtp:Password` | SMTP-app-lösenord | Nej |
| `Smtp:FromName` | Avsändarens visningsnamn | Nej |
| `AllowedHosts` | ASP.NET Core host-filtrering | Sätt i produktion |

### Produktionsnoteringar

- **Redis:** Sätt `ConnectionStrings:Redis` i miljövariabler eller secrets. Utan Redis fungerar inte tokenrevokering över flera serverinstanser.
- **AllowedHosts:** Sätt till din domän (t.ex. `api.nexapay.com`) för att förhindra host header-injection.
- **Migrationer:** Appen kör `MigrateAsync()` vid uppstart och loggar varning i Production. För horisontell skalning – ersätt med separat `dotnet ef database update` i deploy-pipelinen.
- **HSTS** aktiveras automatiskt utanför Development.

---

## Säkerhet

Sammanfattning av säkerhetsfunktioner som är inbyggda:

| Område | Skydd |
|---|---|
| Autentisering | JWT HS256, jti-revokering via denylist |
| Lösenord | Identity-hash, krav: 8+ tecken, gemen/versal/siffra/specialtecken |
| Brute force | Kontolåsning efter 5 misslyckade försök i 15 min |
| Rate limiting | 5/min på auth, 20/min på financial (per IP) |
| Användarnumeration | `ForgotPasswordAsync` returnerar alltid 200 OK |
| Behörighet | RBAC via `[Authorize(Roles=…)]` per endpoint + ägarskaps-check i handlers |
| Personalregistrering | StaffEmailPolicy (kräver `@nexapay.com` för personalroller) |
| HTTPS | UseHttpsRedirection + HSTS i Production |
| Säkerhetsheaders | X-Content-Type-Options, X-Frame-Options, Referrer-Policy |
| SQL injection | EF Core parametriserar alla queries |
| XSS | ASP.NET Core kodar JSON-output |
| CORS | Whitelist via `Cors:AllowedOrigins` |
| PAN/CVV | Lagras ALDRIG – endast Last4Digits + opaque CardToken |
| Validering | FluentValidation kortsluter ogiltiga requests innan handlern körs |
| Concurrency | Optimistisk via RowVersion + retry behavior |
| Audit | Alla kommandon loggas till AuditLogs-tabellen |
| Idempotency | Filtrerat unikt index på IdempotencyKey i Transactions |

---

## Bidra till projektet

Vi följer ett enkelt GitHub-flöde:

1. **Branch-namn:** `feature/<kort-beskrivning>` eller `fix/<kort-beskrivning>` eller `docs/<kort-beskrivning>`.
2. **Commit-meddelanden:** korta och i imperativ form (`Add Transfer endpoint`, inte `Added`).
3. **Pull request:** skapas mot `master`. Branch protection är aktivt – inga direkta push:ar tillåtna.
4. **CI-krav:** kör `dotnet build` och `dotnet test` lokalt före PR. Båda måste passera.
5. **GitHub Project Board:** https://github.com/users/b1-loop/projects/12 – knyt din PR till en issue.

### Kodstandard

- Public klasser/metoder har XML-/blockkommentarer eller filheader på svenska.
- En klass per fil.
- Nullable enabled i `.csproj` – inga onödiga null-warnings.
- `record`-typer för commands och requests (immutability).
- Inga magiska strängar – använd `Roles.*`-konstanter.
- Inga `throw new Exception(...)` – använd specifika typer eller Result-mönstret.

---

## Diagram och dokumentation

| Fil | Innehåll |
|---|---|
| `README.md` (denna fil) | Översikt + setup |
| `DOMAIN_DIAGRAM.md` | Mermaid UML-klassdiagram över domänen |
| `USER_FLOW.md` | Mermaid sekvens- och dataflödesdiagram |
| `APPLICATION_GUIDE.md` | Fördjupad guide till Application-lagret |
| `ARCHITECTURE_REVIEW.md` | Arkitektur-genomgång och beslutsmotiveringar |
| `CODEBASE_GUIDE.md` | Detaljerad mapp- och filgenomgång |
| `docs/NexaPay.postman_collection.json` | Postman-collection med 29 endpoints |

---

## Licens och författare

Skoluppgift – inte avsedd för produktionsbruk.

**Författare:**
- [@Haval-Jalal](https://github.com/Haval-Jalal)
- [@b1-loop](https://github.com/b1-loop) (Bozhidar N. Ivanov)

**Relaterade repos:**
- Frontend (React): https://github.com/Haval-Jalal/NexaPay-FE
