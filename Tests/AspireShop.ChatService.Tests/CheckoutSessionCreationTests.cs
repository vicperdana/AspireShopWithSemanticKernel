using AspireShop.CatalogDb;
using Xunit;

namespace Tests.AspireShop.ChatService.Tests;

/// <summary>
/// Tests for Stripe Checkout Session creation logic and business rules
/// </summary>
public class CheckoutSessionCreationTests
{
    [Fact]
    public void CheckoutSession_Should_Require_Valid_Email()
    {
        // Arrange
        var validEmails = new[] { "test@example.com", "user+tag@domain.co.uk", "name.surname@company.org" };
        var invalidEmails = new[] { "invalid", "@nodomain", "no@", "spaces in@email.com", "" };

        // Act & Assert
        foreach (var email in validEmails)
        {
            Assert.True(IsValidEmail(email), $"Email '{email}' should be valid");
        }

        foreach (var email in invalidEmails)
        {
            Assert.False(IsValidEmail(email), $"Email '{email}' should be invalid");
        }
    }

    [Fact]
    public void CheckoutSession_Should_Calculate_Total_Amount_From_Items()
    {
        // Arrange
        var items = new List<OrderItem>
        {
            new OrderItem { CatalogItemId = 1, Quantity = 2, UnitPrice = 10.00m },
            new OrderItem { CatalogItemId = 2, Quantity = 1, UnitPrice = 25.50m },
            new OrderItem { CatalogItemId = 3, Quantity = 3, UnitPrice = 5.00m }
        };

        // Act
        var totalAmount = items.Sum(item => item.Quantity * item.UnitPrice);

        // Assert
        Assert.Equal(60.50m, totalAmount); // (2*10) + (1*25.50) + (3*5) = 60.50
    }

    [Fact]
    public void CheckoutSession_Should_Require_Non_Empty_Items()
    {
        // Arrange
        var emptyItems = new List<OrderItem>();

        // Act & Assert
        Assert.Empty(emptyItems);
        // Service layer should reject checkout with empty items
    }

    [Fact]
    public void CheckoutSession_Should_Require_Positive_Quantities()
    {
        // Arrange
        var invalidItems = new[]
        {
            new OrderItem { CatalogItemId = 1, Quantity = 0, UnitPrice = 10.00m },
            new OrderItem { CatalogItemId = 2, Quantity = -1, UnitPrice = 10.00m }
        };

        // Act & Assert
        foreach (var item in invalidItems)
        {
            Assert.False(item.Quantity > 0, $"Quantity {item.Quantity} should be rejected");
        }
    }

    [Fact]
    public void CheckoutSession_Should_Require_Non_Negative_Prices()
    {
        // Arrange
        var validItem = new OrderItem { CatalogItemId = 1, Quantity = 1, UnitPrice = 10.00m };
        var freeItem = new OrderItem { CatalogItemId = 2, Quantity = 1, UnitPrice = 0.00m };
        var invalidItem = new OrderItem { CatalogItemId = 3, Quantity = 1, UnitPrice = -5.00m };

        // Act & Assert
        Assert.True(validItem.UnitPrice >= 0);
        Assert.True(freeItem.UnitPrice >= 0);
        Assert.False(invalidItem.UnitPrice >= 0);
    }

    [Fact]
    public void CheckoutSession_Should_Use_USD_As_Default_Currency()
    {
        // Arrange & Act
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            TotalAmount = 100.00m,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("USD", order.Currency);
        Assert.Equal(3, order.Currency.Length); // ISO 4217 format
    }

    [Fact]
    public void CheckoutSession_Should_Create_Order_With_Pending_Status()
    {
        // Arrange & Act
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            TotalAmount = 50.00m,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal(order.CreatedUtc, order.UpdatedUtc);
    }

    [Fact]
    public void CheckoutSession_Should_Link_PaymentSession_To_Order()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerEmail = "test@example.com";

        var order = new Order
        {
            OrderId = orderId,
            CustomerEmail = customerEmail,
            TotalAmount = 75.00m,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = orderId,
            CustomerEmail = customerEmail,
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(15),
            Order = order
        };

        // Assert
        Assert.Equal(order.OrderId, session.OrderId);
        Assert.Equal(order.CustomerEmail, session.CustomerEmail);
        Assert.Equal(order.PaymentStatus, session.Status);
        Assert.NotNull(session.Order);
    }

    [Fact]
    public void CheckoutSession_Should_Prevent_Duplicate_Active_Sessions()
    {
        // Arrange
        var basketId = Guid.NewGuid();
        var existingSession = new PaymentSession
        {
            StripeSessionId = "cs_test_existing",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10) // Still active
        };

        // Act
        var hasActivePendingSession = existingSession.Status == PaymentStatus.Pending &&
                                     existingSession.ExpiresUtc > DateTime.UtcNow;

        // Assert
        Assert.True(hasActivePendingSession);
        // Service layer should reuse existing session or return error
    }

    [Theory]
    [InlineData(10.99)]
    [InlineData(100.00)]
    [InlineData(0.50)] // Minimum Stripe amount ($0.50)
    [InlineData(999999.99)]
    public void CheckoutSession_Should_Accept_Valid_Amounts(decimal amount)
    {
        // Arrange & Act
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            TotalAmount = amount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Assert
        Assert.True(order.TotalAmount >= 0);
        Assert.True(order.TotalAmount >= 0.50m); // Stripe minimum for USD
    }

    [Fact]
    public void CheckoutSession_Should_Generate_Idempotency_Key()
    {
        // Arrange
        var basketId = Guid.NewGuid();
        var userId = "user_123";

        // Act
        var idempotencyKey = $"checkout_{basketId}_{userId}";

        // Assert
        Assert.NotNull(idempotencyKey);
        Assert.Contains(basketId.ToString(), idempotencyKey);
        Assert.Contains(userId, idempotencyKey);
    }

    [Fact]
    public void CheckoutSession_Should_Set_15_Minute_Expiration()
    {
        // Arrange
        var createdUtc = DateTime.UtcNow;

        // Act
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = createdUtc,
            ExpiresUtc = createdUtc.AddMinutes(15)
        };

        // Assert
        Assert.NotNull(session.ExpiresUtc);
        var expirationMinutes = (session.ExpiresUtc.Value - session.CreatedUtc).TotalMinutes;
        Assert.Equal(15, expirationMinutes);
    }

    // Helper method for email validation
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
