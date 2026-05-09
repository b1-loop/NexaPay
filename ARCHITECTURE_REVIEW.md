# NexaPay – Kvarvarande åtgärdspunkter

> **Datum:** 2026-05-09  
> **Branch:** master  
> **Stack:** .NET 8 · ASP.NET Core · EF Core 8 · MediatR · FluentValidation · AutoMapper · ASP.NET Identity · JWT

---

## Projektöversikt

```
NexaPay.sln
├── NexaPay.Domain          – Entiteter, value objects, interfaces, events. Inga externa NuGet-beroenden
├── NexaPay.Application     – Handlers, validators, DTOs, pipeline behaviors, policies
├── NexaPay.Infrastructure  – EF Core, repositories, Identity, JWT, Redis
├── NexaPay.API             – Controllers, Contracts/, middleware, Swagger, Program.cs
└── NexaPay.Tests           – 159 tester (enhet + integration)
```

**Status:** Alla öppna arkitekturpunkter åtgärdade. Nedanstående är konfigurationsval som sätts vid driftsättning — inget kvarstår i koden.

---

## Kvarvarande konfiguration (sätts vid driftsättning, inte i kod)

| # | Problem |
|---|---------|
| C1 | `ConnectionStrings:Redis` saknas i prod – sätt i miljövariabler/secrets. Varning loggas automatiskt om strängen saknas. |
| C2 | `AllowedHosts` i `appsettings.json` – sätt till faktisk domän när API:et driftsätts. |
| C3 | `MigrateAsync` vid uppstart loggar en varning i Production och kör sedan – bör ersättas av ett separat `dotnet ef database update`-steg i deploy-pipelinen vid horisontell skalning. |
