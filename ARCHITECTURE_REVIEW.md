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

### HÖG – Ny granskning 2026-05-08

| # | Problem | Status | Konsekvens |
|---|---------|--------|------------|
| S1 | Rate limiting saknas på finansiella endpoints | Öppet | `TransactionsController` och `AccountsController` har ingen rate limiting. En angripare kan göra obegränsat antal insättningar/uttag/överföringar per sekund. Bara `AuthController` skyddas idag av policyn `"auth"`. |

**Åtgärd:** Lägg till en `"financial"` rate limit-policy (t.ex. 20 req/min per användare-ID) i `ServiceExtensions.cs` och applicera `[EnableRateLimiting("financial")]` på `TransactionsController`, `AccountsController` och `CardsController`.

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

### Ny granskning 2026-05-08 – öppna punkter

| # | Allvarlighetsgrad | Problem | Fil | Konsekvens |
|---|-------------------|---------|-----|------------|
| D | MEDEL | `AuditBehavior` använder reflektion för `IsSuccess` – defaultar till `true` | `AuditBehavior.cs:41-43` | `?.GetValue(response) as bool? ?? true` — om `TResponse` saknar `IsSuccess`-property loggas kommandot alltid som lyckat. Felaktigt audit-spår. |
| E | MEDEL | `UnitOfWork` omsluter `DbUpdateConcurrencyException` i generisk `Exception` | `UnitOfWork.cs:80-84` | `catch (DbUpdateConcurrencyException ex) { throw new Exception("...", ex); }` — anroparen kan inte fånga `DbUpdateConcurrencyException` specifikt. Optimistisk concurrency-hantering i handlers försvåras. |
| F | MEDEL | Kulturkänslig beloppsformatering i felmeddelanden | `WithdrawHandler.cs:71-73` | `{account.Balance:C}` och `{request.Amount:C}` ger olika output beroende på serverns `CultureInfo` (t.ex. `$1,000.00` vs `1 000,00 kr`). Inkonsekvent i loggar och API-svar. |
| G | MEDEL | Redis-fallback loggar ingen varning – tyst säkerhetsförsämring | `DependencyInjection.cs` | Om Redis-anslutningssträngen saknas används `InMemoryTokenDenylist` utan att någon loggpost skrivs. En felkonfigurerad prod-miljö degraderas tyst till singelinstans-denylist. |
| H | LÅG | Unikhetsgaranti för konto-/kortnummer via while-loop med DB-anrop | `CreateAccountHandler.cs`, `CreateCardHandler.cs` | Loopar `while (await _uow.Accounts.ExistsAsync(...))` tills ett unikt nummer hittas. Under hög last kan detta ge många DB-rundturer. Bättre: unik DB-constraint + retry på constraint violation. |
| I | LÅG | `InMemoryTokenDenylist.RemoveExpired()` itererar hela samlingen vid varje `Revoke` | `InMemoryTokenDenylist.cs` | O(n) rensning på varje logout-anrop. Inga problem vid låg volym, men skalas dåligt. |
| J | LÅG | Inga health check-endpoints | – | Ingen `/health`-endpoint. Kubernetes/load balancer kan inte avgöra om API:et är igång och kan nå databasen. |
| K | LÅG | Ingen API-versioneringsstrategi | – | Alla endpoints lever under `/api/`. En brytande förändring i framtiden kräver `/api/v2/` utan förberedd infrastruktur. |
| L | LÅG | Swagger saknar endpoint-beskrivningar och felresponsschemata | – | `[ProducesResponseType]`-attribut och `/// <summary>`-kommentarer saknas. Swagger UI visar bara statuskod 200 för alla endpoints. |

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
| Rate limiting-tester för finansiella endpoints | HÖG | Inga tester verifierar att 429 returneras vid missbruk av `/api/transactions` eller `/api/accounts`. Behövs när `S1` åtgärdas. |
| Test för `AuditBehavior` med typ utan `IsSuccess` | MEDEL | Ingen testtäckning för att `IsSuccess`-reflektionen defaultar korrekt (eller felaktigt). |
| Test för `DbUpdateConcurrencyException`-hantering | MEDEL | Inga tester verifierar att optimistisk concurrency triggas och hanteras rätt i `UnitOfWork`. |

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
| Arkitektur | 9/10 | Clean Architecture korrekt, rätt beroendeflöde, bra mönster |
| Kodkvalitet | 8/10 | Bra namngivning, `Transaction` oföränderlig, `IsStaff()` utbruten – men `AuditBehavior`-reflektion, exception-wrapping i `UnitOfWork` och kulturkänslig formatering är öppna brister |
| Säkerhet | 8/10 | CSPRNG, lockout, RBAC, token-revokering, audit log, DefaultChallengeScheme-bugg fixad – men rate limiting saknas på finansiella endpoints (S1) och Redis-fallback loggar ingen varning (G) |
| Funktionalitet | 9/10 | Alla CRUD-flöden, kortaktivering, domänbaserad rollbegränsning, staff kan skapa kort åt kunder, `POST /logout`, `POST /api/admin/users` |
| Testning | 8/10 | 148 tester – 133 enhetstester + 15 integrationstester – men saknar tester för rate limiting, concurrency och `AuditBehavior`-edge cases |
| Produktionsklar | 7/10 | Inga health checks, ingen API-versionering, tyst Redis-fallback och obegränsade finansiella endpoints gör att API:et inte är fullt produktionsklart |

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

### Kvarvarande punkter – prioriterad åtgärdslista

| Prio | # | Problem | Fil | Åtgärd |
|------|---|---------|-----|--------|
| HÖG | S1 | Rate limiting saknas på finansiella endpoints | `ServiceExtensions.cs`, `TransactionsController`, `AccountsController`, `CardsController` | Lägg till `"financial"` policy (20 req/min per user-ID), applicera `[EnableRateLimiting("financial")]` |
| MEDEL | D | `AuditBehavior` reflektionsfel ger felaktigt audit-spår | `AuditBehavior.cs` | Lägg till `where TResponse : Result` constraint, eller skapa `IResult`-interface med `IsSuccess`-property |
| MEDEL | E | `DbUpdateConcurrencyException` omsluts i generisk `Exception` | `UnitOfWork.cs` | Kasta om originaltypen, eller skapa en domänspecifik `ConcurrencyException` |
| MEDEL | F | `{balance:C}` kulturkänsligt i felmeddelanden | `WithdrawHandler.cs` | Använd `{balance:F2}` (invariant) eller `balance.ToString("F2", CultureInfo.InvariantCulture)` |
| MEDEL | G | Redis-fallback tyst – ingen varningslogg | `DependencyInjection.cs` | Logga `LogWarning("Redis ej konfigurerat – använder InMemoryTokenDenylist")` i else-grenen |
| LÅG | H | Unikhetsloop gör flera DB-anrop | `CreateAccountHandler.cs`, `CreateCardHandler.cs` | Lägg till `UNIQUE` constraint i DB + hantera `DbUpdateException` istf. while-loop |
| LÅG | I | `InMemoryTokenDenylist.RemoveExpired()` O(n) per Revoke | `InMemoryTokenDenylist.cs` | Kör rensning på bakgrundstimer (`IHostedService`) istf. vid varje `Revoke` |
| LÅG | J | Inga health check-endpoints | – | `services.AddHealthChecks().AddSqlServer(...)` + `app.MapHealthChecks("/health")` |
| LÅG | K | Ingen API-versionering | – | `Asp.Versioning.Http` NuGet + `[ApiVersion("1.0")]` på controllers |
| LÅG | L | Swagger saknar responsscheman | – | `[ProducesResponseType(typeof(ApiResponse<AccountDto>), 200)]` och `[ProducesResponseType(401)]` på endpoints |
| KONFIGURATION | – | `ConnectionStrings:Redis` tom i prod | `appsettings.json` | Sätt Redis-anslutningssträngen i miljövariabler/secrets för att aktivera skalbar denylist |
| KONFIGURATION | – | `AllowedHosts` i produktion | `appsettings.json` | Sätt till faktisk domän när API:et driftsätts |
