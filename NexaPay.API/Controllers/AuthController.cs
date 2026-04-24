// ============================================================
// AuthController.cs – NexaPay.API/Controllers
// ============================================================
// Hanterar registrering och inloggning.
// Returnerar JWT-tokens som klienten använder för
// alla efterföljande anrop.
//
// Endpoints:
//   POST /api/auth/register  ← Registrera ny användare
//   POST /api/auth/login     ← Logga in och få JWT-token
// ============================================================

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NexaPay.Infrastructure.Identity;

namespace NexaPay.API.Controllers
{
    // [ApiController] aktiverar automatisk modellvalidering
    [ApiController]

    // "api/[controller]" = "api/auth"
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        // UserManager hanterar användare – skapa, hitta, validera lösenord
        private readonly UserManager<IdentityUser> _userManager;

        // RoleManager hanterar roller – skapa och tilldela roller
        private readonly RoleManager<IdentityRole> _roleManager;

        // Vår JwtService för att generera tokens
        private readonly IJwtService _jwtService;

        public AuthController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtService jwtService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtService = jwtService;
        }

        // --------------------------------------------------------
        // POST api/auth/register
        // --------------------------------------------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request)
        {
            // Kontrollera om e-posten redan används
            var existingUser = await _userManager
                .FindByEmailAsync(request.Email);

            if (existingUser != null)
            {
                return BadRequest(new
                {
                    message = "E-postadressen används redan"
                });
            }

            // Skapa en ny IdentityUser
            var user = new IdentityUser
            {
                UserName = request.Email,
                Email = request.Email,
                // EmailConfirmed = true för enkelhetens skull
                // I produktion skulle vi skicka bekräftelsemail
                EmailConfirmed = true
            };

            // CreateAsync hashar lösenordet automatiskt
            // Vi lagrar ALDRIG lösenord i klartext!
            var result = await _userManager.CreateAsync(
                user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .Select(e => e.Description)
                    .ToList();

                return BadRequest(new { errors });
            }

            // --------------------------------------------------------
            // Tilldela roll
            // --------------------------------------------------------
            var roleName = request.IsAdmin ? "Admin" : "User";

            // Skapa rollen om den inte finns
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(
                    new IdentityRole(roleName));
            }

            // Tilldela rollen till användaren
            await _userManager.AddToRoleAsync(user, roleName);

            return Ok(new
            {
                message = $"Användaren {request.Email} " +
                          $"registrerades framgångsrikt",
                role = roleName
            });
        }

        // --------------------------------------------------------
        // POST api/auth/login
        // --------------------------------------------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            // Hitta användaren via e-post
            var user = await _userManager
                .FindByEmailAsync(request.Email);

            if (user == null)
            {
                // Generiskt felmeddelande av säkerhetsskäl
                // Vi avslöjar inte om e-posten finns eller inte
                return Unauthorized(new
                {
                    message = "Felaktig e-post eller lösenord"
                });
            }

            // Kontrollera lösenordet mot det hashade värdet
            var passwordValid = await _userManager
                .CheckPasswordAsync(user, request.Password);

            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message = "Felaktig e-post eller lösenord"
                });
            }

            // Hämta användarens roller
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";

            // Generera JWT-token med JwtService
            var token = _jwtService.GenerateToken(
                user.Id,
                user.Email!,
                role);

            return Ok(new
            {
                token,
                email = user.Email,
                role,
                expiresAt = DateTime.UtcNow.AddHours(24),
                message = "Inloggning lyckades"
            });
        }
    }

    // --------------------------------------------------------
    // Request-modeller för AuthController
    // --------------------------------------------------------
    public record RegisterRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        // Om true skapas användaren med Admin-rollen
        public bool IsAdmin { get; init; } = false;
    }

    public record LoginRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }
}