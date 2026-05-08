namespace NexaPay.Domain.Policy
{
    public static class TransactionPolicy
    {
        // Maximum amount allowed per single transaction.
        // Applies to deposits, withdrawals, and transfers.
        // Driven by AML (Anti-Money Laundering) regulations on large cash transactions.
        public const decimal MaxTransactionAmount = 1_000_000m;

        public const int MaxDescriptionLength = 500;
    }
}
