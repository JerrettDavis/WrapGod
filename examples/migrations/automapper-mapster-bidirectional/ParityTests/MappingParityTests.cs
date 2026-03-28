using Xunit;

namespace ParityTests;

public class MappingParityTests
{
    [Fact]
    public void AutoMapper_and_Mapster_emit_equivalent_dto_graphs()
    {
        var sourceA = BuildDomainOrderForAutoMapper();
        var sourceM = BuildDomainOrderForMapster();

        var autoDto = AutoMapperApp.AutoMapperBridge.ToOrderDto(sourceA);
        var mapDto = MapsterApp.MapsterBridge.ToOrderDto(sourceM);

        Assert.Equal(autoDto.OrderId, mapDto.OrderId);
        Assert.Equal(autoDto.CustomerName, mapDto.CustomerName);
        Assert.Equal(autoDto.Address.City, mapDto.Address.City);
        Assert.Equal(autoDto.DiscountCode, mapDto.DiscountCode);
        Assert.Equal(autoDto.ShippingFee, mapDto.ShippingFee);
        Assert.Equal(autoDto.Total, mapDto.Total);
        Assert.Equal(autoDto.Items.Count, mapDto.Items.Count);
        Assert.Equal(autoDto.Items[0].LineTotal, mapDto.Items[0].LineTotal);
        Assert.Equal(autoDto.Items[1].LineTotal, mapDto.Items[1].LineTotal);
    }

    [Fact]
    public void AutoMapper_and_Mapster_reverse_map_equivalent_domain_graphs()
    {
        var dtoA = BuildOrderDtoForAutoMapper();
        var dtoM = BuildOrderDtoForMapster();

        var autoDomain = AutoMapperApp.AutoMapperBridge.ToDomainOrder(dtoA);
        var mapDomain = MapsterApp.MapsterBridge.ToDomainOrder(dtoM);

        Assert.Equal(autoDomain.Id, mapDomain.Id);
        Assert.Equal(autoDomain.Customer.Name, mapDomain.Customer.Name);
        Assert.Equal(autoDomain.Customer.Address.State, mapDomain.Customer.Address.State);
        Assert.Equal(autoDomain.DiscountCode, mapDomain.DiscountCode);
        Assert.Equal(autoDomain.ShippingFee, mapDomain.ShippingFee);
        Assert.Equal(autoDomain.Lines.Count, mapDomain.Lines.Count);
        Assert.Equal(autoDomain.Lines[0].Sku, mapDomain.Lines[0].Sku);
        Assert.Equal(autoDomain.Lines[1].Quantity, mapDomain.Lines[1].Quantity);
    }

    [Fact]
    public void Nullable_and_nested_collection_mappings_are_preserved()
    {
        var sourceA = new AutoMapperApp.DomainOrder
        {
            Id = "ORD-NULL-1",
            Customer = new AutoMapperApp.Customer
            {
                Name = "JD",
                Address = new AutoMapperApp.Address { Street = "1 Main", City = "Tulsa", State = null }
            },
            Lines = [new AutoMapperApp.OrderLine { Sku = "SKU-X", Quantity = 1, UnitPrice = 9.99m }],
            DiscountCode = null,
            ShippingFee = null
        };

        var sourceM = new MapsterApp.DomainOrder
        {
            Id = "ORD-NULL-1",
            Customer = new MapsterApp.Customer
            {
                Name = "JD",
                Address = new MapsterApp.Address { Street = "1 Main", City = "Tulsa", State = null }
            },
            Lines = [new MapsterApp.OrderLine { Sku = "SKU-X", Quantity = 1, UnitPrice = 9.99m }],
            DiscountCode = null,
            ShippingFee = null
        };

        var autoDto = AutoMapperApp.AutoMapperBridge.ToOrderDto(sourceA);
        var mapDto = MapsterApp.MapsterBridge.ToOrderDto(sourceM);

        Assert.Null(autoDto.DiscountCode);
        Assert.Null(mapDto.DiscountCode);
        Assert.Null(autoDto.ShippingFee);
        Assert.Null(mapDto.ShippingFee);
        Assert.Null(autoDto.Address.State);
        Assert.Null(mapDto.Address.State);
        Assert.Single(autoDto.Items);
        Assert.Single(mapDto.Items);
    }

    private static AutoMapperApp.DomainOrder BuildDomainOrderForAutoMapper() =>
        new()
        {
            Id = "ORD-100",
            Customer = new AutoMapperApp.Customer
            {
                Name = "Jane Doe",
                Address = new AutoMapperApp.Address { Street = "123 Main", City = "Tulsa", State = "OK" }
            },
            Lines =
            [
                new AutoMapperApp.OrderLine { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m },
                new AutoMapperApp.OrderLine { Sku = "SKU-2", Quantity = 1, UnitPrice = 15m }
            ],
            DiscountCode = "SPRING10",
            ShippingFee = 4.99m
        };

    private static MapsterApp.DomainOrder BuildDomainOrderForMapster() =>
        new()
        {
            Id = "ORD-100",
            Customer = new MapsterApp.Customer
            {
                Name = "Jane Doe",
                Address = new MapsterApp.Address { Street = "123 Main", City = "Tulsa", State = "OK" }
            },
            Lines =
            [
                new MapsterApp.OrderLine { Sku = "SKU-1", Quantity = 2, UnitPrice = 10m },
                new MapsterApp.OrderLine { Sku = "SKU-2", Quantity = 1, UnitPrice = 15m }
            ],
            DiscountCode = "SPRING10",
            ShippingFee = 4.99m
        };

    private static AutoMapperApp.OrderDto BuildOrderDtoForAutoMapper() =>
        new()
        {
            OrderId = "ORD-200",
            CustomerName = "John Doe",
            Address = new AutoMapperApp.AddressDto { Street = "500 Elm", City = "Broken Arrow", State = "OK" },
            Items =
            [
                new AutoMapperApp.OrderLineDto { Sku = "SKU-A", Quantity = 3, UnitPrice = 5m, LineTotal = 15m },
                new AutoMapperApp.OrderLineDto { Sku = "SKU-B", Quantity = 1, UnitPrice = 20m, LineTotal = 20m }
            ],
            DiscountCode = "LOYALTY",
            ShippingFee = 0m,
            Total = 35m
        };

    private static MapsterApp.OrderDto BuildOrderDtoForMapster() =>
        new()
        {
            OrderId = "ORD-200",
            CustomerName = "John Doe",
            Address = new MapsterApp.AddressDto { Street = "500 Elm", City = "Broken Arrow", State = "OK" },
            Items =
            [
                new MapsterApp.OrderLineDto { Sku = "SKU-A", Quantity = 3, UnitPrice = 5m, LineTotal = 15m },
                new MapsterApp.OrderLineDto { Sku = "SKU-B", Quantity = 1, UnitPrice = 20m, LineTotal = 20m }
            ],
            DiscountCode = "LOYALTY",
            ShippingFee = 0m,
            Total = 35m
        };
}
