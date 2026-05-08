// ============================================================
// ServiceExtensions.cs – NexaPay.API
// ============================================================
// Samlar alla extension methods för tjänsteregistrering.
// Håller Program.cs ren och läsbar.
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NexaPay.API.Middleware;
using NexaPay.Infrastructure.Persistence;
using System.Threading.RateLimiting;

namespace NexaPay.API
{
    public static class ServiceExtensions
    {
        // --------------------------------------------------------
        // Identity – användare, lösenord och roller
        // --------------------------------------------------------
        public static IServiceCollection AddIdentityServices(
            this IServiceCollection services)
        {
            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                // Lösenordskrav
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;

                // Kontolåsning efter misslyckade inloggningar
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);

                // Kräv unik e-post
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // AddIdentity sätter DefaultChallengeScheme till cookie-autentisering,
            // vilket gör att oskyddade anrop omdirigeras till en inloggningssida (→ 404).
            // Vi återställer till JWT Bearer så att 401 returneras korrekt.
            services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme =
                    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme =
                    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            });

            return services;
        }

        // --------------------------------------------------------
        // API-tjänster – Controllers, Swagger, CORS
        // --------------------------------------------------------
        public static IServiceCollection AddApiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Controllers
            services.AddControllers();

            // --------------------------------------------------------
            // Rate Limiting
            // --------------------------------------------------------
            // "auth"      – max 5 req/min per IP på AuthController
            // "financial" – max 20 req/min per IP på Accounts/Cards/Transactions
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("auth", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?
                            .ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                options.AddPolicy("financial", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?
                            .ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));

                options.RejectionStatusCode =
                    StatusCodes.Status429TooManyRequests;
            });

            // --------------------------------------------------------
            // Health checks – /health returnerar API:ets status
            // --------------------------------------------------------
            services.AddHealthChecks();

            // --------------------------------------------------------
            // Swagger med JWT-stöd
            // --------------------------------------------------------
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                // ------------------------------------------------
                // Grundläggande API-information
                // ------------------------------------------------
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "NexaPay API",
                    Version = "v1",
                    Description =
                        "Ett modernt bank-API byggt med Clean Architecture.\n\n" +
                        "**Så här testar du skyddade endpoints:**\n" +
                        "1. Registrera en användare via POST /api/auth/register\n" +
                        "2. Logga in via POST /api/auth/login\n" +
                        "3. Kopiera token från svaret\n" +
                        "4. Klicka på 🔒 Authorize-knappen\n" +
                        "5. Skriv: Bearer {din token}\n" +
                        "6. Nu kan du testa alla skyddade endpoints!"
                });

                // ------------------------------------------------
                // JWT Bearer-definition
                // ------------------------------------------------
                // Berättar för Swagger att vi använder JWT Bearer
                // authentication och hur tokens ska skickas
                options.AddSecurityDefinition(
                    "Bearer",
                    new OpenApiSecurityScheme
                    {
                        // Beskrivning som visas i Authorize-dialogen
                        Description =
                            "JWT Authorization header.\n\n" +
                            "Skriv: Bearer {din token}\n\n" +
                            "Exempel: Bearer eyJhbGciOiJIUzI1NiIs...",

                        // Headerns namn
                        Name = "Authorization",

                        // Token skickas i HTTP-headern
                        In = ParameterLocation.Header,

                        // HTTP Bearer-schema
                        Type = SecuritySchemeType.Http,

                        // Bearer-schema
                        Scheme = "Bearer",

                        // JWT-format på token
                        BearerFormat = "JWT"
                    });

                // ------------------------------------------------
                // Kräv JWT-token för alla endpoints
                // ------------------------------------------------
                // Detta lägger till hänglåset 🔒 på alla endpoints
                // och skickar automatiskt med token i headers
                options.AddSecurityRequirement(
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    // Referera till vår Bearer-definition ovan
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            // Tom lista = gäller alla scopes
                            Array.Empty<string>()
                        }
                    });

                // ------------------------------------------------
                // Lös namnkonflikter
                // ------------------------------------------------
                // Förhindrar 500-fel om två klasser har samma namn
                // T.ex. CreateAccountRequest och CreateCardRequest
                options.CustomSchemaIds(type => type.FullName);
            });

            // --------------------------------------------------------
            // CORS – origins läses från konfigurationen per miljö
            // --------------------------------------------------------
            // Development (appsettings.Development.json): localhost-origins
            // Production (appsettings.json): tom lista – måste konfigureras
            var allowedOrigins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    if (allowedOrigins.Length > 0)
                    {
                        policy
                            .WithOrigins(allowedOrigins)
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    }
                    else
                    {
                        // Inga origins konfigurerade – neka allt
                        // Tvingar en explicit konfiguration i produktion
                        policy.SetIsOriginAllowed(_ => false);
                    }
                });
            });

            return services;
        }

        // --------------------------------------------------------
        // Middleware-pipeline
        // --------------------------------------------------------
        public static WebApplication UseApiMiddleware(
            this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                // Aktivera Swagger middleware
                app.UseSwagger();

                // Swagger UI med anpassade inställningar
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint(
                        "/swagger/v1/swagger.json",
                        "NexaPay API v1");

                    // Swagger UI laddas på /swagger
                    options.RoutePrefix = "swagger";

                    // ----------------------------------------
                    // Swagger UI-anpassningar
                    // ----------------------------------------

                    // Visa operationer expanderade som standard
                    // None = alla kollapsade (bättre för stora API:er)
                    options.DocExpansion(
                        Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);

                    // Visa request duration i UI
                    // Bra för att se hur snabba endpoints är
                    options.DisplayRequestDuration();

                    // Visa HTTP-metod och sökväg i sidtiteln
                    options.DocumentTitle = "NexaPay API";
                });
            }

            // 1. Global felhantering – alltid först
            app.UseMiddleware<ExceptionMiddleware>();

            // 2. HTTPS
            app.UseHttpsRedirection();

            // 3. CORS
            app.UseCors("CorsPolicy");

            // 4. Rate Limiting – före autentisering för maximal effekt
            app.UseRateLimiter();

            // 5. Authentication – vem är du?
            app.UseAuthentication();

            // 6. Authorization – vad får du göra?
            app.UseAuthorization();

            // 7. Controllers
            app.MapControllers();

            // 8. Health check endpoint – ingen autentisering, används av load balancers
            app.MapHealthChecks("/health");

            return app;
        }
    }
}