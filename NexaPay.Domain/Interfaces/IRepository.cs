// Generisk repository-bas finns nu i IGenericRepository<T>.
// IAccountRepository, ICardRepository och ITransactionRepository
// ärver från IGenericRepository<T> och lägger till entitet-specifika
// queries. Update/Remove exponeras inte – aggregaten har egna
// intention-revealing metoder (Account.Close(), Card.Block() osv).
