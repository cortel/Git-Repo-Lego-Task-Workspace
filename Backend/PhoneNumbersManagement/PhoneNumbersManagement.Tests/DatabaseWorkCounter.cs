using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PhoneNumbersManagement.Tests;

public sealed class DatabaseWorkCounter : DbTransactionInterceptor
{
    public int TransactionsStarted { get; private set; }

    public void Reset() => TransactionsStarted = 0;

    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        TransactionsStarted++;
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
        TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        TransactionsStarted++;
        return ValueTask.FromResult(result);
    }
}
