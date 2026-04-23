// ============================================================
// MappingProfile.cs – NexaPay.Application/Mappings
// ============================================================
// Definierar reglerna för hur entiteter mappas till DTOs.
// AutoMapper använder dessa regler när vi anropar:
//   _mapper.Map<AccountDto>(account)
//
// Profile är AutoMappers basklass för mapping-konfiguration.
// Vi registrerar alla mappings i konstruktorn.
// ============================================================

using AutoMapper;
using NexaPay.Application.DTOs;
using NexaPay.Domain.Entities;

namespace NexaPay.Application.Mappings
{
    // Ärver från Profile – AutoMappers basklass
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // --------------------------------------------------------
            // Account → AccountDto
            // --------------------------------------------------------
            // CreateMap<Källa, Destination>()
            // AutoMapper kopierar automatiskt properties med samma namn
            // Vi behöver bara konfigurera de som SKILJER sig åt
            CreateMap<Account, AccountDto>()
                // AccountType är en enum i entiteten men vi vill ha
                // en sträng i DTO så klienten ser "Savings" istället för "1"
                .ForMember(
                    dest => dest.AccountType,
                    opt => opt.MapFrom(src => src.AccountType.ToString()));
            // Alla andra properties (Id, AccountNumber, Balance osv.)
            // mappas automatiskt eftersom de har samma namn

            // --------------------------------------------------------
            // Card → CardDto
            // --------------------------------------------------------
            CreateMap<Card, CardDto>()
                // Kortnumret ska maskeras – visa bara sista 4 siffrorna
                // T.ex. "4532123456789010" → "**** **** **** 9010"
                // Vi använder Substring() istället för [^4..] range syntax
                // eftersom AutoMapper använder expression trees internt
                // och range syntax inte stöds där
                .ForMember(
                    dest => dest.MaskedCardNumber,
                    opt => opt.MapFrom(src =>
                        // Kontrollera att kortnumret är tillräckligt långt
                        src.CardNumber.Length >= 4
                            // Substring(Length - 4) = de sista 4 tecknen
                            // T.ex. "4532123456789010".Substring(12) = "9010"
                            ? $"**** **** **** {src.CardNumber.Substring(src.CardNumber.Length - 4)}"
                            // Om kortnumret är kortare än 4 tecken – visa hela
                            // Detta bör aldrig hända i produktion
                            : src.CardNumber))
                // CardStatus enum → sträng
                // Klienten ser "Active" istället för "0"
                .ForMember(
                    dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()));

            // --------------------------------------------------------
            // Transaction → TransactionDto
            // --------------------------------------------------------
            CreateMap<Transaction, TransactionDto>()
                // TransactionType enum → sträng
                // Klienten ser "Deposit" istället för "0"
                .ForMember(
                    dest => dest.Type,
                    opt => opt.MapFrom(src => src.Type.ToString()));
            // Alla andra properties mappas automatiskt
        }
    }
}