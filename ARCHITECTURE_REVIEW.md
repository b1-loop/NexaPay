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
- Kontolåsning konfigurerad: 5 misslyckade försök → 15 minuters lockout (se dock §3 – fungerar ej fullt ut)
- RBAC med 5 väldefinierade roller och tydlig rollhierarki
- Domänbaserad rollbegränsning vid registrering
- Rate limiting på `AuthController`: max 5 requests/minut per IP → 429 Too Many Requests
- Optimistisk concurrency via `RowVersion` på `Account` – förhindrar race conditions vid parallella transaktioner
- Ägarskapsvalidering i handlers: ägare-check sker INNAN data ändras
- Kortnummer maskeras i `CardDto` (`**** **** **** 9010`) – CVV returneras en gång vid skapande, lagras aldrig
- `ExceptionMiddleware` returnerar generiska felmeddelanden på 500-fel som når middleware
- Överföring tre-fas: validera allt → uppdatera → spara atomärt

### Domänbaserad rollbegränsning – registrering

`POST /register` är publik men personalroller kräver en `@nexapay.com`-e-postadress. Logiken sitter i `RegisterHandler` och domänen läses från konfiguration (`StaffDomain` i `appsettings.json`).

| E-postdomän | Begärd roll | Resultat |
|-------------|-------------|----------|
| `@nexapay.com` | Admin, BankManager, Teller, Auditor | ✅ Tillåtet |
| `@nexapay.com` | User | ✅ Tillåtet |
| Annan domän | Admin, BankManager, Teller, Auditor | ❌ Explicit fel |
| Annan domän | User | ✅ Tillåtet |

### Swagger

Korrekt konfigurerat med JWT Bearer-stöd och inlindat i `if (app.Environment.IsDevelopment())` – exponeras inte i produktion.

### TransferHandler – tre-fassäkerhet

1. **Fas 1: Validering** – hämta och validera ALLT, ingen uppdatering
2. **Fas 2: Uppdatering** – ändra saldon först när alla checks passerat
3. **Fas 3: Spara atomärt** – ett enda `SaveChangesAsync`

---

## 3. Säkerhetsproblem

### MEDEL

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 1 | `ex.Message` exponeras i catch-block | Flera handlers | Interna exception-meddelanden (DB-fel, sökvägar) skickas till klienten via `Result.Failure($"... {ex.Message}")`. Dessa går **inte** via `ExceptionMiddleware` utan returneras direkt i JSON-svaret. |
| 2 | Icke-kryptografisk slumpgenerator | `CreateCardHandler.cs` | Kortnummer och CVV genereras med `Random.Shared.Next()` – inte en CSPRNG. Ska använda `RandomNumberGenerator` från `System.Security.Cryptography`. |
| 3 | Kontolåsning enforced inte på misslyckade inloggningar | `AuthService.cs:LoginAsync` | `CheckPasswordAsync` triggar **inte** `AccessFailedAsync`. Räknaren för misslyckade inloggningar ökas aldrig → 15-minuters lockout aktiveras aldrig oavsett inställning. Kräver antingen `SignInManager.PasswordSignInAsync` eller manuellt anrop av `_userManager.AccessFailedAsync(user)`. Rate limiting (5 req/min per IP) ger delvis skydd men blockerar per IP, inte per konto. |
| 4 | `CreateCardHandler` saknar `IsStaff`-bypass | `CreateCardHandler.cs:55`, `CreateCardCommand.cs` | Kommandot har inget `IsStaff`-fält. Ägarskapskontrollen `account.OwnerId != request.UserId` har inget undantag för personal. Teller och BankManager kan **inte** skapa kort åt kunder, trots att `Roles.CanWrite` tillåter dem på endpoint-nivå. Inkonsekvent med `DepositHandler` och `WithdrawHandler` som har `IsStaff`-bypass. |

**Filer som exponerar `ex.Message`:**

| Fil | Rad | Kontext |
|-----|-----|---------|
| `TransferHandler.cs` | 186 | `$"Ett fel uppstod vid överföringen: {ex.Message}"` |
| `DepositHandler.cs` | 121 | `$"Ett fel uppstod vid insättningen: {ex.Message}"` |
| `WithdrawHandler.cs` | 98 | `$"Ett fel uppstod vid uttaget: {ex.Message}"` |
| `CreateCardHandler.cs` | 126 | `$"Ett fel uppstod när kortet skulle skapas: {ex.Message}"` |
| `AuthService.cs` | 104 | `$"Ett fel uppstod vid registrering: {ex.Message}"` |
| `AuthService.cs` | 150 | `$"Ett fel uppstod vid inloggning: {ex.Message}"` |
| `RegisterHandler.cs` | 43 | `$"Ett fel uppstod vid registrering: {ex.Message}"` |

**Fix för alla:** Ersätt `ex.Message` med en generisk text och logga ex via `ILogger`.

### LÅG

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 5 | `AllowedHosts: "*"` | `appsettings.json:17` | Inga host-begränsningar – bör sättas till faktisk domän i produktion |
| 6 | Inget audit log | Hela Infrastructure | Ingen spårning av vem som ändrat vad, när (utöver transaktionshistorik) |
| 7 | Ingen token-revokering | `JwtService.cs`, `AuthController.cs` | `Jti`-claim finns men används aldrig. Det finns ingen `POST /logout`-endpoint och inga medel att blacklista specifika tokens. Stulna tokens är giltiga i 24h. |
| 8 | `double.Parse` utan felhantering | `JwtService.cs:102` | `double.Parse(_configuration["Jwt:ExpiryHours"] ?? "24")` kastar `FormatException` om värdet är ogiltigt. Bör använda `double.TryParse`. |

---

## 4. Kodkvalitet & arkitekturbrister

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| A | `Transaction`-entitet inte verkligt oföränderlig | `Transaction.cs` | Alla properties har `{ get; set; }`. Koden och kommentarerna säger att transaktioner är oföränderliga men entiteten tillåter full mutation. Bör använda `{ get; init; }` (eller `{ get; private set; }`) för att tvinga fram oföränderlighet. |
| B | `Roles.CanTransfer` används för konto-radering | `AccountsController.cs:130` | `[Authorize(Roles = Roles.CanTransfer)]` på DELETE-endpoint är semantiskt förvirrande. CanTransfer = Admin, BankManager, User – vilket är rätt behörighet – men ett dedikerat `CanDelete` eller `CanManageAccounts` vore tydligare. |
| C | Duplicerad `IsStaff()`-logik | `AccountsController.cs:44`, `CardsController.cs:40`, `TransactionsController.cs:49` | Exakt samma fyra rader i tre controllers. Bör brytas ut till en extension method på `ClaimsPrincipal`. |

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
| `AuthServiceTests` | 8 | Registrering (5 scenarion), inloggning (3 scenarion) |
| `RegisterHandlerTests` | 8 | Domänbaserad rollbegränsning – alla kombinationer av domän och roll |
| `CreateAccountValidatorTests` | 9 | Tom, för kort, för lång, exakt min/max, ogiltig typ, tom OwnerId, alla typer |
| `DepositValidatorTests` | Flera | Belopp, beskrivning |
| `WithdrawValidatorTests` | Flera | Belopp, beskrivning |
| `TransferValidatorTests` | Flera | Från/till konton, belopp, självöverföring |
| `BlockCardHandlerTests` | 4 | Happy path, ej funnet, redan blockerat, utgånget |
| `ActivateCardHandlerTests` | 7 | Happy path, IsStaff, fel ägare, ej funnet, redan aktivt, blockerat, utgånget |
| `CreateCardHandlerTests` | 5 | Happy path, ej funnet, fel ägare, inaktivt konto, Inactive-status |
| `DeleteAccountHandlerTests` | 6 | Happy path, ej funnet, fel ägare, Admin override, saldo > 0, soft delete |
| `GetTransactionsByAccountHandlerTests` | 6 | Happy path, ej funnet, fel ägare, IsAdmin, Page=0→1, PageSize>100→100 |
| `RegisterValidatorTests` | 14 | E-post, lösenordskrav (4 regler), ogiltig roll, alla 5 giltiga roller |
| `LoginValidatorTests` | 4 | Happy path, tom e-post, ogiltigt format, tomt lösenord |

### Vad som saknas

| Saknas | Prioritet | Kommentar |
|--------|-----------|-----------|
| Test som verifierar att lockout faktiskt triggas | HÖG | `AuthServiceTests` mockerar bort Identity – det fångar inte att `AccessFailedAsync` aldrig anropas |
| Test för `IsStaff`-bypass i `CreateCardHandler` | MEDEL | Täcks inte – avslöjar det bugg som beskrivs i §3 |
| Integrationstester (`WebApplicationFactory`) | MEDEL | Testar hela HTTP-flödet end-to-end inkl. rate limiting och auth middleware |

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
| Kodkvalitet | 7/10 | Async genomgående, bra namngivning – men duplicerad logik och `Transaction`-mutabilitet drar ned |
| Säkerhet | 6/10 | JWT, CVV, lösenord, rate limiting och concurrency på plats – men `ex.Message`, `Random.Shared`, icke-fungerande lockout och saknad token-revokering är reella brister |
| Funktionalitet | 8/10 | Alla CRUD-flöden, kortaktivering, domänbaserad rollbegränsning – `CreateCard` saknar staff-bypass |
| Testning | 8/10 | ~130 tester, bred täckning – kvarståenede: lockout-test, staff-card-test, integrationstester |
| Produktionsklar | 6/10 | Fundamentet är solitt men problemen i §3 behöver åtgärdas |

---

### Kvarvarande öppna punkter – prioritetsordning

| Prioritet | # | Problem | Fil | Åtgärd |
|-----------|---|---------|-----|--------|
| HÖG | 3 | Kontolåsning fungerar inte | `AuthService.cs:LoginAsync` | Lägg till `await _userManager.AccessFailedAsync(user)` vid misslyckat lösenord och `await _userManager.ResetAccessFailedCountAsync(user)` vid lyckat |
| HÖG | 1 | `ex.Message` exponeras i 7 catch-block | Se tabell i §3 | Ersätt med generisk text + `ILogger` |
| MEDEL | 2 | `Random.Shared` för kortnummer/CVV | `CreateCardHandler.cs:137–152` | Byt till `RandomNumberGenerator.GetInt32()` |
| MEDEL | 4 | `CreateCardHandler` saknar IsStaff-bypass | `CreateCardCommand.cs`, `CreateCardHandler.cs:55`, `CardsController.cs:80` | Lägg till `IsStaff` i kommandot, skicka det från controllern, lägg till `if (!request.IsStaff && ...)` i handlens ägarskapscheck |
| MEDEL | – | Integrationstester | `NexaPay.Tests` | Lägg till `WebApplicationFactory`-baserade tester |
| LÅG | 7 | Ingen token-revokering | `JwtService.cs`, `AuthController.cs` | Lägg till `POST /logout` + en in-memory eller Redis-baserad denylist för `Jti` |
| LÅG | 8 | `double.Parse` utan felhantering | `JwtService.cs:102` | Byt till `double.TryParse` med fallback |
| LÅG | A | `Transaction` inte oföränderlig | `Transaction.cs` | Byt `{ get; set; }` → `{ get; init; }` |
| LÅG | C | Duplicerad `IsStaff()` | 3 controllers | Bryt ut till `ClaimsPrincipalExtensions.IsStaff()` |
| LÅG | 5 | `AllowedHosts: "*"` | `appsettings.json` | Sätt till faktisk domän i produktion |
| LÅG | 6 | Inget audit log | Infrastructure | Implementera audit trail för känsliga operationer |
