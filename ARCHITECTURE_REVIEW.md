# NexaPay – Fullständig Arkitekturanalys & Kodgranskning

> **Datum:** 2026-05-06  
> **Granskad av:** Claude – varje fil läst individuellt  
> **Branch:** master  
> **Stack:** .NET 8 · ASP.NET Core · Entity Framework Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT

---

## Innehåll

1. [Projektstruktur](#1-projektstruktur)
2. [Vad som fungerar bra](#2-vad-som-fungerar-bra)
3. [Verkliga buggar – måste åtgärdas](#3-verkliga-buggar--måste-åtgärdas)
4. [Säkerhetsproblem](#4-säkerhetsproblem)
5. [Designproblem & förbättringar](#5-designproblem--förbättringar)
6. [Tester – vad finns och vad saknas](#6-tester--vad-finns-och-vad-saknas)
7. [NuGet-paket](#7-nuget-paket)
8. [Sammanfattning & prioriterad åtgärdslista](#8-sammanfattning--prioriterad-åtgärdslista)

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
| Result Pattern | `Result<T>` i alla handlers | Explicit success/failure, inga kastas exceptions för affärslogikfel |
| Soft Delete | `Account.IsActive` + global query filter | Konton tas aldrig bort fysiskt |
| Pagination | `GetTransactionsByAccountIdPagedAsync` | Skip/Take med `PagedResult<T>` som returnerar metadata |
| DTO-separation | `AccountDto`, `CardDto`, `TransactionDto` | Interna entiteter exponeras aldrig direkt |
| Lazy Initialization | `UnitOfWork` | Repositories skapas bara vid behov via `??=` |

### Säkerhet som fungerar rätt

- JWT-validering med Issuer, Audience, Lifetime och `ClockSkew = TimeSpan.Zero`
- ASP.NET Identity med starka lösenordskrav: 8+ tecken, versaler, gemener, siffror, specialtecken
- Kontolåsning: 5 misslyckade försök → 15 minuters lockout
- RBAC med 5 väldefinierade roller och tydlig rollhierarki
- Ägarskapsvalidering i handlers: ägare-check sker INNAN data ändras
- Kortnummer maskeras i `CardDto` (`**** **** **** 9010`) – CVV skickas aldrig ut
- `ExceptionMiddleware` returnerar generiska felmeddelanden på 500-fel

### Swagger

Swagger är korrekt konfigurerat med JWT Bearer-stöd och är **redan inlindat i `if (app.Environment.IsDevelopment())`** i `ServiceExtensions.cs` – exponeras alltså inte i produktion.

### TransferHandler – buggfix-logiken

`TransferHandler` är uppdelad i tre faser:
1. **Validering** – hämta och validera ALLT, ingen uppdatering
2. **Uppdatering** – ändra saldon först när alla checks passerat
3. **Spara atomärt** – ett enda `SaveChangesAsync`

Det är korrekt och skyddar mot att pengar försvinner vid valideringsfel.

---

---

## 4. Säkerhetsproblem

### KRITISKT

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 3 | Vem som helst kan registrera sig som Admin | `AuthController.cs` – `POST /register` | Endpointen är publik (inget `[Authorize]`) och accepterar `"Role": "Admin"` |

**Admin-registrering – åtgärd:**
```csharp
// AuthController.cs – begränsa till User-rollen utan autentisering
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request)
{
    // Utan autentisering kan bara User-rollen sättas
    var role = Roles.User;
    // ...
}

// Separat endpoint för personal, kräver Admin-token:
[HttpPost("register/staff")]
[Authorize(Roles = Roles.Admin)]
public async Task<IActionResult> RegisterStaff([FromBody] RegisterStaffRequest request) { ... }
```

---

### MEDEL

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 5 | Ingen rate limiting | `AuthController.cs` | Brute-force på `POST /login` möjlig |
| 6 | Race condition på saldo | `DepositHandler`, `WithdrawHandler`, `TransferHandler` | Ingen optimistisk concurrency (`RowVersion`) – två simultana requests kan ge felaktigt saldo |

**Rate limiting – åtgärd:**
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    }));

// AuthController.cs
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase { ... }
```

**Race condition – åtgärd:**
```csharp
// Account.cs
[Timestamp]
public byte[] RowVersion { get; set; } = [];

// AccountConfiguration.cs
builder.Property(a => a.RowVersion).IsRowVersion();
```

---

### LÅG

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 8 | `AllowedHosts: "*"` | `appsettings.json` | Inga host-begränsningar |
| 10 | Inget audit log | Hela Infrastructure | Ingen spårning av vem som ändrat vad |

---

## 5. Designproblem & förbättringar

---

## 6. Tester – vad finns och vad saknas

### Testfiler (14 st)

| Fil | Antal tester | Täcker |
|-----|-------------|--------|
| `CreateAccountHandlerTests` | 4 | Skapande, noll-saldo, SaveChanges, mapping |
| `DepositHandlerTests` | **6** | Insättning, overdraft, fel ägare, inaktivt konto, transaktionstyp, Teller IsStaff |
| `WithdrawHandlerTests` | Flera | Uttag, overdraft-skydd |
| `TransferHandlerTests` | **8** | Happy path, fel ägare, insufficient balance, saknade konton, inaktiva konton, exakt saldo |
| `AccountTests` | Flera | Domänentitet |
| `AuthServiceTests` | **8** | Registrering (5 scenarion), inloggning (3 scenarion) |
| `CreateAccountValidatorTests` | **9** | Tom, för kort, för lång, exakt min/max, ogiltig typ, tom OwnerId, alla typer |
| `DepositValidatorTests` | Flera | Belopp, beskrivning |
| `WithdrawValidatorTests` | Flera | Belopp, beskrivning |
| `TransferValidatorTests` | Flera | Från/till konton, belopp, självöverföring |
| `BlockCardHandlerTests` | **4** | Happy path, ej funnet, redan blockerat, utgånget |
| `ActivateCardHandlerTests` | **7** | Happy path, IsStaff, fel ägare, ej funnet, redan aktivt, blockerat, utgånget |
| `CreateCardHandlerTests` | **5** | Happy path, ej funnet, fel ägare, inaktivt konto, Inactive-status |
| `DeleteAccountHandlerTests` | **6** | Happy path, ej funnet, fel ägare, Admin override, saldo > 0, soft delete |
| `GetTransactionsByAccountHandlerTests` | **6** | Happy path, ej funnet, fel ägare, IsAdmin, Page=0→1, PageSize>100→100 |
| `RegisterValidatorTests` | **14** | E-post, lösenordskrav (4 regler), ogiltig roll, alla 5 giltiga roller |
| `LoginValidatorTests` | **4** | Happy path, tom e-post, ogiltigt format, tomt lösenord |

`TransferHandlerTests` är särskilt välskrivet – täcker alla affärsregler och verifierar att `SaveChangesAsync` aldrig anropas vid fel.

### Vad som saknas

| Saknas | Prioritet | Kommentar |
|--------|-----------|-----------|
| Test: Admin kan registreras via publik endpoint | HÖG | Kräver integrationstester |
| Integrationstester (`WebApplicationFactory`) | MEDEL | Testar hela HTTP-flödet |

### Testarkitekturen är bra

`TestBase` med `MockUnitOfWork`, `MockAccountRepository` och `MockTransactionRepository` kopplat via `Setup()` är ett bra mönster. Riktig AutoMapper används (inte mockad) vilket innebär att mappingsfel fångas i tester.

---

## 7. NuGet-paket

| Paket | Version | Status |
|-------|---------|--------|
| MediatR | 12.4.0 | OK |
| AutoMapper | 16.1.1 | OK |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | OK |
| Microsoft.Extensions.Logging.Abstractions | **10.0.0** | ⚠️ .NET 10 preview – bör vara 8.0.x |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.26 | OK |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.26 | OK (men dubblerad i API) |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.26 | OK |
| Swashbuckle.AspNetCore | 6.9.0 | OK |
| NUnit | 3.14.0 | OK |
| Moq | 4.20.72 | OK |
| FluentAssertions | 8.9.0 | OK |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.26 | OK |
| coverlet.collector | 6.0.0 | OK |

---

## 8. Sammanfattning & prioriterad åtgärdslista

### Betyg

| Område | Betyg | Kommentar |
|--------|-------|-----------|
| Arkitektur | 9/10 | Clean Architecture korrekt, rätt beroendeflöde, bra mönster |
| Kodkvalitet | 7/10 | Async genomgående, bra namngivning, men felaktig using, inkonsistent filstruktur |
| Säkerhet | 6/10 | JWT-nyckel, CVV och lösenordsloggning åtgärdade – kvar: fri Admin-registrering, ingen rate limiting |
| Funktionalitet | 8/10 | Teller-bug, kortaktivering och ExpiresAt-synk fixade |
| Testning | 9/10 | Bred täckning över alla handlers och validators – kvar: integrationstester |
| Produktionsklar | 5/10 | Flera säkerhetsproblem lösta – Admin-registrering måste begränsas innan deploy |

---

### Prioriterad åtgärdslista

#### KRITISKT (blockerande för produktion)

1. **Begränsa Admin-registrering** – publik endpoint ska bara tillåta `User`-rollen

#### HÖG (bör fixas snart)

2. **Lägg till rate limiting** på `AuthController` mot brute-force  

#### MEDEL (förbättringar)

4. **Lägg till `RowVersion`** på `Account` för optimistisk concurrency  
