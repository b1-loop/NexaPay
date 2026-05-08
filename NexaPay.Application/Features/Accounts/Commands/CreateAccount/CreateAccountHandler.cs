using AutoMapper;
using MediatR;
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

        public CreateAccountHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<AccountDto>> Handle(
            CreateAccountCommand request,
            CancellationToken cancellationToken)
        {
            var accountNumber = GenerateAccountNumber();

            var account = Account.Open(
                accountNumber,
                request.AccountName,
                request.AccountType,
                request.OwnerId,
                request.Currency);

            await _unitOfWork.Accounts.AddAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var accountDto = _mapper.Map<AccountDto>(account);
            return Result<AccountDto>.Success(accountDto);
        }

        // Genererar "SE" + 18 siffror med CSPRNG.
        private static string GenerateAccountNumber()
        {
            var part1 = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000);
            var part2 = RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000);
            return $"SE{part1}{part2}";
        }
    }
}
