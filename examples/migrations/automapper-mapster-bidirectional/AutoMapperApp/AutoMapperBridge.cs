using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoMapperApp;

public static class AutoMapperBridge
{
    private static readonly IMapper Mapper = CreateMapper();

    public static OrderDto ToOrderDto(DomainOrder source) => Mapper.Map<OrderDto>(source);

    public static DomainOrder ToDomainOrder(OrderDto source) => Mapper.Map<DomainOrder>(source);

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<OrderLine, OrderLineDto>()
                .ForMember(d => d.LineTotal, opt => opt.MapFrom(s => s.Quantity * s.UnitPrice));

            cfg.CreateMap<OrderLineDto, OrderLine>();
            cfg.CreateMap<Address, AddressDto>();
            cfg.CreateMap<AddressDto, Address>();

            cfg.CreateMap<DomainOrder, OrderDto>()
                .ForMember(d => d.OrderId, opt => opt.MapFrom(s => s.Id))
                .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.Customer.Name))
                .ForMember(d => d.Address, opt => opt.MapFrom(s => s.Customer.Address))
                .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Lines))
                .ForMember(d => d.Total, opt => opt.MapFrom(s => s.Lines.Sum(x => x.Quantity * x.UnitPrice)));

            cfg.CreateMap<OrderDto, DomainOrder>()
                .ForMember(d => d.Id, opt => opt.MapFrom(s => s.OrderId))
                .ForMember(d => d.Customer, opt => opt.MapFrom(s => new Customer
                {
                    Name = s.CustomerName,
                    Address = s.Address == null
                        ? new Address()
                        : new Address
                        {
                            Street = s.Address.Street,
                            City = s.Address.City,
                            State = s.Address.State
                        }
                }))
                .ForMember(d => d.Lines, opt => opt.MapFrom(s => s.Items));
        }, NullLoggerFactory.Instance);

        config.AssertConfigurationIsValid();
        return config.CreateMapper();
    }
}
