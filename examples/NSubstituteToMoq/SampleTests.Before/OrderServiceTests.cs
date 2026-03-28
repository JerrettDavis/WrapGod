using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
        _inventory.GetStock("SKU-1").Returns(5);
        _gateway.Charge("cust-1", 20m).Returns(true);

        var result = _sut.PlaceOrder("cust-1", items);

        Assert.True(result);
    }

    // ──────────────────────────────────────────────
    // 2. Verify called once (Received(1))
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldReserveStock_WhenSuccessful()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m }
        };
        _inventory.GetStock("SKU-1").Returns(5);
        _gateway.Charge("cust-1", 20m).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _inventory.Received(1).ReserveStock("SKU-1", 2);
    }

    // ──────────────────────────────────────────────
    // 3. Verify never called (DidNotReceive)
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldNotCharge_WhenInsufficientStock()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 10, UnitPrice = 5m }
        };
        _inventory.GetStock("SKU-1").Returns(3);

        _sut.PlaceOrder("cust-1", items);

        _gateway.DidNotReceive().Charge(Arg.Any<string>(), Arg.Any<decimal>());
    }

    // ──────────────────────────────────────────────
    // 4. Argument predicate (Arg.Is<T>)
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldAuditWithCustomerId()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m }
        };
        _inventory.GetStock("SKU-1").Returns(5);
        _gateway.Charge("cust-1", 20m).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _audit.Received(1).Record("OrderPlaced", Arg.Is<string>(s => s.Contains("cust-1")));
    }

    // ──────────────────────────────────────────────
    // 5. Argument matcher: Arg.Any<T>
    // ──────────────────────────────────────────────

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

        _gateway.Received(1).Refund("txn-123");
        _inventory.Received(1).ReleaseStock("SKU-1", 2);
        _inventory.Received(1).ReleaseStock("SKU-2", 1);
        _audit.Received(1).Record("OrderCancelled", Arg.Is<string>(s => s.Contains("txn-123")));
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
        _gateway.ChargeAsync("cust-1", 100m).Returns(true);
        _inventory.ReserveStockAsync("SKU-1", 1).Returns(Task.CompletedTask);

        var result = await _sut.PlaceOrderAsync("cust-1", items);

        Assert.True(result);
        await _inventory.Received(1).ReserveStockAsync("SKU-1", 1);
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
        _gateway.ChargeAsync("cust-1", 100m).Returns(false);

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
        _inventory.GetStock("SKU-1").Returns(10);
        _gateway.Charge(Arg.Any<string>(), Arg.Any<decimal>())
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
        _gateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
            .ThrowsAsync(new TimeoutException("Timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => _sut.PlaceOrderAsync("cust-1", items));
    }

    // ──────────────────────────────────────────────
    // 11. Sequential returns
    // ──────────────────────────────────────────────

    [Fact]
    public void GetStock_ShouldReturnSequentialValues()
    {
        _inventory.GetStock("SKU-1").Returns(10, 8, 6);

        Assert.Equal(10, _inventory.GetStock("SKU-1"));
        Assert.Equal(8, _inventory.GetStock("SKU-1"));
        Assert.Equal(6, _inventory.GetStock("SKU-1"));
    }

    // ──────────────────────────────────────────────
    // 12. Callback with When/Do (void method)
    // ──────────────────────────────────────────────

    [Fact]
    public void CancelOrder_ShouldTrackReleasedStock_ViaCallback()
    {
        var released = new List<(string Sku, int Qty)>();
        _inventory.When(i => i.ReleaseStock(Arg.Any<string>(), Arg.Any<int>()))
            .Do(ci => released.Add((ci.ArgAt<string>(0), ci.ArgAt<int>(1))));

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
        _inventory.GetStock(Arg.Any<string>())
            .Returns(ci => ci.ArgAt<string>(0) == "SKU-1" ? 10 : 0);

        Assert.Equal(10, _inventory.GetStock("SKU-1"));
        Assert.Equal(0, _inventory.GetStock("SKU-OTHER"));
    }

    // ──────────────────────────────────────────────
    // 14. Property get mocking
    // ──────────────────────────────────────────────

    [Fact]
    public void AuditLog_Source_ShouldReturnConfiguredValue()
    {
        _audit.Source.Returns("OrderModule");

        Assert.Equal("OrderModule", _audit.Source);
    }

    // ──────────────────────────────────────────────
    // 15. Property set verification
    // ──────────────────────────────────────────────

    [Fact]
    public void AuditLog_Source_ShouldVerifySet()
    {
        _audit.Source = "TestModule";

        _audit.Received(1).Source = "TestModule";
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
        _inventory.GetStock(Arg.Any<string>()).Returns(100);
        _gateway.Charge("cust-1", 20m).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _inventory.Received(2).ReserveStock(Arg.Any<string>(), Arg.Any<int>());
    }

    // ──────────────────────────────────────────────
    // 17. Verify at least once (Received())
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldCallGetStockAtLeastOnce()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m },
            new() { Sku = "SKU-2", Quantity = 1, UnitPrice = 10m }
        };
        _inventory.GetStock(Arg.Any<string>()).Returns(100);
        _gateway.Charge(Arg.Any<string>(), Arg.Any<decimal>()).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _inventory.Received().GetStock(Arg.Any<string>());
    }

    // ──────────────────────────────────────────────
    // 18. ReceivedWithAnyArgs
    // ──────────────────────────────────────────────

    [Fact]
    public void PlaceOrder_ShouldAudit_ReceivedWithAnyArgs()
    {
        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 1, UnitPrice = 10m }
        };
        _inventory.GetStock("SKU-1").Returns(10);
        _gateway.Charge("cust-1", 10m).Returns(true);

        _sut.PlaceOrder("cust-1", items);

        _audit.ReceivedWithAnyArgs().Record(default!, default!);
    }

    // ──────────────────────────────────────────────
    // 19. Generic interface mocking
    // ──────────────────────────────────────────────

    [Fact]
    public void PricingEngine_ShouldCalculatePrice()
    {
        var pricing = Substitute.For<IPricingEngine<decimal>>();
        pricing.CalculatePrice("SKU-1", 5).Returns(45.00m);
        pricing.GetDiscount("SAVE10").Returns(0.10m);
        pricing.ApplyDiscount(45.00m, 0.10m).Returns(40.50m);

        Assert.Equal(45.00m, pricing.CalculatePrice("SKU-1", 5));
        Assert.Equal(0.10m, pricing.GetDiscount("SAVE10"));
        Assert.Equal(40.50m, pricing.ApplyDiscount(45.00m, 0.10m));
    }

    // ──────────────────────────────────────────────
    // 20. Argument range matching (predicate)
    // ──────────────────────────────────────────────

    [Fact]
    public void Charge_ShouldOnlyMatch_InRange()
    {
        _gateway.Charge(Arg.Any<string>(), Arg.Is<decimal>(a => a > 0 && a <= 1000))
            .Returns(true);

        Assert.True(_gateway.Charge("cust-1", 500m));
        Assert.False(_gateway.Charge("cust-1", 1500m)); // default false, not matched
    }

    // ──────────────────────────────────────────────
    // 21. Event raising
    // ──────────────────────────────────────────────

    [Fact]
    public void Gateway_ShouldRaisePaymentEvent()
    {
        PaymentEventArgs? receivedArgs = null;
        _gateway.PaymentProcessed += (_, args) => receivedArgs = args;

        _gateway.PaymentProcessed += Raise.EventWith(
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
        _inventory.TryReserve("SKU-1", 2, out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[2] = "RES-001";
                return true;
            });

        var success = _inventory.TryReserve("SKU-1", 2, out var reservationId);

        Assert.True(success);
        Assert.Equal("RES-001", reservationId);
    }

    // ──────────────────────────────────────────────
    // 23. Arg.Do inline capture
    // ──────────────────────────────────────────────

    [Fact]
    public void AuditRecord_ShouldCaptureDetails_ViaArgDo()
    {
        var capturedDetails = new List<string>();
        _audit.Record("OrderFailed", Arg.Do<string>(d => capturedDetails.Add(d)));

        var items = new List<OrderItem>
        {
            new() { Sku = "SKU-1", Quantity = 100, UnitPrice = 5m }
        };
        _inventory.GetStock("SKU-1").Returns(3);

        _sut.PlaceOrder("cust-1", items);

        Assert.Single(capturedDetails);
        Assert.Contains("Insufficient", capturedDetails[0]);
    }

    // ──────────────────────────────────────────────
    // 24. Default substitute returns default
    // ──────────────────────────────────────────────

    [Fact]
    public void DefaultSubstitute_ShouldReturnDefaults()
    {
        // NSubstitute returns 0 for int, false for bool, null for ref types
        Assert.Equal(0, _inventory.GetStock("UNKNOWN"));
        Assert.False(_gateway.Charge("nobody", 0m));
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
        _inventory.GetStock("SKU-1").Returns(3);

        _sut.PlaceOrder("cust-1", items);

        _audit.Received(1).Record("OrderFailed", Arg.Is<string>(s => s.Contains("Insufficient")));
    }
}
