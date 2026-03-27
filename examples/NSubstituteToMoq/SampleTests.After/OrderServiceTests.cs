using Moq;
using Xunit;

namespace SampleTests.After;

public class OrderServiceTests
{
    private readonly Mock<IPaymentGateway> _gatewayMock = new();
    private readonly Mock<IInventoryService> _inventoryMock = new();
    private readonly Mock<IAuditLog> _auditMock = new();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _sut = new OrderService(
            _gatewayMock.Object,
            _inventoryMock.Object,
            _auditMock.Object);
    }

    [Fact]
    public void PlaceOrder_ShouldSucceed_WhenStockAndPaymentOk()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(5);
        _gatewayMock.Setup(g => g.Charge("cust-1", 20m)).Returns(true);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.True(result);
        _inventoryMock.Verify(i => i.ReserveStock("SKU-1", 2), Times.Once);
        _auditMock.Verify(
            a => a.Record("OrderPlaced", It.Is<string>(s => s.Contains("cust-1"))),
            Times.Once);
    }

    [Fact]
    public void PlaceOrder_ShouldFail_WhenInsufficientStock()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 10, UnitPrice = 5m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(3);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.False(result);
        _gatewayMock.Verify(
            g => g.Charge(It.IsAny<string>(), It.IsAny<decimal>()),
            Times.Never);
        _auditMock.Verify(
            a => a.Record("OrderFailed", It.Is<string>(s => s.Contains("Insufficient"))),
            Times.Once);
    }

    [Fact]
    public void PlaceOrder_ShouldFail_WhenPaymentDeclined()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 50m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(10);
        _gatewayMock.Setup(g => g.Charge("cust-1", 50m)).Returns(false);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.False(result);
        _inventoryMock.Verify(
            i => i.ReserveStock(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never);
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

        _gatewayMock.Verify(g => g.Refund("txn-123"), Times.Once);
        _inventoryMock.Verify(i => i.ReleaseStock("SKU-1", 2), Times.Once);
        _inventoryMock.Verify(i => i.ReleaseStock("SKU-2", 1), Times.Once);
        _auditMock.Verify(
            a => a.Record("OrderCancelled", It.Is<string>(s => s.Contains("txn-123"))),
            Times.Once);
    }
}
