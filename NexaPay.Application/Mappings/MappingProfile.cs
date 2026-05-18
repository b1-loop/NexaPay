// ============================================================
// MappingProfile.cs – NexaPay.Application/Mappings
// ============================================================
// AutoMapper-konfiguration som översätter domän-entiteter till
// DTOs (Data Transfer Objects) som API:et skickar ut till klienter.
//
// Varför mappa istället för att exponera entiteter direkt?
//   * Domänen får behålla sina värdeobjekt (Money) och privata
//     setters utan att JSON-serialiseras med interna detaljer.
//   * Vi maskerar känsliga fält (kortnummer → "**** **** **** 1234").
//   * Enums serialiseras som läsbara strängar istället för siffror.
//
// AutoMapper registreras i Application/DependencyInjection.cs
// och anropas i handlers via IMapper.
// ============================================================

using AutoMapper;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;

namespace NexaPay.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // --------------------------------------------------------
            // Account → AccountDto
            // --------------------------------------------------------
            // Plattar ut Money-värdeobjektet till två fält (Balance + Currency)
            // och konverterar enum-värden till strängar för JSON-läsbarhet.
            CreateMap<Account, AccountDto>()
                .ForMember(dest => dest.Balance,
                    opt => opt.MapFrom(src => src.Balance.Amount))
                .ForMember(dest => dest.Currency,
                    opt => opt.MapFrom(src => src.Balance.Currency.ToString()))
                .ForMember(dest => dest.AccountType,
                    opt => opt.MapFrom(src => src.AccountType.ToString()))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.OwnerId,
                    opt => opt.MapFrom(src => src.OwnerId));

            // --------------------------------------------------------
            // Card → CardDto
            // --------------------------------------------------------
            // Exponerar ALDRIG hela kortnumret. CardToken stannar internt,
            // men vi visar en maskad version med endast de fyra sista siffrorna
            // för att användaren ska kunna identifiera sitt kort.
            CreateMap<Card, CardDto>()
                .ForMember(dest => dest.MaskedCardNumber,
                    opt => opt.MapFrom(src => $"**** **** **** {src.Last4Digits}"))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()));

            // --------------------------------------------------------
            // Transaction → TransactionDto
            // --------------------------------------------------------
            CreateMap<Transaction, TransactionDto>()
                .ForMember(dest => dest.Amount,
                    opt => opt.MapFrom(src => src.Amount.Amount))
                .ForMember(dest => dest.Currency,
                    opt => opt.MapFrom(src => src.Amount.Currency.ToString()))
                .ForMember(dest => dest.BalanceAfterTransaction,
                    opt => opt.MapFrom(src => src.BalanceAfterTransaction.Amount))
                .ForMember(dest => dest.Type,
                    opt => opt.MapFrom(src => src.Type.ToString()));
        }
    }
}
