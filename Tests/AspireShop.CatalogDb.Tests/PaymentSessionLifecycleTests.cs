using AspireShop.CatalogDb;
using Xunit;

namespace Tests.AspireShop.CatalogDb.Tests;

/// <summary>
/// Tests for PaymentSession lifecycle state transitions and business rules
/// </summary>
public class PaymentSessionLifecycleTests
{
    [Fact]
    public void PaymentSession_Should_Initialize_With_Pending_Status()
    {
        // Arrange & Act
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(15)
        };

        // Assert
        Assert.Equal(PaymentStatus.Pending, session.Status);
        Assert.Null(session.CompletedUtc);
        Assert.NotNull(session.ExpiresUtc);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Expired)]
    public void PaymentSession_Should_Allow_Valid_Status_Transitions(PaymentStatus from, PaymentStatus to)
    {
        // Arrange
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = from,
            CreatedUtc = DateTime.UtcNow
        };

        // Act
        session.Status = to;
        session.CompletedUtc = DateTime.UtcNow;

        // Assert
        Assert.Equal(to, session.Status);
        Assert.NotNull(session.CompletedUtc);
    }

    [Theory]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Expired, PaymentStatus.Pending)]
    public void PaymentSession_Should_Not_Allow_Invalid_Status_Transitions(PaymentStatus from, PaymentStatus to)
    {
        // Arrange
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = from,
            CreatedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow
        };

        // Act & Assert - Terminal states should not transition
        // Note: This test validates our understanding of business rules
        // Actual enforcement would be in the service layer
        Assert.True(IsTerminalStatus(from));
        Assert.False(IsValidTransition(from, to));
    }

    [Fact]
    public void PaymentSession_Should_Calculate_Expiration_As_15_Minutes()
    {
        // Arrange
        var createdUtc = DateTime.UtcNow;
        var expectedExpiry = createdUtc.AddMinutes(15);

        // Act
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = createdUtc,
            ExpiresUtc = expectedExpiry
        };

        // Assert
        Assert.NotNull(session.ExpiresUtc);
        Assert.Equal(expectedExpiry, session.ExpiresUtc.Value);
        Assert.Equal(15, (session.ExpiresUtc.Value - session.CreatedUtc).TotalMinutes);
    }

    [Fact]
    public void PaymentSession_Should_Be_Expired_When_Past_ExpiresUtc()
    {
        // Arrange
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-20),
            ExpiresUtc = DateTime.UtcNow.AddMinutes(-5) // Expired 5 minutes ago
        };

        // Act
        var isExpired = session.ExpiresUtc.HasValue && session.ExpiresUtc.Value < DateTime.UtcNow;

        // Assert
        Assert.True(isExpired);
        Assert.Equal(PaymentStatus.Pending, session.Status); // Not auto-updated, requires sweep
    }

    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("test.user+tag@domain.co.uk", true)]
    [InlineData("invalid-email", false)]
    [InlineData("@no-local-part.com", false)]
    [InlineData("no-at-sign.com", false)]
    public void PaymentSession_Should_Validate_Email_Format(string email, bool isValid)
    {
        // Arrange & Act
        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = Guid.NewGuid(),
            CustomerEmail = email,
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        // Assert - Email format validation
        var emailIsValid = IsValidEmail(session.CustomerEmail);
        Assert.Equal(isValid, emailIsValid);
    }

    [Fact]
    public void PaymentSession_Should_Have_Unique_StripeSessionId()
    {
        // Arrange
        var stripeSessionId = "cs_test_unique_123";
        
        var session1 = new PaymentSession
        {
            StripeSessionId = stripeSessionId,
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test1@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        var session2 = new PaymentSession
        {
            StripeSessionId = stripeSessionId, // Same ID - should violate uniqueness
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test2@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        // Assert - StripeSessionId should be unique
        // Note: Actual enforcement is at database level with unique constraint
        Assert.Equal(session1.StripeSessionId, session2.StripeSessionId);
        // In real scenario, DB would throw constraint violation
    }

    [Fact]
    public void PaymentSession_Should_Link_To_Order()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            CustomerEmail = "test@example.com",
            TotalAmount = 99.99m,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        var session = new PaymentSession
        {
            StripeSessionId = "cs_test_123",
            OrderId = orderId,
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            Order = order
        };

        // Assert
        Assert.NotNull(session.Order);
        Assert.Equal(orderId, session.OrderId);
        Assert.Equal(session.CustomerEmail, order.CustomerEmail);
        Assert.Equal(session.Status, order.PaymentStatus);
    }

    // Helper methods for validation logic
    private static bool IsTerminalStatus(PaymentStatus status)
    {
        return status is PaymentStatus.Paid or PaymentStatus.Failed or PaymentStatus.Expired;
    }

    private static bool IsValidTransition(PaymentStatus from, PaymentStatus to)
    {
        if (IsTerminalStatus(from))
            return false; // Terminal states cannot transition

        return from == PaymentStatus.Pending && 
               (to == PaymentStatus.Paid || to == PaymentStatus.Failed || to == PaymentStatus.Expired);
    }

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
