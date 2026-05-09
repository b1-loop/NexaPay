# NexaPay – Kvarvarande åtgärdspunkter

> **Datum:** 2026-05-09  
> **Branch:** master  
> **Stack:** .NET 8 · ASP.NET Core · EF Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT

---

## Projektöversikt

```
NexaPay.sln
├── NexaPay.Domain          – Entiteter, value objects, interfaces, events. Inga externa NuGet-beroenden
├── NexaPay.Application     – Handlers, validators, DTOs, pipeline behaviors
├── NexaPay.Infrastructure  – EF Core, repositories, Identity, JWT, Redis
├── NexaPay.API             – Controllers, middleware, Swagger, Program.cs
└── NexaPay.Tests           – 148 tester (133 enhets + 15 integrations)
```

**Status:** Alla stora arkitekturbrister och samtliga lärarens 20 feedback-punkter är åtgärdade. Kvar är mindre förbättringar och testtäckning.

---

## Kvarvarande åtgärdspunkter

### Tester

| # | Prioritet | Problem | Fil |
|---|-----------|---------|-----|
| T1 | MEDEL | Inga integrationstester verifierar att finansiella endpoints returnerar 429 vid för många requests | `AccountsIntegrationTests.cs` – saknas |
| T2 | LÅG | Inga tester verifierar att `ConcurrencyException` triggas och att `ConcurrencyRetryBehavior` försöker igen | Ny testfil saknas |

### Kod & arkitektur

| # | Prioritet | Problem | Fil/Plats |
|---|-----------|---------|-----------|
| K1 | LÅG | `GetByAccountNumberAsync` saknar `AsNoTracking()` – hämtar en tracking-entitet trots att den bara används för läsning | `AccountRepository.cs:28` |
| K2 | LÅG | `/health` returnerar alltid Healthy även om SQL Server eller Redis är nere – `AddDbContextCheck<ApplicationDbContext>()` är inte registrerat | `ServiceExtensions.cs:125` |
| K3 | LÅG | `DatabaseExtensions.MigrateAsync()` körs vid uppstart – bekvämt i dev, riskabelt i prod (en misslyckad migration kraschar applikationen) | `DatabaseExtensions.cs` |
| K4 | LÅG | Request-klasser (`CreateAccountRequest`, `TransferRequest` m.fl.) är inlinede i controllers – konventionen är en typ per fil | Alla controllers |
| K5 | LÅG | `StaffDomain`-kontrollen (roll + `@nexapay.com`-epost) görs i `RegisterHandler` – renare som en namngiven `IAuthorizationRequirement`/policy | `RegisterHandler.cs` |
| K6 | LÅG | Lärokommentarer i produktionskod (förklarar vad `Task<>`, `?`, `decimal` betyder) – hör hemma i onboarding-docs | Spridda filer |

### Konfiguration (sätts vid driftsättning, inte i kod)

| # | Problem |
|---|---------|
| C1 | `ConnectionStrings:Redis` saknas i prod – sätt i miljövariabler/secrets. Varning loggas automatiskt om strängen saknas. |
| C2 | `AllowedHosts` i `appsettings.json` – sätt till faktisk domän när API:et driftsätts. |
| C3 | `MigrateAsync` bör ersättas av ett separat migrations-steg i deploy-pipelinen i prod (se K3). |
