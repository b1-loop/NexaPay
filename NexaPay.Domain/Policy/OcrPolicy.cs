namespace NexaPay.Domain.Policy
{
    // Regler för OCR-referensnummer på fakturabetalningar.
    // OCR-numret har en kontrollsiffra (sista siffran) beräknad med mod-10 (Luhn).
    public static class OcrPolicy
    {
        public const int MinLength = 2;
        public const int MaxLength = 25;

        // Giltigt OCR: endast siffror, längd inom intervallet och korrekt
        // mod-10-kontrollsiffra (Luhn) över hela talet inklusive kontrollsiffran.
        public static bool IsValid(string? ocr)
        {
            if (string.IsNullOrWhiteSpace(ocr))
                return false;

            ocr = ocr.Trim();

            if (ocr.Length < MinLength || ocr.Length > MaxLength)
                return false;

            if (!ocr.All(char.IsDigit))
                return false;

            return PassesMod10(ocr);
        }

        // Standard Luhn-kontroll: varannan siffra från höger dubbleras,
        // siffersumman ska vara jämnt delbar med 10.
        private static bool PassesMod10(string digits)
        {
            var sum = 0;
            var doubleNext = false;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var n = digits[i] - '0';
                if (doubleNext)
                {
                    n *= 2;
                    if (n > 9) n -= 9;
                }
                sum += n;
                doubleNext = !doubleNext;
            }
            return sum % 10 == 0;
        }
    }
}
