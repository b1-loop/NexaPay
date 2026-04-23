// ============================================================
// DependencyInjection.cs – NexaPay.Application
// ============================================================
// Samlar alla DI-registreringar för Application-lagret.
// Anropas från Program.cs med: builder.Services.AddApplication()
//
// Registrerar:
//   - AutoMapper 16+ (korrekt syntax med cfg + typeof)
//   - FluentValidation (hittar alla Validators automatiskt)
//   - MediatR (hittar alla Handlers automatiskt)
//   - Pipeline Behaviors (i rätt ordning!)
// ============================================================

using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NexaPay.Application.Common.Behaviors;
using NexaPay.Application.Mappings;
using System.Reflection;

namespace NexaPay.Application
{
    // "static" = extension methods måste vara i en statisk klass
    public static class DependencyInjection
    {
        // "this IServiceCollection" gör detta till en extension method
        // Det betyder att vi kan anropa den som:
        // builder.Services.AddApplication()
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            // --------------------------------------------------------
            // AutoMapper 16+
            // --------------------------------------------------------
            // Korrekt syntax för version 16+:
            // Första argumentet  = tom cfg action (krävs av signaturen)
            // Andra argumentet   = typeof(MappingProfile) pekar ut vår profil
            // AutoMapper hittar alla CreateMap<>()-regler i MappingProfile
            services.AddAutoMapper(cfg => { }, typeof(MappingProfile));

            // --------------------------------------------------------
            // FluentValidation
            // --------------------------------------------------------
            // Skannar projektet och registrerar ALLA validators automatiskt
            // T.ex. DepositCommandValidator, CreateAccountCommandValidator osv.
            // Assembly.GetExecutingAssembly() = "titta i detta projekt"
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // --------------------------------------------------------
            // MediatR
            // --------------------------------------------------------
            // Skannar projektet och registrerar ALLA handlers automatiskt
            // T.ex. DepositCommandHandler, GetAccountByIdQueryHandler osv.
            services.AddMediatR(cfg =>
            {
                // Skanna detta projekt efter alla IRequestHandler-implementationer
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

                // --------------------------------------------------------
                // Pipeline Behaviors – ORDNINGEN ÄR VIKTIG!
                // --------------------------------------------------------
                // Behaviors körs i den ordning de registreras:
                // 1. LoggingBehavior   – loggar att request kom in FÖRST
                // 2. ValidationBehavior – validerar requesten SEDAN
                // 3. Handler           – affärslogiken körs SIST
                // Om vi byter ordning loggas inte valideringsfel korrekt

                // Registrera LoggingBehavior för ALLA requests automatiskt
                cfg.AddBehavior(
                    typeof(IPipelineBehavior<,>),
                    typeof(LoggingBehavior<,>));

                // Registrera ValidationBehavior för ALLA requests automatiskt
                cfg.AddBehavior(
                    typeof(IPipelineBehavior<,>),
                    typeof(ValidationBehavior<,>));
            });

            // Returnera services för method chaining
            // T.ex. builder.Services.AddApplication().AddInfrastructure()
            return services;
        }
    }
}