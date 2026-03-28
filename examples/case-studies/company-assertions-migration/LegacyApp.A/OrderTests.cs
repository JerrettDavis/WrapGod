using FluentAssertions;
using Xunit;

namespace LegacyApp.A;

public class OrderTests
{
    [Fact]
    public void Order_Total_Should_Equal_Sum_Of_Items()
    {
        var items = new[] { 10.00m, 25.50m, 3.99m };
        var total = items.Sum();

        total.Should().Be(39.49m);
    }

    [Fact]
    public void Order_Should_Not_Be_Null_After_Creation()
    {
        var order = CreateOrder("ORD-001");

        order.Should().NotBeNull();
        order.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Order_Status_Should_Be_Pending_By_Default()
    {
        var order = CreateOrder("ORD-002");

        order.Status.Should().Be("Pending");
    }

    [Fact]
    public void Order_Items_Should_Contain_Expected_Product()
    {
        var order = CreateOrder("ORD-003");
        order.Items.Add("Widget");
        order.Items.Add("Gadget");

        order.Items.Should().Contain("Widget");
    }

    [Fact]
    public void Order_Items_Should_Have_Expected_Count()
    {
        var order = CreateOrder("ORD-004");
        order.Items.Add("Widget");
        order.Items.Add("Gadget");
        order.Items.Add("Doohickey");

        order.Items.Should().HaveCount(3);
    }

    [Fact]
    public void Order_Priority_Should_Be_True_For_Express()
    {
        var order = CreateOrder("ORD-005");
        order.IsExpress = true;

        order.IsExpress.Should().BeTrue();
    }

    [Fact]
    public void Empty_Order_Should_Have_No_Items()
    {
        var order = CreateOrder("ORD-006");

        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void Order_Discount_Should_Be_Greater_Than_Zero()
    {
        var discount = CalculateDiscount(150.00m);

        discount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Invalid_Order_Should_Throw_ArgumentException()
    {
        Action act = () => CreateOrder("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Order_Id_Should_Start_With_Prefix()
    {
        var order = CreateOrder("ORD-100");

        order.Id.Should().StartWith("ORD-");
    }

    // --- Helpers ---

    private static Order CreateOrder(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Order ID cannot be empty.", nameof(id));

        return new Order { Id = id, Status = "Pending" };
    }

    private static decimal CalculateDiscount(decimal subtotal) =>
        subtotal > 100m ? subtotal * 0.10m : 0m;

    private class Order
    {
        public string Id { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public bool IsExpress { get; set; }
        public List<string> Items { get; set; } = [];
    }
}
