// ============================================================
// AuthService.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Implementerar IAuthService från Application-lagret.
// Hanterar registrering och inloggning med ASP.NET Identity.
//
// Stödjer nu 5 roller:
//   Admin, BankManager, Teller, Auditor, User
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Constants;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Infrastructure.Identity
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtService jwtService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtService = jwtService;
            _logger = logger;
        }

        // --------------------------------------------------------
        // Registrera ny användare
        // --------------------------------------------------------
        public async Task<Result<AuthDto>> RegisterAsync(
            string email,
            string password,
            string role)
        {
            try
            {
                // Kontrollera om e-posten redan används
                var existingUser = await _userManager
                    .FindByEmailAsync(email);

                if (existingUser != null)
                    return Result<AuthDto>.Failure(
                        "E-postadressen används redan");

                // Validera att rollen är giltig
                // Vi kontrollerar mot våra definierade roller
                if (!IsValidRole(role))
                    return Result<AuthDto>.Failure(
                        $"Ogiltig roll: {role}. " +
                        $"Giltiga roller är: {Roles.Admin}, " +
                        $"{Roles.BankManager}, {Roles.Teller}, " +
                        $"{Roles.Auditor}, {Roles.User}");

                // Skapa ny användare
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                // CreateAsync hashar lösenordet automatiskt
                var result = await _userManager
                    .CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    var errors = string.Join(", ",
                        result.Errors.Select(e => e.Description));
                    return Result<AuthDto>.Failure(errors);
                }

                // Skapa rollen om den inte finns
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(
                        new IdentityRole(role));

                // Tilldela rollen till användaren
                await _userManager.AddToRoleAsync(user, role);

                var tokenResult = _jwtService.GenerateToken(
                    user.Id,
                    user.Email!,
                    role);

                return Result<AuthDto>.Success(new AuthDto
                {
                    Token = tokenResult.Token,
                    Email = user.Email!,
                    Role = role,
                    ExpiresAt = tokenResult.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid registrering för e-post {Email}", email);
                return Result<AuthDto>.Failure(
                    "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        // --------------------------------------------------------
        // Logga in befintlig användare
        // --------------------------------------------------------
        public async Task<Result<AuthDto>> LoginAsync(
            string email,
            string password)
        {
            try
            {
                var user = await _userManager
                    .FindByEmailAsync(email);

                if (user == null)
                    return Result<AuthDto>.Failure(
                        "Felaktig e-post eller lösenord");

                // Kontrollera lockout innan lösenordsverifiering
                if (await _userManager.IsLockedOutAsync(user))
                    return Result<AuthDto>.Failure(
                        "Kontot är tillfälligt låst. Försök igen senare.");

                var passwordValid = await _userManager
                    .CheckPasswordAsync(user, password);

                if (!passwordValid)
                {
                    // Öka räknaren för misslyckade försök
                    // Vid 5 misslyckanden låses kontot i 15 minuter
                    await _userManager.AccessFailedAsync(user);
                    return Result<AuthDto>.Failure(
                        "Felaktig e-post eller lösenord");
                }

                // Återställ räknaren vid lyckad inloggning
                await _userManager.ResetAccessFailedCountAsync(user);

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? Roles.User;

                var tokenResult = _jwtService.GenerateToken(
                    user.Id,
                    user.Email!,
                    role);

                return Result<AuthDto>.Success(new AuthDto
                {
                    Token = tokenResult.Token,
                    Email = user.Email!,
                    Role = role,
                    ExpiresAt = tokenResult.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid inloggning för e-post {Email}", email);
                return Result<AuthDto>.Failure(
                    "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        // --------------------------------------------------------
        // Hjälpmetod – validera rollnamn
        // --------------------------------------------------------
        // Kontrollerar att den angivna rollen är en av våra
        // definierade roller – skyddar mot ogiltiga rollnamn
        private static bool IsValidRole(string role)
        {
            return role == Roles.Admin ||
                   role == Roles.BankManager ||
                   role == Roles.Teller ||
                   role == Roles.Auditor ||
                   role == Roles.User;
        }
    }
}