// ============================================================
// CreateAccountHandler.cs
// NexaPay.Application/Features/Accounts/Commands/CreateAccount
// ============================================================
// Hanterar CreateAccountCommand och skapar ett nytt bankkonto.
//
// Flöde:
//   1. Ta emot CreateAccountCommand (redan validerat av pipeline)
//   2. Generera ett unikt kontonummer
//   3. Skapa ett nytt Account-objekt
//   4. Spara via UnitOfWork
//   5. Mappa till AccountDto
//   6. Returnera Result<AccountDto>
//
// IRequestHandler<TRequest, TResponse> är MediatRs interface
// för handlers. MediatR hittar denna klass automatiskt
// och kopplar den till CreateAccountCommand.
// ============================================================

using AutoMapper;
using MediatR;
using NexaPay.Application.Common.Models;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;
using NexaPay.Domain.Interfaces;

namespace NexaPay.Application.Features.Accounts.Commands.CreateAccount
{
    // IRequestHandler<CreateAccountCommand, Result<AccountDto>>
    // betyder att denna handler tar emot CreateAccountCommand
    // och returnerar Result<AccountDto>
    public class CreateAccountHandler
        : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
    {
        // IUnitOfWork ger oss tillgång till alla repositories
        // och möjlighet att spara ändringar atomärt
        private readonly IUnitOfWork _unitOfWork;

        // IMapper används för att översätta Account → AccountDto
        private readonly IMapper _mapper;

        // Konstruktor – IUnitOfWork och IMapper injiceras av DI-containern
        // Vi behöver aldrig skapa dessa objekt själva – DI sköter det
        public CreateAccountHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        // Handle är metoden MediatR anropar när ett CreateAccountCommand skickas
        // "request" = det Command som kom in med all data
        // "cancellationToken" = möjlighet att avbryta om klienten kopplar ner
        public async Task<Result<AccountDto>> Handle(
            CreateAccountCommand request,
            CancellationToken cancellationToken)
        {
            // Slå in allt i try/catch för att fånga oväntade fel
            // T.ex. databasfel, nätverksfel osv.
            try
            {
                // --------------------------------------------------------
                // Steg 1: Generera ett unikt kontonummer
                // --------------------------------------------------------
                // I verkligheten skulle detta följa bankstandard (IBAN)
                // Vi genererar ett enkelt unikt nummer för vårt syfte
                // "SE" prefix + 18 slumpmässiga siffror
                var accountNumber = GenerateAccountNumber();

                // Kontrollera att kontonumret inte redan finns
                // (extremt osannolikt men vi kollar ändå – god praxis)
                var exists = await _unitOfWork.Accounts
                    .AccountNumberExistsAsync(accountNumber);

                // Om det mot förmodan finns – generera ett nytt
                while (exists)
                {
                    accountNumber = GenerateAccountNumber();
                    exists = await _unitOfWork.Accounts
                        .AccountNumberExistsAsync(accountNumber);
                }

                // --------------------------------------------------------
                // Steg 2: Skapa Account-objektet
                // --------------------------------------------------------
                var account = new Account
                {
                    // Guid.NewGuid() genererar ett unikt ID
                    Id = Guid.NewGuid(),

                    // Kontonumret vi genererade ovan
                    AccountNumber = accountNumber,

                    // Kontonamnet från requesten
                    AccountName = request.AccountName,

                    // Nytt konto börjar alltid med 0 kr i saldo
                    // Kunden måste göra en insättning separat
                    Balance = 0,

                    // Kontotypen från requesten
                    AccountType = request.AccountType,

                    // Nytt konto är alltid aktivt från start
                    IsActive = true,

                    // Ägaren från JWT-token (satt av controllern)
                    OwnerId = request.OwnerId,

                    // Sätt tidsstämpel för när kontot skapades
                    CreatedAt = DateTime.UtcNow
                    // UtcNow = koordinerad universell tid
                    // Alltid spara tid i UTC i databasen – konvertera till
                    // lokal tid i frontend vid behov
                };

                // --------------------------------------------------------
                // Steg 3: Spara kontot via repository
                // --------------------------------------------------------
                // AddAsync lägger till kontot i EF Cores change tracker
                // men sparar det inte till databasen än
                await _unitOfWork.Accounts.AddAsync(account);

                // SaveChangesAsync sparar ALLT till databasen i en transaktion
                // Om detta misslyckas rullas allt tillbaka automatiskt
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // --------------------------------------------------------
                // Steg 4: Mappa Account → AccountDto och returnera
                // --------------------------------------------------------
                // _mapper.Map<AccountDto>(account) använder reglerna
                // vi definierade i MappingProfile.cs
                var accountDto = _mapper.Map<AccountDto>(account);

                // Returnera ett lyckat Result med AccountDto
                // Controllern tar emot detta och skickar 201 Created
                return Result<AccountDto>.Success(accountDto);
            }
            catch (Exception ex)
            {
                // Fånga oväntade fel och returnera ett misslyckat Result
                // Vi loggar inte här – LoggingBehavior sköter loggningen
                // Vi kastar inte om felet – vi returnerar ett Result.Failure
                // så att controllern kan hantera det på ett kontrollerat sätt
                return Result<AccountDto>.Failure(
                    $"Ett fel uppstod när kontot skulle skapas: {ex.Message}");
            }
        }

        // --------------------------------------------------------
        // Hjälpmetod: Generera ett unikt kontonummer
        // --------------------------------------------------------
        // Privat metod som bara används inom denna handler
        // Genererar ett kontonummer i formatet "SE" + 18 siffror
        private static string GenerateAccountNumber()
        {
            // Random.Shared är trådsäker i .NET 6+
            // Next(100000000, 999999999) genererar ett 9-siffrigt tal
            var part1 = Random.Shared.Next(100000000, 999999999);
            var part2 = Random.Shared.Next(100000000, 999999999);

            // Kombinera till ett kontonummer med SE-prefix
            // T.ex. "SE123456789987654321"
            return $"SE{part1}{part2}";
        }
    }
}