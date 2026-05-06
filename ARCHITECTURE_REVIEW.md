# NexaPay – Fullständig Arkitekturanalys & Kodgranskning

> **Datum:** 2026-05-06  
> **Granskad av:** Claude – varje fil läst individuellt  
> **Branch:** master  
> **Stack:** .NET 8 · ASP.NET Core · Entity Framework Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT

---

## Innehåll

1. [Projektstruktur](#1-projektstruktur)
2. [Vad som fungerar bra](#2-vad-som-fungerar-bra)
3. [Säkerhetsproblem](#3-säkerhetsproblem)
4. [Tester – vad finns och vad saknas](#4-tester--vad-finns-och-vad-saknas)
5. [NuGet-paket](#5-nuget-paket)
6. [Sammanfattning & prioriterad åtgärdslista](#6-sammanfattning--prioriterad-åtgärdslista)

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
- JWT-nyckel och connection string lagras i User Secrets (dev) / miljövariabler (prod) – aldrig i källkod
- ASP.NET Identity med starka lösenordskrav: 8+ tecken, versaler, gemener, siffror, specialtecken
- Kontolåsning: 5 misslyckade försök → 15 minuters lockout
- RBAC med 5 väldefinierade roller och tydlig rollhierarki
- Domänbaserad rollbegränsning vid registrering (se nedan)
- Rate limiting på `AuthController`: max 5 requests/minut per IP → 429 Too Many Requests
- Optimistisk concurrency via `RowVersion` på `Account` – förhindrar race conditions vid parallella transaktioner
- Ägarskapsvalidering i handlers: ägare-check sker INNAN data ändras
- Kortnummer maskeras i `CardDto` (`**** **** **** 9010`) – CVV returneras en gång vid skapande, lagras aldrig
- `ExceptionMiddleware` returnerar generiska felmeddelanden på 500-fel

### Domänbaserad rollbegränsning – registrering

`POST /register` är publik men personalroller kräver en `@nexapay.com`-e-postadress. Logiken sitter i `RegisterHandler` och domänen läses från konfiguration (`StaffDomain` i `appsettings.json`).

| E-postdomän | Begärd roll | Resultat |
|-------------|-------------|----------|
| `@nexapay.com` | Admin, BankManager, Teller, Auditor | ✅ Tillåtet |
| `@nexapay.com` | User | ✅ Tillåtet |
| Annan domän | Admin, BankManager, Teller, Auditor | ❌ Explicit fel – "Personalroller kräver en @nexapay.com-e-postadress" |
| Annan domän | User | ✅ Tillåtet |

**Inblandade filer:**
- `appsettings.json` – `"StaffDomain": "nexapay.com"` (konfigurerbar, ej hårdkodad)
- `IAppSettings` – interface i Application-lagret
- `AppSettings` – implementation i Infrastructure, läser från `IConfiguration`
- `RegisterHandler` – tillämpar domänregeln innan `AuthService` anropas

### Swagger

Swagger är korrekt konfigurerat med JWT Bearer-stöd och är **redan inlindat i `if (app.Environment.IsDevelopment())`** i `ServiceExtensions.cs` – exponeras alltså inte i produktion.

### TransferHandler – buggfix-logiken

`TransferHandler` är uppdelad i tre faser:
1. **Validering** – hämta och validera ALLT, ingen uppdatering
2. **Uppdatering** – ändra saldon först när alla checks passerat
3. **Spara atomärt** – ett enda `SaveChangesAsync`

Det är korrekt och skyddar mot att pengar försvinner vid valideringsfel.

---

## 3. Säkerhetsproblem

### LÅG

| # | Problem | Fil | Detalj |
|---|---------|-----|--------|
| 8 | `AllowedHosts: "*"` | `appsettings.json` | Inga host-begränsningar |
| 10 | Inget audit log | Hela Infrastructure | Ingen spårning av vem som ändrat vad |

---

## 4. Tester – vad finns och vad saknas

### Testfiler

| Fil | Antal tester | Täcker |
|-----|-------------|--------|
| `CreateAccountHandlerTests` | 4 | Skapande, noll-saldo, SaveChanges, mapping |
| `DepositHandlerTests` | **6** | Insättning, overdraft, fel ägare, inaktivt konto, transaktionstyp, Teller IsStaff |
| `WithdrawHandlerTests` | Flera | Uttag, overdraft-skydd |
| `TransferHandlerTests` | **8** | Happy path, fel ägare, insufficient balance, saknade konton, inaktiva konton, exakt saldo |
| `AccountTests` | Flera | Domänentitet |
| `AuthServiceTests` | **8** | Registrering (5 scenarion), inloggning (3 scenarion) |
| `RegisterHandlerTests` | **8** | Domänbaserad rollbegränsning – alla kombinationer av domän och roll |
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
| Integrationstester (`WebApplicationFactory`) | MEDEL | Testar hela HTTP-flödet end-to-end |

### Testarkitekturen är bra

`TestBase` med `MockUnitOfWork`, `MockAccountRepository` och `MockTransactionRepository` kopplat via `Setup()` är ett bra mönster. Riktig AutoMapper används (inte mockad) vilket innebär att mappingsfel fångas i tester.

---

## 5. NuGet-paket

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

## 6. Sammanfattning & prioriterad åtgärdslista

### Betyg

| Område | Betyg | Kommentar |
|--------|-------|-----------|
| Arkitektur | 9/10 | Clean Architecture korrekt, rätt beroendeflöde, bra mönster |
| Kodkvalitet | 8/10 | Async genomgående, bra namngivning, välstrukturerade handlers |
| Säkerhet | 8/10 | JWT, CVV, lösenordsloggning, Admin-registrering, rate limiting och concurrency åtgärdade |
| Funktionalitet | 8/10 | Alla CRUD-flöden, kortaktivering, domänbaserad rollbegränsning |
| Testning | 9/10 | 130 tester, bred täckning – kvar: integrationstester |
| Produktionsklar | 7/10 | Säkerhetsfundamentet på plats – kvar: audit log, AllowedHosts |

---

### Kvarvarande öppna punkter

| # | Problem | Prioritet |
|---|---------|-----------|
| 8 | `AllowedHosts: "*"` i `appsettings.json` | LÅG |
| 10 | Inget audit log | LÅG |
| – | Integrationstester (`WebApplicationFactory`) | MEDEL |
