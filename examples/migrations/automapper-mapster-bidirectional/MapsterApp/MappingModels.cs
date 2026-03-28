namespace MapsterApp;

public sealed class DomainOrder
{
    public string Id { get; set; } = string.Empty;
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = [];
    public string? DiscountCode { get; set; }
    public decimal? ShippingFee { get; set; }
}

public sealed class Customer
{
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
}

public sealed class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
}

public sealed class OrderLine
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new();
    public List<OrderLineDto> Items { get; set; } = [];
    public string? DiscountCode { get; set; }
    public decimal? ShippingFee { get; set; }
    public decimal Total { get; set; }
}

public sealed class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
}

public sealed class OrderLineDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
