// ============================================================
// AuthService.cs – NexaPay.Infrastructure/Identity
// ============================================================
// Implementerar IAuthService från Application-lagret.
// Det är HÄR Identity-koden lever – inte i controllers!
//
// AuthService känner till:
//   - UserManager (Identity) – hanterar användare
//   - RoleManager (Identity) – hanterar roller
//   - IJwtService – genererar tokens
//
// Application-lagret vet INGENTING om detta –
// det känner bara till IAuthService-interfacet.
// ============================================================

using Microsoft.AspNetCore.Identity;
using NexaPay.Application.Common.Interfaces;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;

namespace NexaPay.Infrastructure.Identity
{
    public class AuthService : IAuthService
    {
        // UserManager hanterar användare i Identity
        private readonly UserManager<IdentityUser> _userManager;

        // RoleManager hanterar roller i Identity
        private readonly RoleManager<IdentityRole> _roleManager;

        // IJwtService genererar JWT-tokens
        private readonly IJwtService _jwtService;

        public AuthService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtService jwtService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtService = jwtService;
        }

        // --------------------------------------------------------
        // Registrera ny användare
        // --------------------------------------------------------
        public async Task<Result<AuthDto>> RegisterAsync(
            string email,
            string password,
            bool isAdmin)
        {
            try
            {
                // Kontrollera om e-posten redan används
                var existingUser = await _userManager
                    .FindByEmailAsync(email);

                if (existingUser != null)
                    return Result<AuthDto>.Failure(
                        "E-postadressen används redan");

                // Skapa ny användare
                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    // EmailConfirmed = true för enkelhetens skull
                    // I produktion skulle vi skicka bekräftelsemail
                    EmailConfirmed = true
                };

                // CreateAsync hashar lösenordet automatiskt
                var result = await _userManager
                    .CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    // Samla alla felmeddelanden från Identity
                    var errors = string.Join(", ",
                        result.Errors.Select(e => e.Description));

                    return Result<AuthDto>.Failure(errors);
                }

                // Tilldela roll
                var roleName = isAdmin ? "Admin" : "User";

                // Skapa rollen om den inte finns
                if (!await _roleManager.RoleExistsAsync(roleName))
                    await _roleManager.CreateAsync(
                        new IdentityRole(roleName));

                // Tilldela rollen till användaren
                await _userManager.AddToRoleAsync(user, roleName);

                // Generera JWT-token
                var token = _jwtService.GenerateToken(
                    user.Id,
                    user.Email!,
                    roleName);

                // Returnera AuthDto med token och användarinfo
                return Result<AuthDto>.Success(new AuthDto
                {
                    Token = token,
                    Email = user.Email!,
                    Role = roleName,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                });
            }
            catch (Exception ex)
            {
                return Result<AuthDto>.Failure(
                    $"Ett fel uppstod vid registrering: {ex.Message}");
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
                // Hitta användaren via e-post
                var user = await _userManager
                    .FindByEmailAsync(email);

                if (user == null)
                    // Generiskt felmeddelande av säkerhetsskäl
                    // Vi avslöjar inte om e-posten finns eller inte
                    return Result<AuthDto>.Failure(
                        "Felaktig e-post eller lösenord");

                // Kontrollera lösenordet mot det hashade värdet
                var passwordValid = await _userManager
                    .CheckPasswordAsync(user, password);

                if (!passwordValid)
                    return Result<AuthDto>.Failure(
                        "Felaktig e-post eller lösenord");

                // Hämta användarens roller
                var roles = await _userManager
                    .GetRolesAsync(user);

                var role = roles.FirstOrDefault() ?? "User";

                // Generera JWT-token
                var token = _jwtService.GenerateToken(
                    user.Id,
                    user.Email!,
                    role);

                return Result<AuthDto>.Success(new AuthDto
                {
                    Token = token,
                    Email = user.Email!,
                    Role = role,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                });
            }
            catch (Exception ex)
            {
                return Result<AuthDto>.Failure(
                    $"Ett fel uppstod vid inloggning: {ex.Message}");
            }
        }
    }
}