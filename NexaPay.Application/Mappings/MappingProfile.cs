using AutoMapper;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;

namespace NexaPay.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
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

            CreateMap<Card, CardDto>()
                .ForMember(dest => dest.MaskedCardNumber,
                    opt => opt.MapFrom(src => $"**** **** **** {src.Last4Digits}"))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()));

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
