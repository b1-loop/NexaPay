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
        private readonly INotificationService _notificationService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IJwtService jwtService,
            INotificationService notificationService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _jwtService = jwtService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<Result<AuthDto>> RegisterAsync(string email, string password, string role, bool skipEmailConfirmation = false)
        {
            try
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null)
                    return Result<AuthDto>.Failure("E-postadressen används redan");

                if (!IsValidRole(role))
                    return Result<AuthDto>.Failure(
                        $"Ogiltig roll: {role}. " +
                        $"Giltiga roller är: {Roles.Admin}, {Roles.BankManager}, " +
                        $"{Roles.Teller}, {Roles.Auditor}, {Roles.User}");

                var user = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = skipEmailConfirmation
                };

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Result<AuthDto>.Failure(errors);
                }

                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));

                await _userManager.AddToRoleAsync(user, role);

                if (!skipEmailConfirmation)
                {
                    var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await _notificationService.NotifyEmailConfirmationAsync(email, confirmationToken);
                }

                return Result<AuthDto>.Success(new AuthDto
                {
                    Token = string.Empty,
                    Email = user.Email!,
                    Role = role,
                    ExpiresAt = DateTime.UtcNow,
                    RequiresEmailConfirmation = !skipEmailConfirmation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid registrering för e-post {Email}", email);
                return Result<AuthDto>.Failure("Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        public async Task<Result<AuthDto>> LoginAsync(string email, string password)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return Result<AuthDto>.Failure("Felaktig e-post eller lösenord");

                if (await _userManager.IsLockedOutAsync(user))
                    return Result<AuthDto>.Failure("Kontot är tillfälligt låst. Försök igen senare.");

                var passwordValid = await _userManager.CheckPasswordAsync(user, password);
                if (!passwordValid)
                {
                    await _userManager.AccessFailedAsync(user);
                    return Result<AuthDto>.Failure("Felaktig e-post eller lösenord");
                }

                if (!user.EmailConfirmed)
                    return Result<AuthDto>.Failure(
                        "E-postadressen är inte bekräftad. Kontrollera din inkorg och bekräfta ditt konto.");

                await _userManager.ResetAccessFailedCountAsync(user);

                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? Roles.User;

                var tokenResult = _jwtService.GenerateToken(user.Id, user.Email!, role);

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
                return Result<AuthDto>.Failure("Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        public async Task<Result> ConfirmEmailAsync(string userId, string token)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Result.Failure("Ogiltigt användar-ID eller token.");

                // Redan bekräftad – returnera success direkt (idempotent)
                if (user.EmailConfirmed)
                    return Result.Success();

                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("E-postbekräftelse misslyckades för {UserId}: {Errors}", userId, errors);
                    return Result.Failure("Ogiltig eller utgången bekräftelselänk.");
                }

                _logger.LogInformation("E-post bekräftad för {UserId}", userId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid e-postbekräftelse för {UserId}", userId);
                return Result.Failure("Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        public async Task<Result> ForgotPasswordAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                // Avslöja inte om e-postadressen finns – returnera alltid success
                if (user != null && user.EmailConfirmed)
                {
                    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _notificationService.NotifyPasswordResetAsync(email, resetToken);
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid lösenordsåterställning för {Email}", email);
                return Result.Failure("Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                    return Result.Failure("Ogiltig e-postadress eller token.");

                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Lösenordsåterställning misslyckades för {Email}: {Errors}", email, errors);
                    return Result.Failure("Ogiltig eller utgången återställningslänk.");
                }

                _logger.LogInformation("Lösenord återställt för {Email}", email);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid lösenordsåterställning för {Email}", email);
                return Result.Failure("Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return Result.Failure("Användaren hittades inte.");

                var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Result.Failure(errors);
                }

                _logger.LogInformation("Lösenord bytt för {UserId}", userId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid lösenordsbyte för {UserId}", userId);
                return Result.Failure("Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        private static bool IsValidRole(string role) =>
            role == Roles.Admin ||
            role == Roles.BankManager ||
            role == Roles.Teller ||
            role == Roles.Auditor ||
            role == Roles.User;
    }
}
