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

## 3. Verkliga buggar – måste åtgärdas

### Bug 2 – `AuthDto.ExpiresAt` är hårdkodad till 24 timmar

**Fil:** `NexaPay.Infrastructure/Identity/AuthService.cs` rad 100 och 141

```csharp
ExpiresAt = DateTime.UtcNow.AddHours(24) // Hårdkodat!
```

Men `JwtService` läser från konfigurationen:
```csharp
expires: DateTime.UtcNow.AddHours(
    double.Parse(_configuration["Jwt:ExpiryHours"] ?? "24"))
```

Om `Jwt:ExpiryHours` ändras i konfigurationen gäller den faktiska token-livslängden det nya värdet, men `ExpiresAt`-fältet i API-svaret säger fortfarande 24 timmar. Klienten får felaktig information om när token löper ut.

**Åtgärd:** Läs `ExpiryHours` från `IConfiguration` i `AuthService` och använd samma värde för `ExpiresAt`.

---

## 4. Säkerhetsproblem

### KRITISKT

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 1 | JWT-signeringsnyckel i klartext | `appsettings.json` rad 6 | `"Key": "NexaPaySuperSecretKeyThatIsAtLeast32CharactersLong!"` – vem som helst med repo-åtkomst kan signera godtyckliga JWT-tokens |
| 2 | CVV lagras i databasen | `Card.cs`, `CardConfiguration.cs`, `CreateCardHandler.cs` | Bryter mot PCI-DSS 3.2.1. Koden kommenterar själv att det inte bör göras |
| 3 | Vem som helst kan registrera sig som Admin | `AuthController.cs` – `POST /register` | Endpointen är publik (inget `[Authorize]`) och accepterar `"Role": "Admin"` |

**JWT-nyckel – åtgärd:**
```bash
# Lägg till i .gitignore:
appsettings.json

# Använd User Secrets i dev:
dotnet user-secrets set "Jwt:Key" "ny-hemlig-nyckel-minst-32-tecken"

# I produktion: miljövariabel
JWT__KEY=din-hemliga-nyckel
```

**CVV – åtgärd:**
Ta bort `CVV`-property från `Card`, ta bort från `CardConfiguration`, skapa ny migration. Returnera CVV *en gång* i `CreateCardHandler` som en separat sträng utanför `CardDto` – spara det aldrig.

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
| 4 | Lösenord loggas i klartext | `LoggingBehavior.cs` rad 52–54 | `{@Request}` serialiserar hela `LoginCommand` inkl. `Password` |
| 5 | Ingen rate limiting | `AuthController.cs` | Brute-force på `POST /login` möjlig |
| 6 | Race condition på saldo | `DepositHandler`, `WithdrawHandler`, `TransferHandler` | Ingen optimistisk concurrency (`RowVersion`) – två simultana requests kan ge felaktigt saldo |

**Lösenordsloggning – åtgärd:**
```csharp
// LoginCommand.cs – överskrid ToString()
public override string ToString() =>
    $"LoginCommand {{ Email = {Email} }}"; // Password utelämnas
```

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
| 7 | CORS AllowAll | `ServiceExtensions.cs` rad 145–153 | Tillåter alla origins i alla miljöer |
| 8 | `AllowedHosts: "*"` | `appsettings.json` | Inga host-begränsningar |
| 9 | `Console.WriteLine` i produktionskod | `DatabaseExtensions.cs` rad 131 | Ska vara `ILogger` |
| 10 | Inget audit log | Hela Infrastructure | Ingen spårning av vem som ändrat vad |

---

## 5. Designproblem & förbättringar

### 5.4 `IJwtService`-interfacet definieras i samma fil som implementationen

**Fil:** `NexaPay.Infrastructure/Identity/JwtService.cs`

`interface IJwtService` och `class JwtService` ligger i samma fil. Konventionen är en klass/interface per fil. Interfacet bör antingen flytta till Application-lagret (om det ska vara testbart) eller till en separat fil i Infrastructure.

---

### 5.7 Retry-logik i EF Core kan maskera problem

**Fil:** `NexaPay.Infrastructure/DependencyInjection.cs` rad 49–56

```csharp
sqlOptions.EnableRetryOnFailure(
    maxRetryCount: 3,
    maxRetryDelay: TimeSpan.FromSeconds(30),
    ...);
```

`maxRetryDelay: TimeSpan.FromSeconds(30)` kombinerat med exponentiell backoff kan ge väntetider på upp till 90+ sekunder vid upprepade fel. HTTP-timeouts inträffar långt innan dess (standard ASP.NET Core request timeout). Överväg ett lägre värde (t.ex. 5 sekunder).

---

### 5.8 `AuthDto.ExpiresAt` och token-livslängd är osynkroniserade

Se **Bug 2** ovan. `AuthDto.ExpiresAt` är hårdkodad till 24 h medan den faktiska token-livslängden läses från konfigurationen.

---

## 6. Tester – vad finns och vad saknas

### Testfiler (14 st)

| Fil | Antal tester | Täcker |
|-----|-------------|--------|
| `CreateAccountHandlerTests` | 4 | Skapande, noll-saldo, SaveChanges, mapping |
| `DepositHandlerTests` | Flera | Insättning, overdraft |
| `WithdrawHandlerTests` | Flera | Uttag, overdraft-skydd |
| `TransferHandlerTests` | **8** | Happy path, fel ägare, insufficient balance, saknade konton, inaktiva konton, exakt saldo |
| `AccountTests` | Flera | Domänentitet |
| `AuthServiceTests` | **8** | Registrering (5 scenarion), inloggning (3 scenarion) |
| `CreateAccountValidatorTests` | **9** | Tom, för kort, för lång, exakt min/max, ogiltig typ, tom OwnerId, alla typer |
| `DepositValidatorTests` | Flera | Belopp, beskrivning |
| `WithdrawValidatorTests` | Flera | Belopp, beskrivning |
| `TransferValidatorTests` | Flera | Från/till konton, belopp, självöverföring |

`TransferHandlerTests` är särskilt välskrivet – täcker alla affärsregler och verifierar att `SaveChangesAsync` aldrig anropas vid fel.

### Vad som saknas

| Saknas | Prioritet | Kommentar |
|--------|-----------|-----------|
| Test: Teller kan göra deposit på kundens konto | HÖG | Avslöjar Bug 2 |
| Test: Admin kan registreras via publik endpoint | HÖG | Avslöjar Bug 3 (säkerhet) |
| Test: `ActivateCardHandler` | MEDEL | Ingen täckning för kortaktivering |
| `BlockCardHandlerTests` | MEDEL | Ingen täckning för kortblockering |
| `CreateCardHandlerTests` | MEDEL | Ingen täckning för kortskapande |
| `DeleteAccountHandlerTests` | MEDEL | Ingen täckning för kontostängning |
| `GetTransactionsByAccountHandlerTests` | LÅG | Pagination-logik otestad |
| `RegisterValidatorTests` | LÅG | Lösenordskomplex, rollvalidering |
| `LoginValidatorTests` | LÅG | E-postformat |
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
| Säkerhet | 4/10 | JWT i klartext, CVV i DB, fri Admin-registrering, lösenord loggas |
| Funktionalitet | 7/10 | Alla CRUD-flöden finns men Teller-bug, ingen kortaktivering |
| Testning | 7/10 | Bra täckning på Transfer och Auth, men stora luckor (BlockCard, DeleteAccount) |
| Produktionsklar | 4/10 | Kräver säkerhetsåtgärder och buggfixar innan deploy |

---

### Prioriterad åtgärdslista

#### KRITISKT (blockerande för produktion)

1. **Ta bort JWT-nyckeln från `appsettings.json`** → User Secrets / miljövariabel  
2. **Ta bort CVV-lagring** – ny migration, returnera CVV en gång vid skapande  
3. **Begränsa Admin-registrering** – publik endpoint ska bara tillåta `User`-rollen  
4. ~~**Fixa Teller-bug**~~ – ✅ Åtgärdad (2026-05-06)

#### HÖG (bör fixas snart)

5. ~~**Lägg till kortaktivering**~~ – ✅ Åtgärdad (2026-05-06)  
6. **Fixa `AuthDto.ExpiresAt`** – läs `Jwt:ExpiryHours` från konfiguration istället för hårdkodat 24  
7. **Lägg till rate limiting** på `AuthController` mot brute-force  
8. ~~**Ta bort felaktigt `using System.Transactions;`** i `Account.cs`~~ – ✅ Åtgärdad (2026-05-06)  

#### MEDEL (förbättringar)

9. ~~**Flytta `DeleteAccountCommand.cs`**~~ – ✅ Åtgärdad (2026-05-06)  
10. **Skydda mot lösenordsloggning** – överskrid `ToString()` på `LoginCommand`  
11. **Lägg till `RowVersion`** på `Account` för optimistisk concurrency  
12. ~~**Lägg till kortnummer-kontroll** i `CreateCardHandler`~~ – ✅ Åtgärdad (2026-05-06)  
13. ~~**Ta bort explicit `Microsoft.Extensions.Logging.Abstractions`-pin**~~ – ✅ Åtgärdad (2026-05-06)  
14. ~~**Ta bort `Microsoft.AspNetCore.Identity.EntityFrameworkCore`** från API-projektet~~ – ✅ Åtgärdad (2026-05-06)  

#### LÅG (städning)

15. ~~**Använd rollkonstanter i controllers**~~ – ✅ Åtgärdad (2026-05-06)  
16. Flytta `IJwtService` till en egen fil (eller till Application-lagret)  
17. Ersätt `Console.WriteLine` i `DatabaseExtensions.cs` med `ILogger`  
18. ~~**Ta bort `GetTransactionsByAccountIdAsync`**~~ – ✅ Åtgärdad (2026-05-06)  
19. Specificera CORS-origins per miljö istället för AllowAll  
20. Lägg till tester för `BlockCardHandler`, `CreateCardHandler`, `DeleteAccountHandler`  
