using Microsoft.EntityFrameworkCore;

namespace EfCoreApp;

public static class EfOrderService
{
    public static IReadOnlyList<OrderSummary> RunScenario()
    {
        using var db = CreateContext();

        db.Orders.AddRange(
            new OrderEntity { Id = 1, CustomerName = "JD", Total = 19.99m, IsPaid = false, CreatedUtc = DateTime.UtcNow },
            new OrderEntity { Id = 2, CustomerName = "Alex", Total = 42.50m, IsPaid = true, CreatedUtc = DateTime.UtcNow },
            new OrderEntity { Id = 3, CustomerName = "Kim", Total = 5.00m, IsPaid = false, CreatedUtc = DateTime.UtcNow });

        db.SaveChanges();

        var unpaid = db.Orders.Where(o => !o.IsPaid).OrderBy(o => o.Id).ToList();
        foreach (var order in unpaid)
        {
            order.IsPaid = true;
        }

        db.SaveChanges();

        return db.Orders
            .OrderBy(o => o.Id)
            .Select(o => new OrderSummary(o.Id, o.CustomerName, o.Total, o.IsPaid))
            .ToList();
    }

    private static OrdersDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase($"efcore-dapper-pack-{Guid.NewGuid()}")
            .Options;

        return new OrdersDbContext(opts);
    }

    private sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
    {
        public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    }
}
