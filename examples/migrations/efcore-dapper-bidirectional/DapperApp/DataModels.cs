namespace DapperApp;

public sealed class OrderRow
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
}

public sealed class OrderSummary
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
}
