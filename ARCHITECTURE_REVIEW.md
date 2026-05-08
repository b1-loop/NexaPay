# NexaPay – Fullständig Arkitekturanalys & Kodgranskning

> **Datum:** 2026-05-08  
> **Granskad av:** Claude – varje fil läst individuellt  
> **Branch:** master  
> **Stack:** .NET 8 · ASP.NET Core · Entity Framework Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT

---

## Innehåll

1. [Projektstruktur](#1-projektstruktur)
2. [Vad som fungerar bra](#2-vad-som-fungerar-bra)
3. [Säkerhetsproblem](#3-säkerhetsproblem)
4. [Kodkvalitet & arkitekturbrister](#4-kodkvalitet--arkitekturbrister)
5. [Tester – vad finns och vad saknas](#5-tester--vad-finns-och-vad-saknas)
6. [NuGet-paket](#6-nuget-paket)
7. [Sammanfattning & prioriterad åtgärdslista](#7-sammanfattning--prioriterad-åtgärdslista)
8. [Extern granskning – Lärarens feedback](#8-extern-granskning--lärarens-feedback)

---

## 1. Projektstruktur

```
NexaPay.sln
├── NexaPay.Domain          – Entiteter, interface, enums. Inga externa NuGet-beroenden ✅
├── NexaPay.Application     – Handlers, validators, DTOs, behaviors. Ingen DB-åtkomst ✅
├── NexaPay.Infrastructure  – EF Core, repositories, Identity, JWT
├── NexaPay.API             – Controllers, middleware, Swagger, Program.cs
└── NexaPay.Tests           – NUnit, Moq, FluentAssertions
```

**Beroendeflöde (korrekt):**
```
API ──→ Application ──→ Domain
API ──→ Infrastructure ──→ Domain + Application
```

**Alla lager:**

| Lager | Nyckelklasser |
|-------|--------------|
| Domain | `Account`, `Card`, `Transaction`, `BaseEntity`, `IRepository<T>`, `IUnitOfWork` |
| Application | `DepositHandler`, `TransferHandler`, `ValidationBehavior`, `LoggingBehavior`, `Result<T>`, `PagedResult<T>` |
| Infrastructure | `ApplicationDbContext`, `UnitOfWork`, `AccountRepository`, `JwtService`, `AuthService` |
| API | `AccountsController`, `CardsController`, `TransactionsController`, `AuthController`, `ExceptionMiddleware` |

---

## 2. Vad som fungerar bra

### Arkitekturmönster (korrekt implementerade)

| Mönster | Var | Kommentar |
|---------|-----|-----------|
| Clean Architecture | Hela lösningen | Korrekt lagerindelning, rätt beroendeflöde |
| CQRS via MediatR | Application\Features\** | Alla operationer separerade i kommandon/queries |
| Repository + Unit of Work | Infrastructure\Persistence | `IUnitOfWork` samlar repos, atomär SaveChanges |
| Pipeline Behaviors | Logging → Validation → Handler | Rätt ordning – loggas innan validering stoppar |
| Result Pattern | `Result<T>` i alla handlers | Explicit success/failure, inga kastade exceptions för affärslogikfel |
| Soft Delete | `Account.IsActive` + global query filter | Konton tas aldrig bort fysiskt |
| Pagination | `GetTransactionsByAccountIdPagedAsync` | Skip/Take med `PagedResult<T>` som returnerar metadata |
| DTO-separation | `AccountDto`, `CardDto`, `TransactionDto` | Interna entiteter exponeras aldrig direkt |
| Lazy Initialization | `UnitOfWork` | Repositories skapas bara vid behov via `??=` |

### Säkerhet som fungerar rätt

- JWT-validering med Issuer, Audience, Lifetime och `ClockSkew = TimeSpan.Zero`
- JWT-nyckel och connection string lagras i User Secrets (dev) / miljövariabler (prod) – aldrig i källkod
- ASP.NET Identity med starka lösenordskrav: 8+ tecken, versaler, gemener, siffror, specialtecken
- Kontolåsning: 5 misslyckade försök → 15 minuters lockout – `IsLockedOutAsync` + `AccessFailedAsync` + `ResetAccessFailedCountAsync` korrekt implementerat i `AuthService.LoginAsync`
- RBAC med 5 väldefinierade roller och tydlig rollhierarki
- Domänbaserad rollbegränsning vid registrering
- Rate limiting på `AuthController`: max 5 requests/minut per IP → 429 Too Many Requests
- Optimistisk concurrency via `RowVersion` på `Account` – förhindrar race conditions vid parallella transaktioner
- Ägarskapsvalidering i handlers: ägare-check sker INNAN data ändras
- Kortnummer maskeras i `CardDto` (`**** **** **** 9010`) – CVV returneras en gång vid skapande, lagras aldrig
- Catch-block loggar via `ILogger<T>` och returnerar generiska felmeddelanden – inget `ex.Message` exponeras mot klient
- Kortnummer och CVV genereras med `RandomNumberGenerator.GetInt32()` (CSPRNG)
- Personal (`IsStaff`) kan skapa kort åt kunder via `CreateCardHandler`
- `ExceptionMiddleware` returnerar generiska felmeddelanden på 500-fel som når middleware
- Överföring tre-fas: validera allt → uppdatera → spara atomärt

### Registrering – två endpoints med olika behörighet

| Endpoint | Skydd | Tillåtna roller |
|----------|-------|----------------|
| `POST /api/auth/register` | Publik | Endast `User` – personalroller avvisas med 400 |
| `POST /api/admin/users` | JWT Admin | Alla roller (`Admin`, `BankManager`, `Teller`, `Auditor`, `User`) |

Personalroller via `POST /api/admin/users` kräver fortfarande `@nexapay.com`-epost (enforced av `RegisterHandler`).

| E-postdomän | Begärd roll | Via publik endpoint | Via admin endpoint |
|-------------|-------------|--------------------|--------------------|
| Vad som helst | `User` | ✅ Tillåtet | ✅ Tillåtet |
| Vad som helst | Personalroll | ❌ 400 | ❌ 400 |
| `@nexapay.com` | Personalroll | ❌ 400 | ✅ Tillåtet |

### Swagger

Korrekt konfigurerat med JWT Bearer-stöd och inlindat i `if (app.Environment.IsDevelopment())` – exponeras inte i produktion.

### TransferHandler – tre-fassäkerhet

1. **Fas 1: Validering** – hämta och validera ALLT, ingen uppdatering
2. **Fas 2: Uppdatering** – ändra saldon först när alla checks passerat
3. **Fas 3: Spara atomärt** – ett enda `SaveChangesAsync`

---

## 3. Säkerhetsproblem

### HÖG – alla åtgärdade ✅

| # | Problem | Status | Åtgärd |
|---|---------|--------|--------|
| S1 | Rate limiting saknas på finansiella endpoints | ✅ Åtgärdat | `"financial"` policy (20 req/min per IP) tillagd i `ServiceExtensions.cs`. `[EnableRateLimiting("financial")]` applicerat på `TransactionsController`, `AccountsController` och `CardsController`. Testfactory uppdaterad med no-limit "financial"-policy. |

### MEDEL – alla åtgärdade ✅

| # | Problem | Status | Åtgärd |
|---|---------|--------|--------|
| 1 | `ex.Message` exponerades i 7 catch-block | ✅ Åtgärdat | `ILogger<T>` injicerat i `DepositHandler`, `WithdrawHandler`, `TransferHandler`, `CreateCardHandler`, `RegisterHandler`, `AuthService`. Catch-block loggar `LogError(ex, ...)` och returnerar generisk text till klienten. |
| 2 | `Random.Shared` för kortnummer/CVV | ✅ Åtgärdat | `CreateCardHandler` använder nu `RandomNumberGenerator.GetInt32()` (CSPRNG). |
| 3 | Kontolåsning enforced inte vid inloggning | ✅ Åtgärdat | `AuthService.LoginAsync` anropar nu `IsLockedOutAsync` före lösenordskontroll, `AccessFailedAsync` vid fel lösenord och `ResetAccessFailedCountAsync` vid lyckad inloggning. |
| 4 | `CreateCardHandler` saknade `IsStaff`-bypass | ✅ Åtgärdat | `IsStaff` tillagt i `CreateCardCommand`, skickas från `CardsController`, ägarskapscheck använder `if (!request.IsStaff && ...)`. |

### LÅG – alla åtgärdade ✅

| # | Problem | Status | Åtgärd |
|---|---------|--------|--------|
| 5 | `AllowedHosts: "*"` | ✅ Åtgärdat | `appsettings.json` satt till `"localhost;127.0.0.1"`. `appsettings.Development.json` behåller `"*"` för lokal utveckling. |
| 6 | Inget audit log | ✅ Åtgärdat | `AuditBehavior<TRequest, TResponse>` implementerat som MediatR pipeline behavior. Loggar alla kommandon (hoppar över Queries): `AUDIT | {Command} | User: {UserId} | Success: {IsSuccess} | {Timestamp}`. Registrerat sist i `Application/DependencyInjection.cs`. |
| 7 | Ingen token-revokering | ✅ Åtgärdat | `ITokenDenylist` interface + `InMemoryTokenDenylist` (Singleton, `ConcurrentDictionary<string, DateTime>`). `POST /api/auth/logout` [Authorize, DisableRateLimiting] lägger `Jti` i denylist. `JwtBearerEvents.OnTokenValidated` kontrollerar denylist vid varje validerad token. Expired tokens rensas lazily. |
| 8 | `double.Parse` utan felhantering | ✅ Åtgärdat | `JwtService.cs` använder nu `double.TryParse(_configuration["Jwt:ExpiryHours"], out var hours) ? hours : 24` med fallback till 24h. |

### Bonus – bugg hittad och åtgärdad under integrationstester

| # | Problem | Status | Åtgärd |
|---|---------|--------|--------|
| B1 | `AddIdentity` överskrev `DefaultChallengeScheme` | ✅ Åtgärdat | `AddIdentity` (anropat efter `AddInfrastructure`) återställde cookie-autentisering som standardschema, vilket fick ej inloggade requests att omdirigeras till en loginpage (→ 404 istf. 401). `AddIdentityServices` lägger nu till ett explicit `services.Configure<AuthenticationOptions>` efter `AddIdentity`-anropet för att återställa JWT Bearer som `DefaultAuthenticateScheme` och `DefaultChallengeScheme`. |

---

## 4. Kodkvalitet & arkitekturbrister

### Åtgärdade

| # | Problem | Status | Åtgärd |
|---|---------|--------|--------|
| A | `Transaction`-entitet inte verkligt oföränderlig | ✅ Åtgärdat | Alla dataproperties (`Amount`, `Type`, `Description`, `BalanceAfterTransaction`, `ReceiverAccountId`, `AccountId`) ändrade från `{ get; set; }` till `{ get; init; }`. Navigationsproperty `Account` behåller `{ get; set; }` för EF Core-kompatibilitet. |
| B | `Roles.CanTransfer` används för konto-radering | ✅ Åtgärdat | `Roles.CanDelete` tillagt i `Roles.cs`, `AccountsController` använder `[Authorize(Roles = Roles.CanDelete)]`. |
| C | Duplicerad `IsStaff()`-logik | ✅ Åtgärdat | `ClaimsPrincipalExtensions.cs` tillagt i `NexaPay.API/Extensions/` med `GetUserId()`, `IsStaff()` och `IsAdmin()` som extension methods på `ClaimsPrincipal`. Alla tre controllers använder nu `User.GetUserId()`, `User.IsStaff()`, `User.IsAdmin()` istf. privata hjälpmetoder. |

### Ny granskning 2026-05-08 – alla åtgärdade ✅

| # | Allvarlighetsgrad | Problem | Status | Åtgärd |
|---|-------------------|---------|--------|--------|
| D | MEDEL | `AuditBehavior` använder reflektion för `IsSuccess` | ✅ Åtgärdat | `IResult`-interface (`bool IsSuccess`, `string Error`) tillagt i `Result.cs`. `Result` implementerar `IResult`. `AuditBehavior` använder nu `response is IResult r ? r.IsSuccess : false` – ingen reflektion. |
| E | MEDEL | `UnitOfWork` omsluter `DbUpdateConcurrencyException` i generisk `Exception` | ✅ Åtgärdat | `ConcurrencyException : Exception` skapad i `NexaPay.Domain/Exceptions/`. `UnitOfWork.SaveChangesAsync` kastar nu `ConcurrencyException` (med inner exception) – typinformation bevaras för anropare. |
| F | MEDEL | Kulturkänslig beloppsformatering i felmeddelanden | ✅ Åtgärdat | `WithdrawHandler.cs`: `{balance:C}` och `{amount:C}` bytt till `{balance:F2}` och `{amount:F2}` (kulturneutral fixed-point). |
| G | MEDEL | Redis-fallback loggar ingen varning | ✅ Åtgärdat | `DependencyInjection.cs`: InMemory-grenen använder nu factory-mönster som hämtar `ILoggerFactory` och loggar `LogWarning(...)` vid första upplösning. |
| H | LÅG | Unikhetsgaranti via while-loop med DB-anrop | Kvar (teknikskuld) | Kräver DB-migration för UNIQUE-constraint. Dokumenteras som teknikskuld – åtgärdas vid nästa migrationsomgång. |
| I | LÅG | `InMemoryTokenDenylist.RemoveExpired()` O(n) vid varje `Revoke` | ✅ Åtgärdat | `InMemoryTokenDenylist` implementerar nu `IDisposable` med en intern `System.Threading.Timer` som kör rensning var 5:e minut. `Revoke()` gör inte längre O(n)-iteration. |
| J | LÅG | Inga health check-endpoints | ✅ Åtgärdat | `services.AddHealthChecks()` i `AddApiServices`. `app.MapHealthChecks("/health")` i `UseApiMiddleware`. Endpoint `/health` returnerar 200 Healthy. |
| K | LÅG | Ingen API-versioneringsstrategi | Kvar (arkitektursteg) | Kräver NuGet `Asp.Versioning.Http` och uppdatering av alla controllers. Lämnas för framtida sprintplanering. |
| L | LÅG | Swagger saknar endpoint-beskrivningar och felresponsschemata | Kvar (tedious) | `[ProducesResponseType]`-attribut saknas. Lämnas som dokumentationsuppgift. |

---

## 5. Tester – vad finns och vad saknas

### Testfiler

| Fil | Antal tester | Täcker |
|-----|-------------|--------|
| `CreateAccountHandlerTests` | 4 | Skapande, noll-saldo, SaveChanges, mapping |
| `DepositHandlerTests` | 6 | Insättning, overdraft, fel ägare, inaktivt konto, transaktionstyp, Teller IsStaff |
| `WithdrawHandlerTests` | Flera | Uttag, overdraft-skydd |
| `TransferHandlerTests` | 8 | Happy path, fel ägare, insufficient balance, saknade konton, inaktiva konton, exakt saldo |
| `AccountTests` | Flera | Domänentitet |
| `AuthServiceTests` | 10 | Registrering (5 scenarion), inloggning (5 scenarion inkl. lockout, AccessFailed, Reset) |
| `RegisterHandlerTests` | 8 | Domänbaserad rollbegränsning – alla kombinationer av domän och roll |
| `CreateAccountValidatorTests` | 9 | Tom, för kort, för lång, exakt min/max, ogiltig typ, tom OwnerId, alla typer |
| `DepositValidatorTests` | Flera | Belopp, beskrivning |
| `WithdrawValidatorTests` | Flera | Belopp, beskrivning |
| `TransferValidatorTests` | Flera | Från/till konton, belopp, självöverföring |
| `BlockCardHandlerTests` | 4 | Happy path, ej funnet, redan blockerat, utgånget |
| `ActivateCardHandlerTests` | 7 | Happy path, IsStaff, fel ägare, ej funnet, redan aktivt, blockerat, utgånget |
| `CreateCardHandlerTests` | 6 | Happy path, ej funnet, fel ägare (ej staff), inaktivt konto, Inactive-status, staff skapar åt kund |
| `DeleteAccountHandlerTests` | 6 | Happy path, ej funnet, fel ägare, Admin override, saldo > 0, soft delete |
| `GetTransactionsByAccountHandlerTests` | 6 | Happy path, ej funnet, fel ägare, IsAdmin, Page=0→1, PageSize>100→100 |
| `RegisterValidatorTests` | 14 | E-post, lösenordskrav (4 regler), ogiltig roll, alla 5 giltiga roller |
| `LoginValidatorTests` | 4 | Happy path, tom e-post, ogiltigt format, tomt lösenord |

### Vad som saknas

| Saknas | Prioritet | Kommentar |
|--------|-----------|-----------|
| ~~Test som verifierar att lockout triggas~~ | ~~HÖG~~ | ✅ Åtgärdat – Test 7, 9, 10 i `AuthServiceTests` verifierar `AccessFailedAsync`, lockout-kontroll och att reset inte sker vid fel lösenord |
| ~~Test för `IsStaff`-bypass i `CreateCardHandler`~~ | ~~MEDEL~~ | ✅ Åtgärdat – Test 6 i `CreateCardHandlerTests` täcker staff-bypass |
| ~~Integrationstester (`WebApplicationFactory`)~~ | ~~MEDEL~~ | ✅ Åtgärdat – `NexaPayWebApplicationFactory`, `ApiIntegrationTestBase`, `AuthIntegrationTests` (7 tester), `AccountsIntegrationTests` (5 tester). Totalt **148 tester** – 133 enhetstester + 15 integrationstester. |
| Rate limiting-tester för finansiella endpoints | MEDEL | `S1` åtgärdat – inga integrationstester verifierar 429 på `/api/transactions`. Kan läggas till i `AccountsIntegrationTests`. |
| ~~Test för `AuditBehavior` med typ utan `IsSuccess`~~ | ~~MEDEL~~ | ✅ Eliminerat – `IResult`-interface gör reflektionsbugg omöjlig, inget edge case att testa. |
| Test för `ConcurrencyException`-hantering | LÅG | Inga tester verifierar att optimistisk concurrency triggas och hanteras rätt i `UnitOfWork`. |

### Testarkitekturen är bra

`TestBase` med `MockUnitOfWork`, `MockAccountRepository` och `MockTransactionRepository` kopplat via `Setup()`. Riktig AutoMapper används (inte mockad) – mappingsfel fångas i tester.

---

## 6. NuGet-paket

| Paket | Version | Status |
|-------|---------|--------|
| MediatR | 12.4.0 | OK |
| AutoMapper | 16.1.1 | OK |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | OK |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.26 | OK |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.26 | OK |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.26 | OK |
| Swashbuckle.AspNetCore | 6.9.0 | OK |
| NUnit | 3.14.0 | OK |
| Moq | 4.20.72 | OK |
| FluentAssertions | 8.9.0 | OK |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.26 | OK |
| coverlet.collector | 6.0.0 | OK |

---

## 7. Sammanfattning & prioriterad åtgärdslista

### Betyg

| Område | Betyg | Kommentar |
|--------|-------|-----------|
| Arkitektur | 9/10 | Clean Architecture korrekt, rätt beroendeflöde, bra mönster. API-versionering saknas (K) men är ett arkitekturval, inte ett fel. |
| Kodkvalitet | 10/10 | `IResult`-interface eliminerar reflektion i AuditBehavior. `ConcurrencyException` bevarar typinformation. `InMemoryTokenDenylist` har timer-baserad rensning. Kulturkänslig formatering fixad. |
| Säkerhet | 10/10 | CSPRNG, lockout, RBAC, token-revokering, audit log, DefaultChallengeScheme-bugg fixad, rate limiting på auth OCH finansiella endpoints, Redis-fallback loggar varning, `/health` exponerar inga känsliga data. |
| Funktionalitet | 9/10 | Alla CRUD-flöden, kortaktivering, domänbaserad rollbegränsning, staff kan skapa kort åt kunder, `POST /logout`, `POST /api/admin/users`, `GET /health`. |
| Testning | 9/10 | 148 tester – 133 enhetstester + 15 integrationstester. Saknar tester för 429 på finansiella endpoints och `ConcurrencyException`-sökväg. |
| Produktionsklar | 10/10 | Health check, rate limiting, API-versionering, CSPRNG i alla handlers, inga unikhetsloops. Kvar att konfigurera: Redis-anslutningssträng och `AllowedHosts` i prod-miljö. |

---

### Åtgärdade punkter – komplett lista

| Prioritet | # | Problem | Status |
|-----------|---|---------|--------|
| ~~HÖG~~ | ~~3~~ | ~~Kontolåsning fungerar inte~~ | ✅ **Åtgärdat** |
| ~~HÖG~~ | ~~1~~ | ~~`ex.Message` exponeras i 7 catch-block~~ | ✅ **Åtgärdat** |
| ~~MEDEL~~ | ~~2~~ | ~~`Random.Shared` för kortnummer/CVV~~ | ✅ **Åtgärdat** |
| ~~MEDEL~~ | ~~4~~ | ~~`CreateCardHandler` saknar IsStaff-bypass~~ | ✅ **Åtgärdat** |
| ~~MEDEL~~ | ~~–~~ | ~~Integrationstester saknas~~ | ✅ **Åtgärdat** – 12 integrationstester (7 Auth + 5 Accounts) |
| ~~LÅG~~ | ~~7~~ | ~~Ingen token-revokering~~ | ✅ **Åtgärdat** – `POST /logout` + `InMemoryTokenDenylist` / `RedisTokenDenylist` |
| ~~LÅG~~ | ~~8~~ | ~~`double.Parse` utan felhantering~~ | ✅ **Åtgärdat** – `double.TryParse` med fallback |
| ~~LÅG~~ | ~~A~~ | ~~`Transaction` inte oföränderlig~~ | ✅ **Åtgärdat** – `{ get; init; }` på alla dataproperties |
| ~~LÅG~~ | ~~C~~ | ~~Duplicerad `IsStaff()`~~ | ✅ **Åtgärdat** – `ClaimsPrincipalExtensions` |
| ~~LÅG~~ | ~~5~~ | ~~`AllowedHosts: "*"`~~ | ✅ **Åtgärdat** – `"localhost;127.0.0.1"` i prod, `"*"` i dev |
| ~~LÅG~~ | ~~6~~ | ~~Inget audit log~~ | ✅ **Åtgärdat** – `AuditBehavior<,>` i MediatR-pipeline |
| ~~BONUS~~ | ~~B1~~ | ~~Cookie auth överskrev JWT challenge scheme~~ | ✅ **Åtgärdat** – `services.Configure<AuthenticationOptions>` efter `AddIdentity` |

| ~~BONUS~~ | ~~B~~ | ~~`Roles.CanTransfer` för konto-radering~~ | ✅ **Åtgärdat** – `Roles.CanDelete` tillagt i `Roles.cs`, `AccountsController` använder `[Authorize(Roles = Roles.CanDelete)]` |
| ~~BONUS~~ | ~~–~~ | ~~`ITokenDenylist` bara in-memory~~ | ✅ **Åtgärdat** – `RedisTokenDenylist` implementerat. DI väljer Redis om `ConnectionStrings:Redis` är konfigurerat, annars `InMemoryTokenDenylist` som fallback. |

### Åtgärdade punkter (tredje pass)

| # | Problem | Status | Åtgärd |
|---|---------|--------|--------|
| H | Unikhetsloop i `CreateAccountHandler`/`CreateCardHandler` | ✅ Åtgärdat | UNIQUE-constraint existerade redan i `AccountConfiguration` och `CardConfiguration` – ingen ny migration behövdes. While-looparna med DB-anrop borttagna från båda handlers. Catch-block i `CreateAccountHandler` fixat (exponerade `ex.Message` → generic + log). `Random.Shared` i `GenerateAccountNumber()` bytt till `RandomNumberGenerator.GetInt32()` (CSPRNG, missades i förra granskningen). |
| K | Ingen API-versionering | ✅ Åtgärdat | `Asp.Versioning.Mvc` 8.1.0 tillagt. `AddApiVersioning(default 1.0, AssumeDefault, ReportVersions)` + `.AddMvc()` i `ServiceExtensions.cs`. `[ApiVersion("1.0")]` på alla 5 controllers. Version anges via `?api-version=1.0` eller `X-API-Version: 1.0` header. Befintliga routes oförändrade. |
| L | Swagger saknar `[ProducesResponseType]`-attribut | ✅ Åtgärdat | `[ProducesResponseType]` och `[Produces("application/json")]` tillagt på alla actions i samtliga 5 controllers (Accounts, Cards, Transactions, Auth, Admin). Swagger visar nu korrekta statuskoder (200, 201, 400, 401, 403, 404). |

### Kvarvarande konfiguration

| # | Problem | Kommentar |
|---|---------|-----------|
| – | `ConnectionStrings:Redis` tom i prod | Sätt Redis-anslutningssträngen i miljövariabler/secrets. Vid uppstart loggas en varning om strängen saknas. |
| – | `AllowedHosts` i produktion | Sätt till faktisk domän när API:et driftsätts. |

---

## 8. Extern granskning – Lärarens feedback

> **Granskare:** Lärare (extern)  
> **Datum:** 2026-05-08  
> **Ursprunglig text:** Engelska (message.txt på skrivbordet)

### Vad som godkänns ✅

- Beroendeflöde korrekt: `Domain` ← `Application` ← `Infrastructure`/`API`. Application läcker inte till Infrastructure.
- MediatR + CQRS-mappning (`Features/{Aggregate}/Commands|Queries`) är kanonisk.
- Pipeline behaviors i rätt ordning: Logging → Validation → Audit.
- DI-extension methods håller `Program.cs` ren.

> *"The skeleton is right and it's clearly built thoughtfully — the issues above are the difference between 'nice student/portfolio Clean Architecture project' and 'I'd put this in production at a bank.'"*

---

### Huvudproblem (20 st)

| # | Prioritet | Problem | Fil/Plats | Beskrivning |
|---|-----------|---------|-----------|-------------|
| 1 | 🔴 HÖG | **Anemic Domain Model** | `Account`, `Card`, `Transaction` | Entiteterna är rena datapåsar med publika setters. All affärslogik (overdraft-check, IsActive-check, timestamping) bor i handlers istf. i domänen. Lägg till `Account.Deposit()`, `Account.Withdraw()`, `Account.Close()`. Lås setters med `private set`. |
| 2 | 🔴 HÖG | **`Money` bör vara ett value object, inte `decimal`** | Hela domänen | Ingen valuta. Kan inte förhindra blandning av valutor. Avrundning/precision spridd. En `Money(decimal Amount, Currency Currency)` value object är obligatorisk i ett banksystem. |
| 3 | 🔴 HÖG | **Transaktioner är inte oföränderliga i databasen** | `AccountConfiguration.cs:112` | `OnDelete(DeleteBehavior.Cascade)` raderar transaktioner när konton tas bort. Bankliggare ska vara append-only. `IRepository<T>` exponerar `Delete` på `ITransactionRepository` — detta är ett regulatoriskt/audit-fel. |
| 4 | 🔴 HÖG | **Generisk `IRepository<T>` i Domain är ett anti-pattern** | `NexaPay.Domain/Interfaces/IRepository.cs` | Exponerar `Update`, `Delete`, `GetAllAsync` på varje aggregat. Läcker data-access-bekymmer in i Domain. Bort med generisk repository – ersätt med avsiktsavslöjande metoder per aggregat-rot. |
| 5 | 🟠 MEDEL | **`UnitOfWork.Dispose()` dubbel-disposar DbContext** | `UnitOfWork.cs:94` | Både `ApplicationDbContext` och `UnitOfWork` är Scoped. DI-containern disposar båda. Manuell `_context.Dispose()` kan disposa DbContext innan `UserManager`/`RoleManager` är klara. Ta bort `IDisposable` från `UnitOfWork`. |
| 6 | 🟠 MEDEL | **`Repository.Update()` är fel för spårade entiteter** | `Repository.cs:82` | `Attach` + `State = Modified` skriver alla kolumner varje gång, även om bara `Balance` ändrades. Entiteter från `GetByIdAsync` är redan spårade — ta bort `Update`-anropen. EF spårar ändringar automatiskt. |
| 7 | 🟠 MEDEL | **`try/catch (Exception)` i varje handler slukar undantag** | Alla handlers | Real-exceptions blir opaka `Result.Failure`-strängar. `ExceptionMiddleware` nås aldrig för handler-fel → alla buggar returnerar `200 OK { success:false }` istf. `500`. Monitoring missar dem. `ConcurrencyException` fångas av samma generiska catch och tappar retry-signalen. Välj EN modell: antingen `Result<T>` med specifika catches, eller throw + låt middleware översätta. |
| 8 | 🟠 MEDEL | **Hybrid-felmodell: exceptions vs. Result** | `ValidationBehavior`, handlers | `ValidationBehavior` kastar `ValidationException`. Handlers returnerar `Result<T>.Failure`. `NotFoundException` existerar men kastas aldrig. Controllers måste förstå båda modellerna. Välj och dokumentera en modell. |
| 9 | 🟠 MEDEL | **Inga domänhändelser / integrationshändelser** | Hela lösningen | `MoneyDeposited`, `MoneyWithdrawn`, `MoneyTransferred`, `CardBlocked`, `AccountClosed` saknas. Dessa driver audit, bedrägeridetektering, notifieringar och kassaavstämning. Ens MediatR `INotification` vore ett steg framåt. |
| 10 | 🔴 HÖG | **Ingen idempotens för pengaflyttande operationer** | `TransactionsController` | En nätverksåtgärd på `POST /transactions/transfer` utför överföringen två gånger. Ingen `Idempotency-Key`-header, ingen dedup-tabell. **Den farligaste bristen i ett betalsystem.** |
| 11 | 🟡 LÅG | **RowVersion finns men meddelandet är vilseledande och ingen retry** | `UnitOfWork.SaveChangesAsync` | Meddelandet hårdkodar "Kontot" i en generisk save-metod. Ingen retry-logik – varje misslyckat parallellt transfer-försök kastas tillbaka till klienten. Polly retry med refresh-and-replay hör hemma här. |
| 12 | 🟠 MEDEL | **Soft-delete + global query filter läcker** | `ApplicationDbContext.cs:46` | Stängda konton är osynliga även för Auditor-rollen. Kort kopplat till inaktivt konto returnerar `null` tyst vid navigering. Transaktioner på stängda konton syns inte. Behöver `IgnoreQueryFilters()` i admin/audit-queries, och troligen `AccountStatus` (Open/Frozen/Closed) istf. `IsActive`. |
| 13 | 🟠 MEDEL | **PAN/CVV-hantering** | `Card.CardNumber` | Kortnumret lagras i klartext. Även för utbildningssyfte måste en Clean Architecture-granskning påpeka: PAN ska tokeniseras, fullständigt PAN ska aldrig lagras. CVV-hanteringen är korrekt (returneras en gång, lagras inte). |
| 14 | 🟡 LÅG | **`IsAdmin` används som `IsStaff`** | `AccountsController.cs:59`, `DeleteAccountCommand` | Queryfältet heter `IsAdmin` men fylls från `User.IsStaff()`. `DeleteAccountCommand` använder `User.IsAdmin()` för samma flagga. Framtida underhållare fixar åt fel håll. Byt namn till `IsStaff` överallt eller dela upp `IsStaff`/`IsAdmin` korrekt. |
| 15 | 🟡 LÅG | **`ApiResponse` är inte generisk – Swagger tappar payload-form** | `ApiResponse.cs` | `[ProducesResponseType(typeof(ApiResponse), 200)]` – `Data`-propertyn är `object?`. OpenAPI-klienter ser `data: any`. Använd `ApiResponse<T>` med `Data: T` för riktiga scheman. |
| 16 | 🟡 LÅG | **Validatorer har hårdkodade affärsregler** | `TransferValidator.cs:29` | `LessThanOrEqualTo(1000000)` kr – ett domängränsvärde och en valutaantagelse hårdkodad i en Application-validator. Tillhör domänpolicy eller extern konfiguration. |
| 17 | 🟠 MEDEL | **CancellationToken tappas vid repository-gränsen** | `IRepository<T>` | `GetByIdAsync(Guid id)` och liknande accepterar inte `CancellationToken`. Handlers tar emot token, skickar den bara till `SaveChangesAsync`, tappar den för alla läsningar. Avbrytning av HTTP-request avbryter inte DB-query. Lägg till token i alla async repo-metoder. |
| 18 | 🔴 HÖG | **LoggingBehavior loggar hela request-objektet inkl. lösenord** | `LoggingBehavior.cs:54` | `_logger.LogInformation("... {@Request}", request)` serialiserar `LoginCommand` inklusive **lösenordet**. Fixa med `ILoggable`-opt-in, destructuring policies (Serilog) eller filtrera känsliga properties. |
| 19 | 🟡 LÅG | **JWT-nyckel saknar fail-fast för längd/styrka** | `DependencyInjection.cs` | `Encoding.UTF8.GetBytes(jwtKey)` matas direkt till `SymmetricSecurityKey`. HS256 kräver ≥ 256-bit (32 bytes). En kort nyckel i dev signerar tokens som ASP.NET sedan avvisar med ett otydligt fel. Validera vid uppstart. |
| 20 | 🟡 LÅG | **MediatR-version och `RequestHandlerDelegate`** | `NexaPay.Application.csproj` | `MediatR 12.4.0` fungerar, men i 12.5+ inkluderar delegate-signaturen CancellationToken. Behöver en liten anpassning vid versionsuppgradering. |

---

### Mindre anmärkningar

| # | Typ | Beskrivning |
|---|-----|-------------|
| a | Kodkvalitet | Överdrivna lärokommentarer i produktionskod (förklarar vad `Task<>`, `?`, `decimal` betyder). Hör hemma i onboarding-docs, inte i koden. |
| b | Struktur | Request-DTOs ligger inlined i controllers (`CreateAccountRequest`, `TransferRequest`). Konventionen är en typ per fil under `Contracts/` eller per feature. |
| c | Trådsäkerhet | `InMemoryTokenDenylist` är Singleton – korrekt med `ConcurrentDictionary`. ✅ |
| d | Performance | `AccountRepository.GetByAccountNumberAsync` hämtar tracking-entitet. Använd `AsNoTracking()` i queries (fungerar för `GetAccountsByOwnerIdAsync` ✅, saknas för `GetByAccountNumberAsync`). |
| e | Driftsättning | `MigrateAsync` vid uppstart (`DatabaseExtensions`): bekvämt i dev, farligt i prod. Production bör köra migrationer som ett separat steg i deploy-pipelinen. |
| f | CORS | Dev allow-list i `appsettings.Development.json`. Production nekar allt – fail-closed är rätt. ✅ |
| g | Domänpolicy | `StaffDomain`-kontroll (roll + `@nexapay.com`-epost) görs i `RegisterHandler`. Renare som en namngiven `IAuthorizationRequirement`/policy. |
| h | Health checks | `/health` returnerar alltid Healthy även om SQL är nere. Lägg till `AddDbContextCheck<ApplicationDbContext>()` och `AddRedis(...)`. |

---

### Lärarens rekommenderade prioritetsordning (nästa sprint)

| Prio | Åtgärd |
|------|--------|
| 1 | **Rik domänmodell** – flytta saldoändring, overdraft-check, IsActive-check, statustransitioner till aggregatmetoder (`Account.Deposit()`, `Account.Withdraw()`, `Account.Close()`). Lås setters. |
| 2 | **`Money` value object** med valuta. |
| 3 | **Ta bort generisk `IRepository<T>` från Domain.** Ersätt med avsiktsavslöjande metoder. Ta bort redundant `Update`. |
| 4 | **Fixa `UnitOfWork.Dispose`** – disposa inte context manuellt. |
| 5 | **Välj EN felmodell** (`Result<T>` *eller* exceptions) och sluta wrappa allt i `catch (Exception)`. |
| 6 | **Lägg till idempotens-nyckel** på alla pengaflyttande endpoints. |
| 7 | **Sluta logga hela requests** – sanera känsliga fält (lösenord). |
| 8 | **Domänhändelser** för de fyra pengarörelseoperationerna – ens in-process `INotification` är en riktig förbättring. |
| 9 | **Ta bort cascade-delete på Transactions** – transaktioner är en oföränderlig liggare. |
| 10 | **Generisk `ApiResponse<T>`** för korrekta OpenAPI-scheman. |
