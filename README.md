# NexaPay

A production-grade banking API built with .NET 8 and Clean Architecture. NexaPay handles bank accounts, card management, and financial transactions — with full role-based access control, JWT authentication, idempotent operations, domain events, optimistic concurrency, and a 4-stage MediatR pipeline.

---

## Table of Contents

- [What NexaPay Does](#what-nexapay-does)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Domain Layer](#domain-layer)
- [Application Layer](#application-layer)
- [Infrastructure Layer](#infrastructure-layer)
- [API Layer](#api-layer)
- [Authentication & Authorization](#authentication--authorization)
- [Request Pipeline](#request-pipeline)
- [Domain Events](#domain-events)
- [API Endpoints](#api-endpoints)
- [Role-Based Access Control](#role-based-access-control)
- [Idempotency](#idempotency)
- [Testing](#testing)
- [Getting Started](#getting-started)
- [Configuration Reference](#configuration-reference)

---

## What NexaPay Does

NexaPay is a backend API for a bank. It allows:

- Customers to **register**, **log in**, create **bank accounts**, issue **cards**, and perform **deposits**, **withdrawals**, and **transfers**
- Bank **staff** (Admin, BankManager, Teller, Auditor) to manage customers and monitor all accounts
- **Admins** to create staff accounts with restricted roles
- Financial operations to be performed **idempotently** — sending the same request twice only executes once
- All write operations to be **audited** and **logged** automatically

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 |
| Web framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 |
| Mediator | MediatR 14 |
| Validation | FluentValidation |
| Mapping | AutoMapper 16 |
| Identity | ASP.NET Core Identity |
| Authentication | JWT Bearer (HS256) |
| Cache / Token denylist | Redis (StackExchange.Redis) or in-memory |
| API versioning | Asp.Versioning.Mvc 8 |
| Rate limiting | ASP.NET Core built-in (`RateLimiterMiddleware`) |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` |
| API docs | Swagger / Swashbuckle |
| Testing | NUnit + FluentAssertions + Moq |
| Test host | `Microsoft.AspNetCore.Mvc.Testing` |
| Database | SQL Server |

---

## Architecture

NexaPay follows **Clean Architecture**. Dependencies only point inward — outer layers depend on inner layers, never the reverse.

```
┌──────────────────────────────────────────────┐
│                  NexaPay.API                 │  ← HTTP, Controllers, Middleware
│  ┌────────────────────────────────────────┐  │
│  │          NexaPay.Application           │  │  ← CQRS Handlers, Validators, Behaviors
│  │  ┌──────────────────────────────────┐  │  │
│  │  │        NexaPay.Domain            │  │  │  ← Entities, Value Objects, Events
│  │  └──────────────────────────────────┘  │  │
│  └────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────┐  │
│  │        NexaPay.Infrastructure          │  │  ← EF Core, Repositories, JWT, Redis
│  └────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

**Domain** has no external NuGet dependencies — it is pure C#.  
**Application** depends only on Domain. It defines interfaces that Infrastructure implements.  
**Infrastructure** implements those interfaces with concrete technology (SQL Server, Redis, ASP.NET Identity).  
**API** wires everything together and exposes HTTP endpoints.

---

## Project Structure

```
NexaPay.sln
├── NexaPay.Domain/
│   ├── Entities/
│   │   ├── BaseEntity.cs             – Id, CreatedAt, UpdatedAt, domain events list
│   │   ├── Account.cs                – Core aggregate: Open(), Deposit(), Withdraw(), TransferTo(), Freeze(), Unfreeze(), Close()
│   │   ├── Card.cs                   – Activate(), Block(), MarkAsExpired()
│   │   └── Transaction.cs            – Immutable record of a financial operation
│   ├── ValueObjects/
│   │   └── Money.cs                  – Sealed, immutable, enforces same-currency arithmetic
│   ├── Enums/
│   │   ├── AccountStatus.cs          – Open | Frozen | Closed
│   │   ├── AccountType.cs            – Savings | Checking | ...
│   │   ├── CardStatus.cs             – Inactive | Active | Blocked | Expired
│   │   ├── Currency.cs               – SEK | EUR | USD
│   │   └── TransactionType.cs        – Deposit | Withdrawal | Transfer
│   ├── Events/
│   │   ├── IDomainEvent.cs           – Marker interface (: INotification)
│   │   ├── MoneyDeposited.cs
│   │   ├── MoneyWithdrawn.cs
│   │   ├── MoneyTransferred.cs
│   │   ├── CardBlocked.cs
│   │   └── AccountClosed.cs
│   ├── Interfaces/
│   │   ├── IAccountRepository.cs
│   │   ├── ICardRepository.cs
│   │   ├── ITransactionRepository.cs
│   │   └── IUnitOfWork.cs            – SaveChangesAsync() + DispatchDomainEventsAsync()
│   ├── Exceptions/
│   │   └── ConcurrencyException.cs   – Thrown by UnitOfWork on DbUpdateConcurrencyException
│   └── Policy/
│       └── TransactionPolicy.cs      – Max transfer limits, daily caps
│
├── NexaPay.Application/
│   ├── DependencyInjection.cs        – AddApplication(): MediatR, AutoMapper, FluentValidation, behaviors
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   ├── LoggingBehavior.cs         – Logs every request + elapsed time + slow-request warnings
│   │   │   ├── ValidationBehavior.cs      – Runs all FluentValidation validators, short-circuits on failure
│   │   │   ├── ConcurrencyRetryBehavior.cs – Catches ConcurrencyException, retries up to 2 times
│   │   │   └── AuditBehavior.cs           – Writes audit records for commands after they succeed
│   │   ├── Constants/
│   │   │   └── Roles.cs              – String constants: Admin, BankManager, Teller, Auditor, User + combined role sets
│   │   ├── EventHandlers/
│   │   │   ├── MoneyDepositedHandler.cs
│   │   │   ├── MoneyWithdrawnHandler.cs
│   │   │   ├── MoneyTransferredHandler.cs
│   │   │   ├── CardBlockedHandler.cs
│   │   │   └── AccountClosedHandler.cs
│   │   ├── Interfaces/
│   │   │   ├── IAuthService.cs       – RegisterAsync(), LoginAsync()
│   │   │   ├── IJwtService.cs        – GenerateToken()
│   │   │   ├── ITokenDenylist.cs     – Revoke(), IsRevoked()
│   │   │   └── IAppSettings.cs       – StaffDomain, JwtKey, ...
│   │   ├── Models/
│   │   │   ├── Result.cs             – Result<T>: IsSuccess, IsFailure, Value, Error
│   │   │   └── PagedResult.cs        – Items, TotalCount, Page, PageSize, TotalPages, HasNextPage, HasPreviousPage
│   │   └── Policies/
│   │       └── StaffEmailPolicy.cs   – IStaffEmailPolicy: Validate(email, role) — enforces @nexapay.com for staff roles
│   ├── DTOs/
│   │   ├── AccountDto.cs
│   │   ├── CardDto.cs
│   │   ├── TransactionDto.cs
│   │   ├── AuthDto.cs                – Token, Email, Role, ExpiresAt
│   │   └── CreateCardResponse.cs
│   ├── Features/
│   │   ├── Accounts/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateAccount/    – CreateAccountCommand + Handler + Validator
│   │   │   │   └── DeleteAccount/    – DeleteAccountCommand + Handler + Validator
│   │   │   └── Queries/
│   │   │       ├── GetAccountById/   – GetAccountByIdQuery + Handler
│   │   │       └── GetAllAccounts/   – GetAllAccountsQuery + Handler
│   │   ├── Auth/
│   │   │   └── Commands/
│   │   │       ├── Login/            – LoginCommand + Handler + Validator
│   │   │       └── Register/         – RegisterCommand + Handler + Validator (uses IStaffEmailPolicy)
│   │   ├── Cards/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateCard/       – CreateCardCommand + Handler + Validator
│   │   │   │   ├── ActivateCard/     – ActivateCardCommand + Handler
│   │   │   │   └── BlockCard/        – BlockCardCommand + Handler
│   │   │   └── Queries/
│   │   │       └── GetCardsByAccount/ – GetCardsByAccountQuery + Handler
│   │   └── Transactions/
│   │       ├── Commands/
│   │       │   ├── Deposit/          – DepositCommand + Handler + Validator (idempotency-aware)
│   │       │   ├── Withdraw/         – WithdrawCommand + Handler + Validator (idempotency-aware)
│   │       │   └── Transfer/         – TransferCommand + Handler + Validator (idempotency-aware)
│   │       └── Queries/
│   │           └── GetTransactionsByAccount/ – Paginated query
│   └── Mappings/
│       └── MappingProfile.cs         – AutoMapper profiles for all entity → DTO mappings
│
├── NexaPay.Infrastructure/
│   ├── DependencyInjection.cs        – AddInfrastructure(): EF Core, repositories, JWT, Redis, token denylist
│   ├── Settings/
│   │   └── AppSettings.cs            – Reads Jwt:Key, Jwt:Issuer, StaffDomain from configuration
│   ├── Identity/
│   │   ├── AuthService.cs            – Implements IAuthService using UserManager<IdentityUser> + RoleManager
│   │   ├── JwtService.cs             – Generates HS256 JWT with claims (sub, email, role, jti, exp)
│   │   ├── InMemoryTokenDenylist.cs  – Thread-safe ConcurrentDictionary, timer-based cleanup
│   │   └── RedisTokenDenylist.cs     – Redis SET with TTL matching token expiry
│   └── Persistence/
│       ├── ApplicationDbContext.cs   – Inherits IdentityDbContext<IdentityUser>, owns all entities
│       ├── UnitOfWork.cs             – Wraps SaveChangesAsync(), dispatches domain events via IPublisher
│       ├── Configurations/
│       │   ├── AccountConfiguration.cs    – RowVersion concurrency token, owned Money type, indexes
│       │   ├── CardConfiguration.cs       – CardNumber unique index
│       │   └── TransactionConfiguration.cs – IdempotencyKey filtered unique index
│       ├── Repositories/
│       │   ├── AccountRepository.cs
│       │   ├── CardRepository.cs
│       │   ├── TransactionRepository.cs
│       │   └── BaseRepository.cs     – Generic CRUD + AsNoTracking reads
│       └── Migrations/               – 9 EF Core migrations (SQL Server)
│
├── NexaPay.API/
│   ├── Program.cs                    – Composition root: AddApplication + AddInfrastructure + AddIdentityServices + AddApiServices
│   ├── ServiceExtensions.cs          – AddIdentityServices(), AddApiServices(), UseApiMiddleware()
│   ├── DatabaseExtensions.cs         – InitialiseDatabaseAsync(): migrations + role seeding
│   ├── ApiResponse.cs                – Envelope type: { success, message, data, errors }
│   ├── Contracts/                    – Request DTOs (one file per contract)
│   │   ├── CreateAccountRequest.cs
│   │   ├── CreateCardRequest.cs
│   │   ├── BlockCardRequest.cs
│   │   ├── DepositRequest.cs
│   │   ├── WithdrawRequest.cs
│   │   ├── TransferRequest.cs
│   │   ├── RegisterRequest.cs
│   │   ├── LoginRequest.cs
│   │   └── AdminCreateUserRequest.cs
│   ├── Controllers/
│   │   ├── AuthController.cs         – POST /register, /login, /logout
│   │   ├── AccountsController.cs     – GET /accounts, GET /accounts/{id}, POST /accounts, DELETE /accounts/{id}
│   │   ├── CardsController.cs        – GET /cards/account/{id}, POST /cards, PUT /cards/{id}/activate, PUT /cards/{id}/block
│   │   ├── TransactionsController.cs – GET /transactions/account/{id}, POST /deposit, /withdraw, /transfer
│   │   └── AdminController.cs        – POST /admin/users (Admin-only)
│   ├── Extensions/
│   │   ├── ClaimsPrincipalExtensions.cs – GetUserId(), IsStaff() helpers on ClaimsPrincipal
│   │   └── ResultExtensions.cs       – ToErrorResponse() maps Result errors to HTTP responses
│   └── Middleware/
│       └── ExceptionMiddleware.cs    – Global exception handler, converts unhandled exceptions to RFC 7807 problem details
│
└── NexaPay.Tests/
    ├── TestBase.cs
    ├── Application/
    │   ├── Behaviors/
    │   │   └── ConcurrencyRetryBehaviorTests.cs – 5 unit tests
    │   ├── Features/
    │   │   ├── Auth/RegisterHandlerTests.cs      – 5 unit tests (domain role restriction)
    │   │   ├── Accounts/                         – CreateAccount, DeleteAccount handler tests
    │   │   ├── Cards/                            – CreateCard, BlockCard handler tests
    │   │   └── Transactions/                     – Deposit, Withdraw, Transfer handler tests
    │   └── Validators/                           – FluentValidation validator tests
    ├── Domain/                                   – Money, Account, Card domain logic tests
    ├── Infrastructure/                           – Repository + UnitOfWork tests
    └── Integration/
        ├── NexaPayWebApplicationFactory.cs       – In-memory DB + role seeding
        ├── ApiIntegrationTestBase.cs             – Login helper, authenticated client
        ├── Accounts/                             – Full HTTP integration tests
        ├── Auth/                                 – Register/Login/Logout HTTP tests
        └── RateLimiting/
            └── RateLimitingIntegrationTests.cs   – Verifies 429 responses
```

---

## Domain Layer

The domain layer has **zero external NuGet dependencies**. All business rules live here.

### BaseEntity

```
BaseEntity
  Id          : Guid
  CreatedAt   : DateTime
  UpdatedAt   : DateTime?
  DomainEvents: List<IDomainEvent>   (private)

  RaiseDomainEvent(event) – adds to the private list
  ClearDomainEvents()     – called by UnitOfWork after dispatch
```

### Account (Aggregate Root)

Account is the central aggregate. It enforces all business rules around money:

```
Account : BaseEntity
  AccountNumber : string          (read-only after creation)
  AccountName   : string
  Balance       : Money           (private set — only changed via domain methods)
  AccountType   : AccountType
  Status        : AccountStatus   (Open | Frozen | Closed)
  OwnerId       : string          (Identity user ID)
  RowVersion    : byte[]          (optimistic concurrency token)

  Transactions  : ICollection<Transaction>
  Cards         : ICollection<Card>
```

**Factory:**
```csharp
Account.Open(accountNumber, accountName, accountType, ownerId, currency)
// Private constructor — ensures all accounts start with zero balance and Open status
```

**Domain methods (enforce invariants, raise events, return Transactions):**

| Method | Guard conditions | Event raised |
|---|---|---|
| `Deposit(amount, description, idempotencyKey?)` | Status must be Open | `MoneyDeposited` |
| `Withdraw(amount, description, idempotencyKey?)` | Status must be Open; sufficient balance | `MoneyWithdrawn` |
| `TransferTo(amount, description, receiver, idempotencyKey?)` | Both accounts Open; sufficient balance | `MoneyTransferred` |
| `Freeze()` | Not already Frozen or Closed | — |
| `Unfreeze()` | Must be Frozen | — |
| `Close()` | Not already Closed; balance must be zero | `AccountClosed` |

### Money (Value Object)

```csharp
sealed class Money : IEquatable<Money>
  Amount   : decimal   // always 2 decimal places (MidpointRounding.AwayFromZero)
  Currency : Currency  // SEK | EUR | USD

  Money.Zero(currency)   // factory for zero balance
  + - > < >= <=          // operators — throws if currencies differ
  ToString()             // "1234.56 SEK"
```

Money prevents mixing currencies at compile-time semantics: `100 SEK + 50 EUR` throws `InvalidOperationException`.

### Card

```
Card : BaseEntity
  CardNumber    : string      (unique)
  CardHolderName: string
  ExpiryDate    : DateTime
  Status        : CardStatus  (Inactive | Active | Blocked | Expired)
  AccountId     : Guid

  Activate()         – must be Inactive
  Block(reason)      – raises CardBlocked event
  MarkAsExpired()    – sets status to Expired
```

### Domain Events

All events implement `IDomainEvent : INotification` (MediatR). They are raised by domain methods and dispatched by `UnitOfWork` **after** a successful `SaveChangesAsync()`.

| Event | Raised by | Payload |
|---|---|---|
| `MoneyDeposited` | `Account.Deposit()` | AccountId, OwnerId, Amount, BalanceAfter, Timestamp |
| `MoneyWithdrawn` | `Account.Withdraw()` | AccountId, OwnerId, Amount, BalanceAfter, Timestamp |
| `MoneyTransferred` | `Account.TransferTo()` | FromAccountId, ToAccountId, OwnerId, Amount, Timestamp |
| `CardBlocked` | `Card.Block()` | CardId, AccountId, Reason, Timestamp |
| `AccountClosed` | `Account.Close()` | AccountId, OwnerId, Timestamp |

---

## Application Layer

### CQRS with MediatR

Every operation is a **Command** (write) or **Query** (read). Controllers never call repositories directly — they send a Command or Query through MediatR, which routes it to the correct Handler.

```
Controller → IMediator.Send(command) → Pipeline Behaviors → Handler → Repository
```

### Pipeline Behaviors (in order)

Behaviors wrap every MediatR request like nested middleware:

```
Request
  └─ LoggingBehavior          (1st)  logs request start + elapsed time
       └─ ValidationBehavior  (2nd)  runs FluentValidation, returns failure on error
            └─ ConcurrencyRetryBehavior (3rd)  catches ConcurrencyException, retries ≤ 2 times
                 └─ AuditBehavior    (4th)  writes audit record after command succeeds
                      └─ Handler     (actual business logic)
```

**LoggingBehavior**
- Logs request name and data with `ILogger`
- Implements `ISensitiveRequest` on requests like `LoginCommand` — data is suppressed, only the request name is logged
- Logs a `Warning` if elapsed time exceeds 500ms

**ValidationBehavior**
- Collects all `IValidator<TRequest>` from DI (registered by `AddValidatorsFromAssembly`)
- Runs all validators in parallel via `ValidateAsync`
- If any fail, returns `Result.Failure(errors)` without calling the handler

**ConcurrencyRetryBehavior**
- Catches `ConcurrencyException` (wraps `DbUpdateConcurrencyException`)
- Retries the handler up to `MaxRetries = 2` (3 total attempts)
- Re-throws on the 3rd failure
- Other exceptions pass through without retry

**AuditBehavior**
- Runs only on `ICommand` (not queries)
- After the handler returns success, writes an audit log entry

### Result Pattern

All handlers return `Result<T>` — never throw for business logic failures:

```csharp
Result<T>.Success(value)   // IsSuccess = true, Value = value
Result<T>.Failure(error)   // IsFailure = true, Error = error message
```

Controllers call `result.IsSuccess` and map to HTTP status codes via `ResultExtensions.ToErrorResponse()`.

### StaffEmailPolicy

Extracted from `RegisterHandler` into a named, testable policy:

```csharp
interface IStaffEmailPolicy
{
    string? Validate(string email, string role);
    // Returns null if allowed, error message if not
}
```

Rule: Any role other than `User` requires an `@nexapay.com` email address. This is enforced in `RegisterHandler` and tested independently.

### Validators

Every Command has a corresponding `FluentValidation` validator. Examples:

- `DepositCommandValidator` — AccountId not empty, Amount > 0 and ≤ daily limit
- `RegisterCommandValidator` — valid email format, password complexity
- `CreateAccountCommandValidator` — non-empty account name, valid AccountType

---

## Infrastructure Layer

### Entity Framework Core

`ApplicationDbContext` inherits `IdentityDbContext<IdentityUser>`. It owns:
- `DbSet<Account>`
- `DbSet<Card>`
- `DbSet<Transaction>`

**SQL Server retry policy** — configured with `EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: 5s)` for transient failures.

**Entity configurations** (via `IEntityTypeConfiguration<T>`):

- `AccountConfiguration` — configures `Money` as an owned type (stored as `Balance_Amount` + `Balance_Currency` columns), adds `RowVersion` as a concurrency token, unique index on `AccountNumber`
- `TransactionConfiguration` — filtered unique index on `IdempotencyKey` (only non-null values): `CREATE UNIQUE INDEX ... WHERE IdempotencyKey IS NOT NULL`
- `CardConfiguration` — unique index on `CardNumber`

### Unit of Work

```csharp
class UnitOfWork : IUnitOfWork
{
    Task SaveChangesAsync()
    // 1. Calls DbContext.SaveChangesAsync()
    // 2. Collects all domain events from all tracked entities
    // 3. Dispatches them via IPublisher (MediatR)
    // 4. Clears the events list on each entity
}
```

This ensures domain events are only dispatched after the database write succeeds.

### Identity & Authentication

**AuthService** (`IAuthService`) wraps ASP.NET Core Identity:
- `RegisterAsync(email, password, role)` — creates `IdentityUser`, assigns role
- `LoginAsync(email, password)` — validates credentials, returns JWT via `IJwtService`

**JwtService** generates tokens with claims:
- `sub` — user ID
- `email` — email address
- `role` — assigned role
- `jti` — unique token ID (used for denylist)
- `exp` — expiry (configurable, default 24h)

Signed with HS256. Key must be ≥ 32 bytes — enforced at startup with an `InvalidOperationException`.

**Token Denylist** — supports logout by revoking tokens before they expire:

| Implementation | When used | Persistence |
|---|---|---|
| `InMemoryTokenDenylist` | No Redis configured | Process memory — lost on restart |
| `RedisTokenDenylist` | `ConnectionStrings:Redis` set | Redis SET with TTL — survives restarts, works across multiple instances |

`OnTokenValidated` in `JwtBearerEvents` checks the denylist on every authenticated request.

### Repositories

All repositories inherit from `BaseRepository<T>`:

| Method | Notes |
|---|---|
| `GetByIdAsync(id)` | Tracked (for writes) |
| `GetAllAsync()` | Tracked |
| `GetByAccountNumberAsync(number)` | `AsNoTracking()` — read-only query |
| `AddAsync(entity)` | |
| `Remove(entity)` | |

---

## API Layer

### Program.cs — Startup Order

```csharp
// 1. Register services
builder.Services.AddApplication();           // MediatR, AutoMapper, FluentValidation, behaviors
builder.Services.AddInfrastructure(config);  // EF Core, repositories, JWT, Redis
builder.Services.AddIdentityServices();      // ASP.NET Identity + JWT scheme fix
builder.Services.AddApiServices(config);     // Controllers, Swagger, CORS, rate limiting, health checks

// 2. Build
var app = builder.Build();

// 3. Database
await app.InitialiseDatabaseAsync();         // Migrations + seed roles

// 4. Middleware pipeline
app.UseApiMiddleware();
```

### Middleware Pipeline (in order)

```
1. ExceptionMiddleware    – catches all unhandled exceptions → 500 problem details
2. UseHttpsRedirection    – redirects HTTP → HTTPS
3. UseCors               – CORS policy (origins from config)
4. UseRateLimiter         – 429 before auth to block unauthenticated hammering
5. UseAuthentication      – validates JWT, populates ClaimsPrincipal
6. UseAuthorization       – checks [Authorize] attributes and role requirements
7. MapControllers         – routes to controller actions
8. MapHealthChecks        – /health (no auth required)
```

### Rate Limiting

Two FixedWindow policies. Limits are read from the `RateLimiting` configuration section — `appsettings.json` holds the strict defaults, `appsettings.Development.json` overrides them with generous values so local testing isn't blocked.

| Policy | Applies to | Default (`appsettings.json`) | Development |
|---|---|---|---|
| `"auth"` | `AuthController` | 5 requests / minute / IP | 100 / minute / IP |
| `"financial"` | `AccountsController`, `CardsController`, `TransactionsController` | 20 requests / minute / IP | 1000 / minute / IP |

Rejected requests receive `429 Too Many Requests`.  
`POST /auth/logout` uses `[DisableRateLimiting]` — logout is always allowed.

### Health Checks

`GET /health` — no authentication required (used by load balancers):

| Check | Implementation | Healthy condition |
|---|---|---|
| `database` | `AddDbContextCheck<ApplicationDbContext>` | `SELECT 1` succeeds |
| `redis` | `RedisHealthCheck : IHealthCheck` | `IConnectionMultiplexer.IsConnected` = true (or "not configured" = Healthy) |

### API Versioning

All controllers are tagged `[ApiVersion("1.0")]`. Version is supplied via:
- Query string: `?api-version=1.0`
- Header: `X-API-Version: 1.0`
- Omitted: defaults to `1.0` (`AssumeDefaultVersionWhenUnspecified = true`)

### Contracts

Request bodies are defined as `record` types in `NexaPay.API/Contracts/` — one file per contract. This separates the API surface from the application commands.

### Extensions

**`ClaimsPrincipalExtensions`**
```csharp
User.GetUserId()  // reads "sub" claim → string
User.IsStaff()    // true if role is Admin, BankManager, Teller, or Auditor
```

**`ResultExtensions`**
```csharp
this.ToErrorResponse(result)
// Maps Result.Error → 404 Not Found (if "not found"), 403 Forbidden, or 400 Bad Request
```

---

## Authentication & Authorization

### Registration Flow

```
POST /api/auth/register
  └─ AuthController enforces Role = "User" only (staff roles rejected at API layer)
       └─ RegisterCommand → RegisterHandler
            └─ StaffEmailPolicy.Validate(email, role)  ← only User role reaches here, always passes
                 └─ IAuthService.RegisterAsync()
                      └─ UserManager.CreateAsync() + AddToRoleAsync()
                           └─ JwtService.GenerateToken()
                                └─ AuthDto { Token, Email, Role, ExpiresAt }
```

Staff accounts (Admin, BankManager, Teller, Auditor) are created exclusively via:
```
POST /api/admin/users   [Authorize(Roles = "Admin")]
```
The `StaffEmailPolicy` enforces that staff roles require an `@nexapay.com` email.

### Login Flow

```
POST /api/auth/login
  └─ LoginCommand → LoginHandler
       └─ IAuthService.LoginAsync()
            └─ UserManager.FindByEmailAsync() + CheckPasswordAsync()
                 └─ Lockout checked (5 failed attempts → 15-minute lockout)
                      └─ JwtService.GenerateToken()
                           └─ AuthDto { Token, ... }
```

### Logout Flow

```
POST /api/auth/logout  [Authorize]
  └─ AuthController reads "jti" + "exp" from ClaimsPrincipal
       └─ ITokenDenylist.Revoke(jti, expiry)
            └─ Token added to denylist (in-memory or Redis)
                 └─ All future requests with this token: OnTokenValidated → 401
```

---

## Request Pipeline

A complete example — `POST /api/transactions/deposit`:

```
HTTP Request
  │
  ├─ ExceptionMiddleware (wraps everything)
  ├─ UseRateLimiter → checks "financial" bucket (20/min/IP)
  ├─ UseAuthentication → validates JWT, checks denylist (OnTokenValidated)
  ├─ UseAuthorization → checks [Authorize(Roles = Roles.CanWrite)]
  │
  └─ TransactionsController.Deposit()
       │   reads AccountId, Amount, Description from body
       │   reads Idempotency-Key from header
       │   calls GetUserId() + IsStaff() from ClaimsPrincipal
       │
       └─ IMediator.Send(DepositCommand)
            │
            ├─ LoggingBehavior → logs "Handling DepositCommand" + request data
            ├─ ValidationBehavior → runs DepositCommandValidator (amount > 0, accountId valid)
            ├─ ConcurrencyRetryBehavior → wraps next, ready to retry on ConcurrencyException
            ├─ AuditBehavior → waits for handler result, then writes audit
            │
            └─ DepositHandler
                 ├─ Check idempotency key → if already exists, return existing transaction
                 ├─ Load Account from IAccountRepository
                 ├─ Verify ownership (or IsStaff)
                 ├─ account.Deposit(Money(amount, currency), description, idempotencyKey)
                 │    ├─ Guards: Status == Open
                 │    ├─ Balance += amount
                 │    └─ Raises MoneyDeposited event
                 ├─ IUnitOfWork.SaveChangesAsync()
                 │    ├─ DbContext.SaveChangesAsync() → SQL INSERT/UPDATE
                 │    └─ Dispatches MoneyDeposited → MoneyDepositedHandler (logs, notifications)
                 └─ Returns Result<TransactionDto>.Success(...)
            │
            └─ AuditBehavior writes audit record
       │
       └─ Controller: return Ok(ApiResponse.Ok(result.Value, "..."))
```

---

## Domain Events

Domain events are dispatched **after** the database save — they can never be published if the transaction fails.

```
UnitOfWork.SaveChangesAsync()
  1. DbContext.SaveChangesAsync()  ← database write commits
  2. foreach entity in ChangeTracker:
       foreach event in entity.DomainEvents:
         IPublisher.Publish(event)    ← MediatR dispatches to handler
  3. entity.ClearDomainEvents()
```

Each event has a corresponding handler in `NexaPay.Application/Common/EventHandlers/`. These handlers are the place to add notifications, webhook calls, email sending, or any side effects that should happen after a domain action.

---

## API Endpoints

### Auth — `[EnableRateLimiting("auth")]`  (5 req/min/IP)

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | — | Register as User |
| POST | `/api/auth/login` | — | Login, receive JWT |
| POST | `/api/auth/logout` | Bearer | Revoke current token |

### Accounts — `[EnableRateLimiting("financial")]`  (20 req/min/IP)

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | `/api/accounts` | All authenticated | Staff: all accounts. User: own accounts |
| GET | `/api/accounts/{id}` | All authenticated | Staff: any. User: own only |
| POST | `/api/accounts` | CanWrite (not Auditor) | Create account |
| DELETE | `/api/accounts/{id}` | Admin, BankManager, User | Close account (must have zero balance) |

### Cards — `[EnableRateLimiting("financial")]`

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | `/api/cards/account/{accountId}` | All authenticated | Get cards for account |
| POST | `/api/cards` | CanWrite (not Auditor) | Issue new card |
| PUT | `/api/cards/{id}/activate` | CanWrite (not Auditor) | Activate inactive card |
| PUT | `/api/cards/{id}/block` | Admin, BankManager | Block card with reason |

### Transactions — `[EnableRateLimiting("financial")]`

| Method | Path | Roles | Description |
|---|---|---|---|
| GET | `/api/transactions/account/{accountId}` | All authenticated | Paginated transaction list |
| POST | `/api/transactions/deposit` | CanWrite (not Auditor) | Deposit money |
| POST | `/api/transactions/withdraw` | CanWrite (not Auditor) | Withdraw money |
| POST | `/api/transactions/transfer` | Admin, BankManager, User | Transfer between accounts |

Query parameters for GET transactions: `?page=1&pageSize=20`

### Admin — `[Authorize(Roles = "Admin")]`

| Method | Path | Roles | Description |
|---|---|---|---|
| POST | `/api/admin/users` | Admin | Create user with any role |

---

## Role-Based Access Control

```
Role         Read All  Write  Transfer  Block Card  Admin
──────────────────────────────────────────────────────────
Admin          ✓        ✓        ✓          ✓         ✓
BankManager    ✓        ✓        ✓          ✓         ✗
Teller         ✓        ✓        ✗          ✗         ✗
Auditor        ✓        ✗        ✗          ✗         ✗
User           own      own      own        ✗         ✗
```

Role string constants with combined sets (used in `[Authorize(Roles = ...)]`):

```csharp
Roles.Admin           = "Admin"
Roles.BankManager     = "BankManager"
Roles.Teller          = "Teller"
Roles.Auditor         = "Auditor"
Roles.User            = "User"

Roles.CanWrite        = "Admin,BankManager,Teller,User"
Roles.CanTransfer     = "Admin,BankManager,User"
Roles.CanDelete       = "Admin,BankManager,User"
Roles.CanBlockCard    = "Admin,BankManager"
Roles.CanViewAllAccounts = "Admin,BankManager,Teller,Auditor"
Roles.AllStaff        = "Admin,BankManager,Teller,Auditor"
```

Staff roles (`Admin`, `BankManager`, `Teller`, `Auditor`) require a `@nexapay.com` email address and can only be created via `POST /api/admin/users`.

---

## Idempotency

Financial commands (Deposit, Withdraw, Transfer) accept an optional `Idempotency-Key` header:

```
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

If a request with the same key has already been processed for the same account, the handler returns the **original transaction result** without executing again. The key is stored as a filtered unique index in the `Transactions` table (only non-null values are indexed).

This means clients can safely retry failed requests without double-charging.

---

## Testing

**159 tests** across 4 categories:

### Unit Tests — Application

- **`RegisterHandlerTests`** (5 tests) — staff email policy enforced: external email + staff role denied, external email + User role allowed, all 4 staff roles require `@nexapay.com`
- **`ConcurrencyRetryBehaviorTests`** (5 tests) — retry once, retry twice, exceed MaxRetries throws, other exceptions not retried, success on first try
- Handler tests for all features (Deposit, Withdraw, Transfer, CreateAccount, DeleteAccount, CreateCard, BlockCard, ActivateCard)
- Validator tests

### Unit Tests — Domain

- `MoneyTests` — arithmetic, currency enforcement, equality, negative amounts rejected
- `AccountTests` — all domain method guard conditions
- `CardTests` — state machine transitions

### Integration Tests — Infrastructure

- Repository tests with real EF Core (in-memory provider)
- UnitOfWork event dispatch

### Integration Tests — API

`NexaPayWebApplicationFactory` creates a test host with:
- EF Core in-memory database
- All 5 roles seeded on startup
- Real middleware pipeline

`ApiIntegrationTestBase` provides `LoginAsync(role)` → `HttpClient` with `Authorization: Bearer ...` header set.

**RateLimitingIntegrationTests** — `RateLimitingWebApplicationFactory` overrides financial `PermitLimit` to 1. Each test gets a fresh factory via `[SetUp]`/`[TearDown]` so rate limit buckets don't bleed between tests.

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server (local or Docker)
- Redis (optional — in-memory fallback used if not configured)

### 1. Clone and restore

```bash
git clone <repo>
cd NexaPay
dotnet restore
```

### 2. Configure appsettings

Copy `appsettings.Development.json` and set:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=NexaPay;Trusted_Connection=True;",
    "Redis": ""
  },
  "Jwt": {
    "Key": "your-secret-key-minimum-32-characters-long",
    "Issuer": "NexaPay",
    "Audience": "NexaPay",
    "ExpiresInHours": 24
  },
  "StaffDomain": "nexapay.com",
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  }
}
```

`Jwt:Key` must be at least 32 bytes (256 bits). The application throws at startup if the key is too short.

### 3. Run

```bash
cd NexaPay.API
dotnet run
```

Database migrations run automatically on startup. The application seeds the 5 roles (`Admin`, `BankManager`, `Teller`, `Auditor`, `User`).

Swagger UI: `https://localhost:{port}/swagger`  
Health check: `https://localhost:{port}/health`

### 4. Test

```bash
dotnet test
```

### Quick start with Swagger

1. `POST /api/auth/register` with `{ "email": "user@gmail.com", "password": "Password1!", "role": "User" }`
2. `POST /api/auth/login` to get a JWT token
3. Click **Authorize** in Swagger UI, enter `Bearer {token}`
4. `POST /api/accounts` to create a bank account
5. `POST /api/transactions/deposit` to add money

To create a staff account: first create an Admin user directly in the database, then use `POST /api/admin/users`.

---

## Configuration Reference

| Key | Description | Required |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | Yes |
| `ConnectionStrings:Redis` | Redis connection string (empty = in-memory denylist) | No |
| `Jwt:Key` | HS256 signing key, min 32 bytes | Yes |
| `Jwt:Issuer` | JWT issuer claim | Yes |
| `Jwt:Audience` | JWT audience claim | Yes |
| `Jwt:ExpiresInHours` | Token lifetime in hours | Yes |
| `StaffDomain` | Domain required for staff roles (e.g. `nexapay.com`) | Yes |
| `Cors:AllowedOrigins` | Array of allowed CORS origins | No (all denied if empty) |
| `AllowedHosts` | ASP.NET Core host filtering | Set in production |

### Production notes

- **Redis**: Set `ConnectionStrings:Redis` in environment variables or secrets. Without it, token revocation (logout) does not survive restarts and does not work across multiple API instances.
- **`AllowedHosts`**: Set to your actual domain (e.g. `api.nexapay.com`) to prevent host header injection.
- **Database migrations**: The application runs `MigrateAsync()` on startup and logs a warning in Production. For horizontal scaling, replace with `dotnet ef database update` as a separate deploy step before starting instances.
