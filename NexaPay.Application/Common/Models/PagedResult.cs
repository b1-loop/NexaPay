// ============================================================
// PagedResult.cs – NexaPay.Application/Common/Models
// ============================================================
// En generisk modell för paginerade resultat.
// Används när vi returnerar listor med många objekt.
//
// Varför paginering?
// Om en användare har 10 000 transaktioner vill vi inte
// returnera alla på en gång – det är långsamt och onödigt.
// Med paginering returnerar vi t.ex. 20 i taget.
//
// Exempel på användning:
//   GET /api/transactions/account/{id}?page=1&pageSize=20
//   → Returnerar de 20 första transaktionerna
//
//   GET /api/transactions/account/{id}?page=2&pageSize=20
//   → Returnerar transaktionerna 21-40
// ============================================================

namespace NexaPay.Application.Common.Models
{
    // "<T>" = generisk typ – fungerar med vilket objekt som helst
    // T.ex. PagedResult<TransactionDto> eller PagedResult<AccountDto>
    public class PagedResult<T>
    {
        // --------------------------------------------------------
        // Data
        // --------------------------------------------------------

        // Listan med objekt för den aktuella sidan
        // T.ex. de 20 transaktionerna på sida 1
        public IEnumerable<T> Items { get; set; }
            = Enumerable.Empty<T>();

        // --------------------------------------------------------
        // Pagineringsinformation
        // --------------------------------------------------------

        // Totalt antal objekt i databasen (alla sidor)
        // T.ex. 10 000 om användaren har 10 000 transaktioner
        public int TotalCount { get; set; }

        // Aktuell sida – börjar på 1
        public int Page { get; set; }

        // Antal objekt per sida
        // T.ex. 20 för att visa 20 transaktioner per sida
        public int PageSize { get; set; }

        // --------------------------------------------------------
        // Beräknade egenskaper
        // --------------------------------------------------------
        // Dessa beräknas automatiskt från ovanstående värden

        // Totalt antal sidor
        // Math.Ceiling = avrunda uppåt
        // T.ex. 10 000 transaktioner / 20 per sida = 500 sidor
        // T.ex. 21 transaktioner / 20 per sida = 2 sidor
        public int TotalPages =>
            (int)Math.Ceiling(TotalCount / (double)PageSize);

        // Om det finns en nästa sida
        // Sant om vi inte är på sista sidan
        public bool HasNextPage => Page < TotalPages;

        // Om det finns en föregående sida
        // Sant om vi inte är på första sidan
        public bool HasPreviousPage => Page > 1;

        // --------------------------------------------------------
        // Factory Method
        // --------------------------------------------------------

        // Skapa ett PagedResult-objekt
        // Används i handlers för att returnera paginerade resultat
        public static PagedResult<T> Create(
            IEnumerable<T> items,
            int totalCount,
            int page,
            int pageSize)
        {
            return new PagedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}