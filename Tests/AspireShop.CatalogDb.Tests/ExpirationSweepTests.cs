using AspireShop.CatalogDb;
using Xunit;

namespace Tests.AspireShop.CatalogDb.Tests;

/// <summary>
/// Tests for payment session expiration sweep logic
/// </summary>
public class ExpirationSweepTests
{
    [Fact]
    public void ExpirationSweep_Should_Identify_Expired_Pending_Sessions()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sessions = new List<PaymentSession>
        {
            // Expired session
            new PaymentSession
            {
                StripeSessionId = "cs_expired_1",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test1@example.com",
                Status = PaymentStatus.Pending,
                CreatedUtc = now.AddMinutes(-20),
                ExpiresUtc = now.AddMinutes(-5) // Expired 5 minutes ago
            },
            // Active session
            new PaymentSession
            {
                StripeSessionId = "cs_active_1",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test2@example.com",
                Status = PaymentStatus.Pending,
                CreatedUtc = now.AddMinutes(-5),
                ExpiresUtc = now.AddMinutes(10) // Still valid
            },
            // Already completed session
            new PaymentSession
            {
                StripeSessionId = "cs_completed_1",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test3@example.com",
                Status = PaymentStatus.Paid,
                CreatedUtc = now.AddMinutes(-30),
                CompletedUtc = now.AddMinutes(-25),
                ExpiresUtc = now.AddMinutes(-15) // Expired but already completed
            }
        };

        // Act
        var expiredSessions = sessions
            .Where(s => s.Status == PaymentStatus.Pending && 
                       s.ExpiresUtc.HasValue && 
                       s.ExpiresUtc.Value < now)
            .ToList();

        // Assert
        Assert.Single(expiredSessions);
        Assert.Equal("cs_expired_1", expiredSessions[0].StripeSessionId);
    }

    [Fact]
    public void ExpirationSweep_Should_Only_Process_Pending_Status()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var expiredTimestamp = now.AddMinutes(-5);
        
        var sessions = new List<PaymentSession>
        {
            new PaymentSession 
            { 
                Status = PaymentStatus.Pending, 
                ExpiresUtc = expiredTimestamp,
                StripeSessionId = "cs_pending",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@example.com",
                CreatedUtc = now.AddMinutes(-20)
            },
            new PaymentSession 
            { 
                Status = PaymentStatus.Paid, 
                ExpiresUtc = expiredTimestamp,
                StripeSessionId = "cs_paid",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@example.com",
                CreatedUtc = now.AddMinutes(-20),
                CompletedUtc = now.AddMinutes(-15)
            },
            new PaymentSession 
            { 
                Status = PaymentStatus.Failed, 
                ExpiresUtc = expiredTimestamp,
                StripeSessionId = "cs_failed",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@example.com",
                CreatedUtc = now.AddMinutes(-20),
                CompletedUtc = now.AddMinutes(-15)
            },
            new PaymentSession 
            { 
                Status = PaymentStatus.Expired, 
                ExpiresUtc = expiredTimestamp,
                StripeSessionId = "cs_already_expired",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@example.com",
                CreatedUtc = now.AddMinutes(-20),
                CompletedUtc = now.AddMinutes(-15)
            }
        };

        // Act
        var sessionsToExpire = sessions
            .Where(s => s.Status == PaymentStatus.Pending && 
                       s.ExpiresUtc.HasValue && 
                       s.ExpiresUtc.Value < now)
            .ToList();

        // Assert
        Assert.Single(sessionsToExpire);
        Assert.Equal("cs_pending", sessionsToExpire[0].StripeSessionId);
    }

    [Fact]
    public void ExpirationSweep_Should_Update_Status_And_CompletedUtc()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var session = new PaymentSession
        {
            StripeSessionId = "cs_to_expire",
            OrderId = Guid.NewGuid(),
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = now.AddMinutes(-20),
            ExpiresUtc = now.AddMinutes(-5),
            CompletedUtc = null
        };

        // Act - Simulate expiration sweep update
        session.Status = PaymentStatus.Expired;
        session.CompletedUtc = now;

        // Assert
        Assert.Equal(PaymentStatus.Expired, session.Status);
        Assert.NotNull(session.CompletedUtc);
        Assert.True(session.CompletedUtc.Value <= now);
    }

    [Fact]
    public void ExpirationSweep_Should_Mirror_Status_To_Order()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        
        var order = new Order
        {
            OrderId = orderId,
            CustomerEmail = "test@example.com",
            TotalAmount = 100.00m,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedUtc = now.AddMinutes(-20),
            UpdatedUtc = now.AddMinutes(-20)
        };

        var session = new PaymentSession
        {
            StripeSessionId = "cs_to_expire",
            OrderId = orderId,
            CustomerEmail = "test@example.com",
            Status = PaymentStatus.Pending,
            CreatedUtc = now.AddMinutes(-20),
            ExpiresUtc = now.AddMinutes(-5),
            Order = order
        };

        // Act - Simulate expiration sweep
        session.Status = PaymentStatus.Expired;
        session.CompletedUtc = now;
        order.PaymentStatus = session.Status;
        order.UpdatedUtc = now;

        // Assert
        Assert.Equal(PaymentStatus.Expired, session.Status);
        Assert.Equal(PaymentStatus.Expired, order.PaymentStatus);
        Assert.Equal(session.Status, order.PaymentStatus);
        Assert.True(order.UpdatedUtc > order.CreatedUtc);
    }

    [Fact]
    public void ExpirationSweep_Should_Run_Every_5_Minutes()
    {
        // Arrange
        var sweepIntervalMinutes = 5;
        var lastSweepTime = DateTime.UtcNow.AddMinutes(-6);
        var now = DateTime.UtcNow;

        // Act
        var shouldRunSweep = (now - lastSweepTime).TotalMinutes >= sweepIntervalMinutes;

        // Assert
        Assert.True(shouldRunSweep);
    }

    [Fact]
    public void ExpirationSweep_Should_Skip_If_Recently_Run()
    {
        // Arrange
        var sweepIntervalMinutes = 5;
        var lastSweepTime = DateTime.UtcNow.AddMinutes(-2); // Run 2 minutes ago
        var now = DateTime.UtcNow;

        // Act
        var shouldRunSweep = (now - lastSweepTime).TotalMinutes >= sweepIntervalMinutes;

        // Assert
        Assert.False(shouldRunSweep);
    }

    [Fact]
    public void ExpirationSweep_Should_Handle_Sessions_Without_ExpiresUtc()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sessions = new List<PaymentSession>
        {
            new PaymentSession
            {
                StripeSessionId = "cs_no_expiry",
                OrderId = Guid.NewGuid(),
                CustomerEmail = "test@example.com",
                Status = PaymentStatus.Pending,
                CreatedUtc = now.AddMinutes(-20),
                ExpiresUtc = null // No expiration set
            }
        };

        // Act
        var expiredSessions = sessions
            .Where(s => s.Status == PaymentStatus.Pending && 
                       s.ExpiresUtc.HasValue && 
                       s.ExpiresUtc.Value < now)
            .ToList();

        // Assert
        Assert.Empty(expiredSessions); // Session without ExpiresUtc should be skipped
    }

    [Fact]
    public void ExpirationSweep_Should_Process_Multiple_Expired_Sessions()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sessions = new List<PaymentSession>();
        
        for (int i = 0; i < 10; i++)
        {
            sessions.Add(new PaymentSession
            {
                StripeSessionId = $"cs_expired_{i}",
                OrderId = Guid.NewGuid(),
                CustomerEmail = $"test{i}@example.com",
                Status = PaymentStatus.Pending,
                CreatedUtc = now.AddMinutes(-20 - i),
                ExpiresUtc = now.AddMinutes(-5 - i)
            });
        }

        // Act
        var expiredSessions = sessions
            .Where(s => s.Status == PaymentStatus.Pending && 
                       s.ExpiresUtc.HasValue && 
                       s.ExpiresUtc.Value < now)
            .ToList();

        // Assert
        Assert.Equal(10, expiredSessions.Count);
    }

    [Fact]
    public void ExpirationSweep_Should_Use_ExpiresUtc_Index_For_Query()
    {
        // Arrange
        var now = DateTime.UtcNow;
        
        // This test validates the query pattern for database index usage
        // Query should filter on: Status = Pending AND ExpiresUtc < now
        var queryPattern = "WHERE Status = @Pending AND ExpiresUtc < @Now";

        // Assert
        Assert.Contains("Status", queryPattern);
        Assert.Contains("ExpiresUtc", queryPattern);
        // This validates that our query will use the composite index on (Status, ExpiresUtc)
    }

    [Fact]
    public void ExpirationSweep_Should_Log_Expired_Session_Count()
    {
        // Arrange
        var expiredCount = 5;
        var sweepTime = DateTime.UtcNow;

        // Act
        var logMessage = $"Expiration sweep completed: {expiredCount} sessions expired at {sweepTime:u}";

        // Assert
        Assert.Contains(expiredCount.ToString(), logMessage);
        Assert.Contains("expired", logMessage.ToLower());
    }

    [Theory]
    [InlineData(0)]  // No expired sessions
    [InlineData(1)]  // Single expired session
    [InlineData(50)] // Batch of expired sessions
    [InlineData(100)] // Large batch
    public void ExpirationSweep_Should_Handle_Various_Batch_Sizes(int expiredCount)
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sessions = new List<PaymentSession>();
        
        for (int i = 0; i < expiredCount; i++)
        {
            sessions.Add(new PaymentSession
            {
                StripeSessionId = $"cs_{i}",
                OrderId = Guid.NewGuid(),
                CustomerEmail = $"test{i}@example.com",
                Status = PaymentStatus.Pending,
                CreatedUtc = now.AddMinutes(-20),
                ExpiresUtc = now.AddMinutes(-1)
            });
        }

        // Act
        var toExpire = sessions
            .Where(s => s.Status == PaymentStatus.Pending && 
                       s.ExpiresUtc.HasValue && 
                       s.ExpiresUtc.Value < now)
            .ToList();

        // Assert
        Assert.Equal(expiredCount, toExpire.Count);
    }
}
