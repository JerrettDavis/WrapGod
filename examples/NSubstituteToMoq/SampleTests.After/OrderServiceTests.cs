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
        _sut = new OrderService(_gatewayMock.Object, _inventoryMock.Object, _auditMock.Object);
    }

    // ──────────────────────────────────────────────
    // 1. Basic Setup + Returns
    // ──────────────────────────────────────────────

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
    }

    // ──────────────────────────────────────────────
    // 2. Verify called once
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldReserveStock_WhenSuccessful()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(5);
        _gatewayMock.Setup(g => g.Charge("cust-1", 20m)).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _inventoryMock.Verify(i => i.ReserveStock("SKU-1", 2), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 3. Verify never called
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldNotCharge_WhenInsufficientStock()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 10, UnitPrice = 5m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(3);

        _sut.PlaceOrder("cust-1", items);

        _gatewayMock.Verify(g => g.Charge(It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
    }

    // ──────────────────────────────────────────────
    // 4. Argument predicate (It.Is<T>)
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldAuditWithCustomerId()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(5);
        _gatewayMock.Setup(g => g.Charge("cust-1", 20m)).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _auditMock.Verify(
            a => a.Record("OrderPlaced", It.Is<string>(s => s.Contains("cust-1"))),
            Times.Once);
    }

    // ──────────────────────────────────────────────
    // 5. Argument matcher: It.IsAny<T>
    // ──────────────────────────────────────────────

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
        _inventoryMock.Verify(i => i.ReserveStock(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    // ──────────────────────────────────────────────
    // 6. Multiple verify calls
    // ──────────────────────────────────────────────

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

    // ──────────────────────────────────────────────
    // 7. Async method mocking
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_ShouldSucceed()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 100m }
        };
        _gatewayMock.Setup(g => g.ChargeAsync("cust-1", 100m)).ReturnsAsync(true);
        _inventoryMock.Setup(i => i.ReserveStockAsync("SKU-1", 1)).Returns(Task.CompletedTask);

        var result = await _sut.PlaceOrderAsync("cust-1", items);

        Assert.True(result);
        _inventoryMock.Verify(i => i.ReserveStockAsync("SKU-1", 1), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 8. Async returns false
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_ShouldFail_WhenPaymentDeclined()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 100m }
        };
        _gatewayMock.Setup(g => g.ChargeAsync("cust-1", 100m)).ReturnsAsync(false);

        var result = await _sut.PlaceOrderAsync("cust-1", items);

        Assert.False(result);
    }

    // ──────────────────────────────────────────────
    // 9. Exception throwing
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldThrow_WhenGatewayFails()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(10);
        _gatewayMock.Setup(g => g.Charge(It.IsAny<string>(), It.IsAny<decimal>()))
            .Throws(new InvalidOperationException("Gateway unreachable"));

        Assert.Throws<InvalidOperationException>(() => _sut.PlaceOrder("cust-1", items));
    }

    // ──────────────────────────────────────────────
    // 10. Async exception throwing
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_ShouldThrow_WhenGatewayFails()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m }
        };
        _gatewayMock.Setup(g => g.ChargeAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ThrowsAsync(new TimeoutException("Timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => _sut.PlaceOrderAsync("cust-1", items));
    }

    // ──────────────────────────────────────────────
    // 11. Sequential returns (SetupSequence)
    // ──────────────────────────────────────────────

    [Fact]
    public void GetStock_ShouldReturnSequentialValues()
    {
        _inventoryMock.SetupSequence(i => i.GetStock("SKU-1"))
            .Returns(10)
            .Returns(8)
            .Returns(6);

        Assert.Equal(10, _inventoryMock.Object.GetStock("SKU-1"));
        Assert.Equal(8, _inventoryMock.Object.GetStock("SKU-1"));
        Assert.Equal(6, _inventoryMock.Object.GetStock("SKU-1"));
    }

    // ──────────────────────────────────────────────
    // 12. Callback (void method)
    // ──────────────────────────────────────────────

    [Fact]
    public void CancelOrder_ShouldTrackReleasedStock_ViaCallback()
    {
        var released = new List<(string Sku, int Qty)>();
        _inventoryMock.Setup(i => i.ReleaseStock(It.IsAny<string>(), It.IsAny<int>()))
            .Callback<string, int>((sku, qty) => released.Add((sku, qty)));

        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-A", Quantity = 3, UnitPrice = 5m },
            new() { Sku = "SKU-B", Quantity = 1, UnitPrice = 20m }
        };

        _sut.CancelOrder("txn-1", items);

        Assert.Equal(2, released.Count);
        Assert.Contains(released, r => r.Sku == "SKU-A" && r.Qty == 3);
        Assert.Contains(released, r => r.Sku == "SKU-B" && r.Qty == 1);
    }

    // ──────────────────────────────────────────────
    // 13. Returns from function (computed)
    // ──────────────────────────────────────────────

    [Fact]
    public void GetStock_ShouldReturnComputedValue()
    {
        _inventoryMock.Setup(i => i.GetStock(It.IsAny<string>()))
            .Returns((string sku) => sku == "SKU-1" ? 10 : 0);

        Assert.Equal(10, _inventoryMock.Object.GetStock("SKU-1"));
        Assert.Equal(0, _inventoryMock.Object.GetStock("SKU-OTHER"));
    }

    // ──────────────────────────────────────────────
    // 14. Property get mocking
    // ──────────────────────────────────────────────

    [Fact]
    public void AuditLog_Source_ShouldReturnConfiguredValue()
    {
        _auditMock.Setup(a => a.Source).Returns("OrderModule");

        Assert.Equal("OrderModule", _auditMock.Object.Source);
    }

    // ──────────────────────────────────────────────
    // 15. Property set verification
    // ──────────────────────────────────────────────

    [Fact]
    public void AuditLog_Source_ShouldVerifySet()
    {
        _auditMock.Object.Source = "TestModule";

        _auditMock.VerifySet(a => a.Source = "TestModule", Times.Once);
    }

    // ──────────────────────────────────────────────
    // 16. Verify exact count
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_MultipleItems_ShouldReserveEach()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m },
            new() { Sku = "SKU-2", Quantity = 2, UnitPrice = 5m }
        };
        _inventoryMock.Setup(i => i.GetStock(It.IsAny<string>())).Returns(100);
        _gatewayMock.Setup(g => g.Charge("cust-1", 20m)).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _inventoryMock.Verify(i => i.ReserveStock(It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
    }

    // ──────────────────────────────────────────────
    // 17. Verify at least once
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldCallGetStockAtLeastOnce()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m },
            new() { Sku = "SKU-2", Quantity = 1, UnitPrice = 10m }
        };
        _inventoryMock.Setup(i => i.GetStock(It.IsAny<string>())).Returns(100);
        _gatewayMock.Setup(g => g.Charge(It.IsAny<string>(), It.IsAny<decimal>())).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _inventoryMock.Verify(i => i.GetStock(It.IsAny<string>()), Times.AtLeastOnce);
    }

    // ──────────────────────────────────────────────
    // 18. ReceivedWithAnyArgs equivalent (It.IsAny for each param)
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldAudit_VerifyWithAnyArgs()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(10);
        _gatewayMock.Setup(g => g.Charge("cust-1", 10m)).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        // ReceivedWithAnyArgs -> must enumerate each param with It.IsAny<T>()
        _auditMock.Verify(a => a.Record(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    // ──────────────────────────────────────────────
    // 19. Generic interface mocking
    // ──────────────────────────────────────────────

    [Fact]
    public void PricingEngine_ShouldCalculatePrice()
    {
        var pricingMock = new Mock<IPricingEngine<decimal>>();
        pricingMock.Setup(p => p.CalculatePrice("SKU-1", 5)).Returns(45.00m);
        pricingMock.Setup(p => p.GetDiscount("SAVE10")).Returns(0.10m);
        pricingMock.Setup(p => p.ApplyDiscount(45.00m, 0.10m)).Returns(40.50m);

        Assert.Equal(45.00m, pricingMock.Object.CalculatePrice("SKU-1", 5));
        Assert.Equal(0.10m, pricingMock.Object.GetDiscount("SAVE10"));
        Assert.Equal(40.50m, pricingMock.Object.ApplyDiscount(45.00m, 0.10m));
    }

    // ──────────────────────────────────────────────
    // 20. Argument range matching (predicate)
    // ──────────────────────────────────────────────

    [Fact]
    public void Charge_ShouldOnlyMatch_InRange()
    {
        _gatewayMock.Setup(g => g.Charge(It.IsAny<string>(), It.Is<decimal>(a => a > 0 && a <= 1000)))
            .Returns(true);

        Assert.True(_gatewayMock.Object.Charge("cust-1", 500m));
        Assert.False(_gatewayMock.Object.Charge("cust-1", 1500m)); // default false, not matched
    }

    // ──────────────────────────────────────────────
    // 21. Event raising
    // ──────────────────────────────────────────────

    [Fact]
    public void Gateway_ShouldRaisePaymentEvent()
    {
        PaymentEventArgs? receivedArgs = null;
        _gatewayMock.Object.PaymentProcessed += (_, args) => receivedArgs = args;

        _gatewayMock.Raise(
            g => g.PaymentProcessed += null,
            new PaymentEventArgs("txn-1", 99.99m));

        Assert.NotNull(receivedArgs);
        Assert.Equal("txn-1", receivedArgs.TransactionId);
        Assert.Equal(99.99m, receivedArgs.Amount);
    }

    // ──────────────────────────────────────────────
    // 22. Out parameter handling
    // ──────────────────────────────────────────────

    [Fact]
    public void TryReserve_ShouldReturnReservationId()
    {
        var reservationId = "RES-001";
        _inventoryMock
            .Setup(i => i.TryReserve("SKU-1", 2, out reservationId))
            .Returns(true);

        var success = _inventoryMock.Object.TryReserve("SKU-1", 2, out var result);

        Assert.True(success);
        Assert.Equal("RES-001", result);
    }

    // ──────────────────────────────────────────────
    // 23. Arg.Do equivalent (Callback on Setup)
    // ──────────────────────────────────────────────

    [Fact]
    public void AuditRecord_ShouldCaptureDetails_ViaCallback()
    {
        var capturedDetails = new List<string>();
        _auditMock.Setup(a => a.Record("OrderFailed", It.IsAny<string>()))
            .Callback<string, string>((_, details) => capturedDetails.Add(details));

        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 100, UnitPrice = 5m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(3);

        _sut.PlaceOrder("cust-1", items);

        Assert.Single(capturedDetails);
        Assert.Contains("Insufficient", capturedDetails[0]);
    }

    // ──────────────────────────────────────────────
    // 24. Default mock returns defaults
    // ──────────────────────────────────────────────

    [Fact]
    public void DefaultMock_ShouldReturnDefaults()
    {
        // Moq Loose mocks return 0 for int, false for bool, null for ref types
        Assert.Equal(0, _inventoryMock.Object.GetStock("UNKNOWN"));
        Assert.False(_gatewayMock.Object.Charge("nobody", 0m));
    }

    // ──────────────────────────────────────────────
    // 25. Audit failure path details
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_InsufficientStock_ShouldAuditFailure()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 10, UnitPrice = 5m }
        };
        _inventoryMock.Setup(i => i.GetStock("SKU-1")).Returns(3);

        _sut.PlaceOrder("cust-1", items);

        _auditMock.Verify(
            a => a.Record("OrderFailed", It.Is<string>(s => s.Contains("Insufficient"))),
            Times.Once);
    }
}
