namespace AutoMapperApp;

public static class AutoMapperBridge
{
    public static OrderDto ToOrderDto(DomainOrder source)
    {
        return new OrderDto
        {
            OrderId = source.Id,
            CustomerName = source.Customer.Name,
            Address = new AddressDto
            {
                Street = source.Customer.Address.Street,
                City = source.Customer.Address.City,
                State = source.Customer.Address.State
            },
            Items = source.Lines.Select(l => new OrderLineDto
            {
                Sku = l.Sku,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.Quantity * l.UnitPrice
            }).ToList(),
            DiscountCode = source.DiscountCode,
            ShippingFee = source.ShippingFee,
            Total = source.Lines.Sum(x => x.Quantity * x.UnitPrice)
        };
    }

    public static DomainOrder ToDomainOrder(OrderDto source)
    {
        return new DomainOrder
        {
            Id = source.OrderId,
            Customer = new Customer
            {
                Name = source.CustomerName,
                Address = source.Address == null
                    ? new Address()
                    : new Address
                    {
                        Street = source.Address.Street,
                        City = source.Address.City,
                        State = source.Address.State
                    }
            },
            Lines = source.Items.Select(i => new OrderLine
            {
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            DiscountCode = source.DiscountCode,
            ShippingFee = source.ShippingFee
        };
    }
}
