namespace EfCoreApp;

public sealed class OrderEntity
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed record OrderSummary(int Id, string CustomerName, decimal Total, bool IsPaid);
