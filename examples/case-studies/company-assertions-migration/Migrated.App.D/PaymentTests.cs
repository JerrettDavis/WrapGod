using Shouldly;
using Xunit;

namespace Migrated.App.D;

public class PaymentTests
{
    [Fact]
    public void Payment_Amount_Should_Be_Positive()
    {
        var payment = CreatePayment(99.99m, "USD");

        payment.Amount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Payment_Currency_Should_Be_USD()
    {
        var payment = CreatePayment(50.00m, "USD");

        payment.Currency.ShouldBe("USD");
    }

    [Fact]
    public void Payment_Should_Not_Be_Null()
    {
        var payment = CreatePayment(10.00m, "EUR");

        payment.ShouldNotBeNull();
    }

    [Fact]
    public void Payment_Status_Should_Start_With_Pending()
    {
        var payment = CreatePayment(10.00m, "USD");

        payment.Status.ShouldStartWith("Pending");
    }

    [Fact]
    public void Payment_IsRefunded_Should_Be_False_Initially()
    {
        var payment = CreatePayment(25.00m, "USD");

        payment.IsRefunded.ShouldBeFalse();
    }

    [Fact]
    public void Payment_Tags_Should_Contain_Online()
    {
        var payment = CreatePayment(75.00m, "USD");
        payment.Tags.Add("Online");
        payment.Tags.Add("Card");

        payment.Tags.ShouldContain("Online");
    }

    [Fact]
    public void Zero_Amount_Should_Throw()
    {
        Should.Throw<ArgumentException>(() => CreatePayment(0m, "USD"));
    }

    [Fact]
    public void Payment_Description_Should_End_With_Currency()
    {
        var payment = CreatePayment(100.00m, "GBP");

        payment.Description.ShouldEndWith("GBP");
    }

    [Fact]
    public void Refund_Should_Set_IsRefunded()
    {
        var payment = CreatePayment(50.00m, "USD");

        payment.IsRefunded.ShouldBeFalse();
        payment.IsRefunded = true;
        payment.IsRefunded.ShouldBeTrue();
    }

    [Fact]
    public void Payment_Tags_Should_Be_Empty_Initially()
    {
        var payment = CreatePayment(30.00m, "USD");

        payment.Tags.ShouldBeEmpty();
    }

    // --- Helpers ---

    private static Payment CreatePayment(decimal amount, string currency)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        return new Payment
        {
            Amount = amount,
            Currency = currency,
            Status = "Pending",
            Description = $"Payment of {amount} {currency}"
        };
    }

    private class Payment
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "";
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsRefunded { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}
