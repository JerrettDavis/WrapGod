namespace SampleTests.After;

public interface IPaymentGateway
{
    bool Charge(string customerId, decimal amount);
    Task<bool> ChargeAsync(string customerId, decimal amount);
    void Refund(string transactionId);
    event EventHandler<PaymentEventArgs> PaymentProcessed;
}

public interface IInventoryService
{
    int GetStock(string sku);
    void ReserveStock(string sku, int quantity);
    void ReleaseStock(string sku, int quantity);
    Task ReserveStockAsync(string sku, int quantity);
    bool TryReserve(string sku, int quantity, out string? reservationId);
}

public interface IAuditLog
{
    void Record(string action, string details);
    string Source { get; set; }
}

public interface IPricingEngine<TDiscount>
{
    decimal CalculatePrice(string sku, int quantity);
    TDiscount? GetDiscount(string code);
    decimal ApplyDiscount(decimal price, TDiscount discount);
}

public class PaymentEventArgs(string transactionId, decimal amount) : EventArgs
{
    public string TransactionId { get; } = transactionId;
    public decimal Amount { get; } = amount;
}

public class OrderItem
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderService(IPaymentGateway gateway, IInventoryService inventory, IAuditLog audit)
{
    public bool PlaceOrder(string customerId, List<OrderItem> items)
    {
        foreach (var item in items)
        {
            var stock = inventory.GetStock(item.Sku);
            if (stock < item.Quantity)
            {
                audit.Record("OrderFailed", $"Insufficient stock for {item.Sku}");
                return false;
            }
        }

        var total = items.Sum(i => i.Quantity * i.UnitPrice);
        var charged = gateway.Charge(customerId, total);

        if (!charged)
        {
            audit.Record("OrderFailed", "Payment declined");
            return false;
        }

        foreach (var item in items)
        {
            inventory.ReserveStock(item.Sku, item.Quantity);
        }

        audit.Record("OrderPlaced", $"Customer {customerId}, total {total:C}");
        return true;
    }

    public void CancelOrder(string transactionId, List<OrderItem> items)
    {
        gateway.Refund(transactionId);

        foreach (var item in items)
        {
            inventory.ReleaseStock(item.Sku, item.Quantity);
        }

        audit.Record("OrderCancelled", $"Transaction {transactionId}");
    }

    public async Task<bool> PlaceOrderAsync(string customerId, List<OrderItem> items)
    {
        var total = items.Sum(i => i.Quantity * i.UnitPrice);
        var charged = await gateway.ChargeAsync(customerId, total);

        if (!charged) return false;

        foreach (var item in items)
        {
            await inventory.ReserveStockAsync(item.Sku, item.Quantity);
        }

        audit.Record("OrderPlaced", $"Async order for {customerId}");
        return true;
    }
}
