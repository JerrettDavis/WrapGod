namespace SampleTests.After;

public interface IPaymentGateway
{
    bool Charge(string customerId, decimal amount);
    Task<bool> ChargeAsync(string customerId, decimal amount);
    void Refund(string transactionId);
}

public interface IInventoryService
{
    int GetStock(string sku);
    void ReserveStock(string sku, int quantity);
    void ReleaseStock(string sku, int quantity);
}

public interface IAuditLog
{
    void Record(string action, string details);
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
}
