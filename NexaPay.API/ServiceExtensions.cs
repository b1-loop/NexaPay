// ============================================================
// ServiceExtensions.cs – NexaPay.API
// ============================================================
// Samlar alla extension methods för tjänsteregistrering.
// Håller Program.cs ren och läsbar.
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NexaPay.API.Middleware;
using NexaPay.Infrastructure.Persistence;

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

            return services;
        }

        // --------------------------------------------------------
        // API-tjänster – Controllers, Swagger, CORS
        // --------------------------------------------------------
        public static IServiceCollection AddApiServices(
            this IServiceCollection services)
        {
            // Controllers
            services.AddControllers();

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
            // CORS
            // --------------------------------------------------------
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
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
            app.UseCors("AllowAll");

            // 4. Authentication – vem är du?
            app.UseAuthentication();

            // 5. Authorization – vad får du göra?
            app.UseAuthorization();

            // 6. Controllers
            app.MapControllers();

            return app;
        }
    }
}