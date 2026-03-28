using FluentAssertions;
using Xunit;

namespace LegacyApp.D;

public class PaymentTests
{
    [Fact]
    public void Payment_Amount_Should_Be_Positive()
    {
        var payment = CreatePayment(99.99m, "USD");

        payment.Amount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Payment_Currency_Should_Be_USD()
    {
        var payment = CreatePayment(50.00m, "USD");

        payment.Currency.Should().Be("USD");
    }

    [Fact]
    public void Payment_Should_Not_Be_Null()
    {
        var payment = CreatePayment(10.00m, "EUR");

        payment.Should().NotBeNull();
    }

    [Fact]
    public void Payment_Status_Should_Start_With_Pending()
    {
        var payment = CreatePayment(10.00m, "USD");

        payment.Status.Should().StartWith("Pending");
    }

    [Fact]
    public void Payment_IsRefunded_Should_Be_False_Initially()
    {
        var payment = CreatePayment(25.00m, "USD");

        payment.IsRefunded.Should().BeFalse();
    }

    [Fact]
    public void Payment_Tags_Should_Contain_Online()
    {
        var payment = CreatePayment(75.00m, "USD");
        payment.Tags.Add("Online");
        payment.Tags.Add("Card");

        payment.Tags.Should().Contain("Online");
    }

    [Fact]
    public void Zero_Amount_Should_Throw()
    {
        Action act = () => CreatePayment(0m, "USD");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Payment_Description_Should_End_With_Currency()
    {
        var payment = CreatePayment(100.00m, "GBP");

        payment.Description.Should().EndWith("GBP");
    }

    [Fact]
    public void Refund_Should_Set_IsRefunded()
    {
        var payment = CreatePayment(50.00m, "USD");

        AssertionHelpers.AssertIsTrue(payment.IsRefunded == false);
        payment.IsRefunded = true;
        AssertionHelpers.AssertIsTrue(payment.IsRefunded);
    }

    [Fact]
    public void Payment_Tags_Should_Be_Empty_Initially()
    {
        var payment = CreatePayment(30.00m, "USD");

        payment.Tags.Should().BeEmpty();
    }

    // --- Custom assertion helpers (legacy pattern) ---

    private static class AssertionHelpers
    {
        public static void AssertIsTrue(bool condition)
        {
            condition.Should().BeTrue();
        }

        public static void AssertAreEqual<T>(T expected, T actual)
        {
            actual.Should().Be(expected);
        }
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
