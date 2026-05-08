using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;
using System.Security.Cryptography;

namespace NexaPay.Application.Features.Accounts.Commands.CreateAccount
{
    public class CreateAccountHandler
        : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<CreateAccountHandler> _logger;

        public CreateAccountHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<CreateAccountHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<AccountDto>> Handle(
            CreateAccountCommand request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Kontonummer: "SE" + 18 kryptografiskt slumpmässiga siffror.
                // Databasen har UNIQUE-constraint på AccountNumber – kollision
                // fångas av DbUpdateException i catch-blocket (astronomiskt osannolikt).
                var accountNumber = GenerateAccountNumber();

                var account = new Account
                {
                    Id = Guid.NewGuid(),
                    AccountNumber = accountNumber,
                    AccountName = request.AccountName,
                    Balance = 0,
                    AccountType = request.AccountType,
                    IsActive = true,
                    OwnerId = request.OwnerId,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Accounts.AddAsync(account);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var accountDto = _mapper.Map<AccountDto>(account);
                return Result<AccountDto>.Success(accountDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oväntat fel vid kontoskapning för ägare {OwnerId}", request.OwnerId);
                return Result<AccountDto>.Failure(
                    "Ett oväntat fel uppstod. Försök igen senare.");
            }
        }

        // Genererar "SE" + 18 siffror med CSPRNG (samma approach som CreateCardHandler).
        private static string GenerateAccountNumber()
        {
            var part1 = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000);
            var part2 = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000);
            return $"SE{part1}{part2}";
        }
    }
}
