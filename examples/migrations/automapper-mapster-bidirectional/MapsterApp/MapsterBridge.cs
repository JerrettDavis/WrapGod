using Mapster;

namespace MapsterApp;

public static class MapsterBridge
{
    private static readonly TypeAdapterConfig Config = BuildConfig();

    public static OrderDto ToOrderDto(DomainOrder source) => source.Adapt<OrderDto>(Config);

    public static DomainOrder ToDomainOrder(OrderDto source) => source.Adapt<DomainOrder>(Config);

    private static TypeAdapterConfig BuildConfig()
    {
        var cfg = new TypeAdapterConfig();

        cfg.NewConfig<OrderLine, OrderLineDto>()
            .Map(d => d.LineTotal, s => s.Quantity * s.UnitPrice);

        cfg.NewConfig<OrderLineDto, OrderLine>();
        cfg.NewConfig<Address, AddressDto>();
        cfg.NewConfig<AddressDto, Address>();

        cfg.NewConfig<DomainOrder, OrderDto>()
            .Map(d => d.OrderId, s => s.Id)
            .Map(d => d.CustomerName, s => s.Customer.Name)
            .Map(d => d.Address, s => s.Customer.Address)
            .Map(d => d.Items, s => s.Lines)
            .Map(d => d.Total, s => s.Lines.Sum(x => x.Quantity * x.UnitPrice));

        cfg.NewConfig<OrderDto, DomainOrder>()
            .Map(d => d.Id, s => s.OrderId)
            .Map(d => d.Customer, s => new Customer
            {
                Name = s.CustomerName,
                Address = s.Address == null
                    ? new Address()
                    : new Address { Street = s.Address.Street, City = s.Address.City, State = s.Address.State }
            })
            .Map(d => d.Lines, s => s.Items);

        return cfg;
    }
}
