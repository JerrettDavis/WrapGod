using Dapper;
using Microsoft.Data.Sqlite;

namespace DapperApp;

public static class DapperOrderService
{
    public static IReadOnlyList<OrderSummary> RunScenario()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        conn.Execute("""
            CREATE TABLE Orders (
              Id INTEGER PRIMARY KEY,
              CustomerName TEXT NOT NULL,
              Total REAL NOT NULL,
              IsPaid INTEGER NOT NULL,
              CreatedUtc TEXT NOT NULL
            );
            """);

        using var tx = conn.BeginTransaction();

        conn.Execute(
            "INSERT INTO Orders (Id, CustomerName, Total, IsPaid, CreatedUtc) VALUES (@Id, @CustomerName, @Total, @IsPaid, @CreatedUtc)",
            new[]
            {
                new { Id = 1, CustomerName = "JD", Total = 19.99m, IsPaid = 0, CreatedUtc = DateTime.UtcNow },
                new { Id = 2, CustomerName = "Alex", Total = 42.50m, IsPaid = 1, CreatedUtc = DateTime.UtcNow },
                new { Id = 3, CustomerName = "Kim", Total = 5.00m, IsPaid = 0, CreatedUtc = DateTime.UtcNow }
            },
            tx);

        conn.Execute("UPDATE Orders SET IsPaid = 1 WHERE IsPaid = 0", transaction: tx);
        tx.Commit();

        return conn.Query<OrderSummary>("SELECT Id, CustomerName, Total, IsPaid FROM Orders ORDER BY Id").ToList();
    }
}
