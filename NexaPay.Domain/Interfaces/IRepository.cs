// Generisk IRepository<T> borttagen – varje aggregat-rot har nu ett
// eget avsiktsavslöjande interface (IAccountRepository, ICardRepository,
// ITransactionRepository) utan Update/Delete-läckage in i domänen.
