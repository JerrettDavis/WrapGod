using NSubstitute;
using Xunit;

namespace SampleTests.Before;

public class OrderServiceTests
{
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IInventoryService _inventory = Substitute.For<IInventoryService>();
    private readonly IAuditLog _audit = Substitute.For<IAuditLog>();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _sut = new OrderService(_gateway, _inventory, _audit);
    }

    [Fact]
    public void PlaceOrder_ShouldSucceed_WhenStockAndPaymentOk()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m }
        };
        _inventory.GetStock("SKU-1").Returns(5);
        _gateway.Charge("cust-1", 20m).Returns(true);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.True(result);
        _inventory.Received(1).ReserveStock("SKU-1", 2);
        _audit.Received(1).Record("OrderPlaced", Arg.Is<string>(s => s.Contains("cust-1")));
    }

    [Fact]
    public void PlaceOrder_ShouldFail_WhenInsufficientStock()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 10, UnitPrice = 5m }
        };
        _inventory.GetStock("SKU-1").Returns(3);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.False(result);
        _gateway.DidNotReceive().Charge(Arg.Any<string>(), Arg.Any<decimal>());
        _audit.Received(1).Record("OrderFailed", Arg.Is<string>(s => s.Contains("Insufficient")));
    }

    [Fact]
    public void PlaceOrder_ShouldFail_WhenPaymentDeclined()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 50m }
        };
        _inventory.GetStock("SKU-1").Returns(10);
        _gateway.Charge("cust-1", 50m).Returns(false);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.False(result);
        _inventory.DidNotReceive().ReserveStock(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public void CancelOrder_ShouldRefundAndReleaseStock()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m },
            new() { Sku = "SKU-2", Quantity = 1, UnitPrice = 25m }
        };

        _sut.CancelOrder("txn-123", items);

        _gateway.Received(1).Refund("txn-123");
        _inventory.Received(1).ReleaseStock("SKU-1", 2);
        _inventory.Received(1).ReleaseStock("SKU-2", 1);
        _audit.Received(1).Record("OrderCancelled", Arg.Is<string>(s => s.Contains("txn-123")));
    }
}
