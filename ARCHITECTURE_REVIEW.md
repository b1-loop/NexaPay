# NexaPay – Arkitekturanalys & Kodgranskning

> **Datum:** 2026-05-06  
> **Granskad av:** Claude (AI-assistent)  
> **Branch:** master  
> **Ramverk:** .NET 8 · ASP.NET Core · Entity Framework Core 8

---

## Innehåll

1. [Projektstruktur & lager](#1-projektstruktur--lager)
2. [Vad som är bra](#2-vad-som-är-bra)
3. [Vad som kan förbättras](#3-vad-som-kan-förbättras)
4. [Säkerhetsproblem](#4-säkerhetsproblem)
5. [Tester](#5-tester)
6. [NuGet-paket](#6-nuget-paket)
7. [Sammanfattande betyg](#7-sammanfattande-betyg)

---

## 1. Projektstruktur & lager

```
NexaPay.sln
├── NexaPay.Domain          (Entiteter, interface, enums – inga externa beroenden)
├── NexaPay.Application     (Handlers, validators, DTOs, MediatR – ingen DB-åtkomst)
├── NexaPay.Infrastructure  (EF Core, repos, Identity, JWT)
├── NexaPay.API             (Controllers, middleware, Swagger, startup)
└── NexaPay.Tests           (NUnit, Moq, FluentAssertions, InMemory EF)
```

Beroendeflödet är korrekt: `API → Application → Domain` och `Infrastructure → Domain + Application`.  
Domain-lagret har inga externa NuGet-beroenden alls – det är rätt.

---

## 2. Vad som är bra

### Arkitektur

| Mönster | Status |
|---------|--------|
| Clean Architecture | Korrekt implementerat med 4 lager |
| CQRS med MediatR | Alla kommandon och queries hanteras via handlers |
| Repository Pattern | Generisk `IRepository<T>` + specifika repos per entitet |
| Unit of Work | `IUnitOfWork` samlar alla repos, atomärt SaveChanges |
| Result Pattern | `Result<T>` – explicit success/failure utan exception-missbruk |
| Pipeline Behaviors | LoggingBehavior → ValidationBehavior → Handler (korrekt ordning) |
| Soft Delete | Global query filter på `IsActive` för Account |
| Pagination | `GetTransactionsByAccountIdPagedAsync` med Skip/Take |

### Säkerhet

- JWT-autentisering med korrekt validering (Issuer, Audience, ClockSkew = 0)
- ASP.NET Identity med starka lösenordskrav (8+ tecken, versaler, siffror, specialtecken)
- Kontolåsning efter 5 misslyckade inloggningsförsök (15 min)
- RBAC med 5 väldefinierade roller (Admin, BankManager, Teller, Auditor, User)
- Ägarskapsvalidering – en User kan inte se andras konton
- Cascade delete på Account → Transactions och Account → Cards

### Kodkvalitet

- Alla I/O-anrop är asynkrona (async/await genomgående)
- Nullable reference types aktiverat
- Rollkonstanter i statisk klass (`Roles.Admin`, `Roles.Teller`, etc.) – inga magic strings
- `ApiResponse<T>` som standardiserat svarsobjekt
- Global exceptionsmiddleware med strukturerade JSON-felmeddelanden
- `ExpiryDate` på Card använder `DateOnly` (korrekt val)

---

## 3. Vad som kan förbättras

### 3.1 Transactions-controller – OK

`TransactionsController.cs` finns och exponerar alla fyra endpoints:

| Metod | Route | Roller |
|-------|-------|--------|
| `GET` | `api/transactions/account/{id}?page=1&pageSize=20` | Alla inloggade |
| `POST` | `api/transactions/deposit` | Admin, BankManager, Teller, User |
| `POST` | `api/transactions/withdraw` | Admin, BankManager, Teller, User |
| `POST` | `api/transactions/transfer` | Admin, BankManager, User |

Notera att **Teller är exkluderad från Transfer** – det är ett medvetet rollbeslut (junior personal kan hjälpa med in-/uttag men inte genomföra överföringar).

---

### 3.2 JWT-nyckel i appsettings.json

**Problem:** `"Key": "NexaPaySuperSecretKeyThatIsAtLeast32CharactersLong!"` ligger hårdkodad i `appsettings.json` som troligen versionshanteras.

**Risk:** Om repot är offentligt exponeras signeringsnyckeln – alla kan skapa giltiga JWT-tokens.

**Åtgärd:** Flytta till .NET User Secrets (utveckling) och miljövariabler / Azure Key Vault (produktion):
```bash
dotnet user-secrets set "Jwt:Key" "din-hemliga-nyckel"
```
Lägg till `appsettings.json` i `.gitignore` eller ta bort nyckelvärdet från filen.

---

### 3.3 CORS är för tillåtande

**Problem:** `"AllowAll"`-policyn tillåter alla ursprung, metoder och headers.

**Åtgärd:** Specificera tillåtna origins i produktion:
```csharp
builder.WithOrigins("https://nexapay.se")
       .AllowAnyHeader()
       .AllowAnyMethod();
```

---

### 3.4 Swagger exponeras i alla miljöer

**Problem:** Swagger är aktivt även i produktion, vilket exponerar hela API-kontraktet.

**Åtgärd:**
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

---

### 3.5 Ingen rate limiting

**Problem:** API:et saknar begränsning på antal anrop. En angripare kan t.ex. brute-force:a lösenord eller överbelasta systemet.

**Åtgärd:** Använd .NET 7+ inbyggd rate limiting:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromMinutes(1);
    });
});
```
Applicera på `AuthController` som minimum.

---

### 3.6 Ingen optimistisk concurrency-kontroll

**Problem:** Om två requests uppdaterar samma konto simultant (t.ex. två överföringar) kan Race Condition uppstå och saldon bli felaktiga.

**Åtgärd:** Lägg till `RowVersion`/`ConcurrencyToken` på Account:
```csharp
[Timestamp]
public byte[] RowVersion { get; set; } = [];
```
Konfigurera i `AccountConfiguration`:
```csharp
builder.Property(a => a.RowVersion).IsRowVersion();
```

---

### 3.7 Ingen explicit databastransaktion i Transfer

**Problem:** TransferHandler uppdaterar två konton och sparar. Om ett sparande misslyckas halvvägs kan saldot bli inkonsekvent.

**Åtgärd:** Wrappa i en explicit `IDbContextTransaction`:
```csharp
await using var tx = await _context.Database.BeginTransactionAsync();
// ... uppdatera båda konton
await tx.CommitAsync();
```
Alternativt kan `IUnitOfWork.SaveChangesAsync` alltid köras en gång efter båda uppdateringarna (vilket delvis redan görs).

---

### 3.8 Koppling från Application till Infrastructure via IAuthService

**Problem:** `IAuthService` är definierad i Application men implementeras i Infrastructure – det är korrekt. Men om Application-projektet refererar direkt till Infrastructure-typer bryter det clean architecture.

**Verifiera** att Application bara använder interfacet och att registreringen sker i API/Infrastructure.

---

### 3.9 Rollvalidering vid registrering

**Problem:** `RegisterValidator` validerar att en roll är giltig, men vem som helst kan registrera sig som Admin via `POST /auth/register`.

**Åtgärd:** Begränsa vilka roller som kan sättas vid publik registrering:
```csharp
// Tillåt bara "User" utan autentisering
// Admin/BankManager/Teller/Auditor kräver Admin-behörighet
```

---

### 3.10 Loggning loggar hela request-objektet

**Problem:** `LoggingBehavior` loggar hela request-objektet, vilket kan inkludera lösenord i klartext vid `LoginCommand`.

**Åtgärd:** Implementera `ILoggingExclusion`-markörinterface eller överskrid `ToString()` på känsliga requests:
```csharp
public class LoginCommand : IRequest<Result<AuthDto>>
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = ""; // loggas ej!
    
    public override string ToString() => $"LoginCommand {{ Email = {Email} }}";
}
```

---

## 4. Säkerhetsproblem

### KRITISKT

| # | Problem | Fil | Risk |
|---|---------|-----|------|
| 1 | JWT-signeringsnyckel i versionshanterad fil | `appsettings.json` | Kritisk – token-förfalskning möjlig |
| 2 | CVV lagras i databasen | `Card.cs`, `CardConfiguration.cs` | Bryter mot PCI-DSS |
| 3 | Vem som helst kan registrera sig som Admin | `AuthController.cs` | Eskalering av privilegier |

### MEDEL

| # | Problem | Fil | Risk |
|---|---------|-----|------|
| 4 | Lösenord loggas (potentiellt) | `LoggingBehavior.cs` | Läckage av autentiseringsuppgifter |
| 5 | Swagger tillgänglig i produktion | `Program.cs` | Exponerar API-kontrakt |
| 6 | Race condition på saldo | `TransferHandler.cs` | Inkonsekvent data |
| 7 | Ingen rate limiting | `AuthController.cs` | Brute-force-attacker |

### LÅG

| # | Problem | Fil | Risk |
|---|---------|-----|------|
| 8 | CORS AllowAll | `ServiceExtensions.cs` | Obegränsad cross-origin åtkomst |
| 9 | `AllowedHosts: "*"` | `appsettings.json` | Host header injection |
| 10 | Inget audit log | Hela Infrastructure | Svårare att spåra missbruk |

---

### CVV – Detalj

CVV-koder ska **aldrig** lagras i databasen efter att en kortvalidering är klar. Det är ett direkt brott mot PCI-DSS Standard 3.2.1. Alternativ:

- Ta bort `CVV`-fältet från `Card`-entiteten och databasen
- Generera CVV dynamiskt och returnera det **en gång** vid kortskapande
- Spara aldrig CVV i varken klartext eller hashat format

---

## 5. Tester

### Vad som finns

| Testfil | Täcker |
|---------|--------|
| `CreateAccountHandlerTests` | Happy path, noll-saldo, SaveChanges-anrop, mapping |
| `DepositHandlerTests` | Insättning |
| `WithdrawHandlerTests` | Uttag med skydd mot negativt saldo |
| `TransferHandlerTests` | Överföring mellan konton |
| `AccountTests` | Domänentitet |
| `AuthServiceTests` | Registrering och inloggning |
| Validator-tester | CreateAccount, Deposit, Withdraw, Transfer |

### Vad som saknas

- **Integrationstester** – tester mot riktig databas (EF InMemory täcker inte SQL Server-specifika beteenden som transaktioner och constraints)
- **Controller-tester** – tester av HTTP-lager, statuskoder och headers
- **Säkerhetstester** – testa att en User inte kan komma åt en annans konto
- **Edge case-tester** – transferera till sig själv, negativa belopp, konto utan aktiv status
- **Validator-tester för Auth** – `RegisterValidator`, `LoginValidator`

### Rekommendation

Lägg till `WebApplicationFactory<Program>` för integrationstester:
```csharp
// NexaPay.Tests/Integration/AccountsIntegrationTests.cs
public class AccountsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Testar hela HTTP-flödet mot InMemory-databas
}
```

---

## 6. NuGet-paket

| Paket | Version | Kommentar |
|-------|---------|-----------|
| MediatR | 12.4.0 | Aktuell |
| AutoMapper | 16.1.1 | Aktuell |
| FluentValidation | 12.1.1 | Aktuell |
| EF Core SqlServer | 8.0.26 | Aktuell (.NET 8) |
| JwtBearer | 8.0.26 | Aktuell |
| Swashbuckle | 6.9.0 | Aktuell för .NET 8 |
| NUnit | 3.14.0 | Aktuell |
| Moq | 4.20.72 | Aktuell |
| FluentAssertions | 8.9.0 | Aktuell |
| coverlet.collector | 6.0.0 | Kodtäckningsmätning |

**Noteringar:**
- `Microsoft.Extensions.Logging.Abstractions 10.0.0` i Application-projektet är en pre-release/preview-version för .NET 10 trots att projektet riktar sig mot .NET 8. Nedgradera till `8.0.x` för konsekvens.
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` finns i **både** Infrastructure och API – det räcker i Infrastructure.

---

## 7. Sammanfattande betyg

| Område | Betyg | Kommentar |
|--------|-------|-----------|
| Arkitektur | 9/10 | Clean Architecture korrekt implementerat |
| Kodkvalitet | 8/10 | Async genomgående, bra namngivning, Result-pattern |
| Säkerhet | 5/10 | JWT-nyckel i klartext, CVV i DB, fri Admin-registrering |
| Testning | 6/10 | Bra unit-tester, men saknar integrationstester |
| Dokumentation | 7/10 | Swagger finns, men ingen API-dokumentation i koden |
| Produktionsklar | 5/10 | Bra grund men kräver säkerhetsåtgärder innan deploy |

### Prioriterad åtgärdslista

1. **[KRITISKT]** Flytta JWT-nyckeln till User Secrets / miljövariabler
2. **[KRITISKT]** Ta bort CVV-lagring – bryter mot PCI-DSS
3. **[KRITISKT]** Begränsa vem som kan registrera sig som Admin
4. **[HÖG]** Lägg till rate limiting på auth-endpoints (se punkt 3.5)
5. **[HÖG]** Lägg till rate limiting på auth-endpoints
6. **[MEDEL]** Stäng av Swagger i produktion
7. **[MEDEL]** Lägg till optimistisk concurrency (`RowVersion`) på Account
8. **[MEDEL]** Begränsa loggning av känsliga request-fält
9. **[LÅG]** Specificera CORS-origins per miljö
10. **[LÅG]** Lägg till integrationstester med `WebApplicationFactory`
