using Xunit;
using Moq;
using AspireShop.ServiceDefaults.RateLimiting;

namespace Tests.AspireShop.ChatService.Tests;

public class ThrottlingTests
{
    [Fact]
    public async Task ChatThrottle_Should_Allow_Under_Limit()
    {
        // Arrange
        var options = new ChatThrottleOptions { MaxRequestsPerUser = 30, WindowMinutes = 1 };
        var throttle = new InMemoryChatThrottle(options);
        var userId = "user_test";

        // Act
        var results = new List<bool>();
        for (int i = 0; i < 25; i++)
        {
            results.Add(throttle.IsAllowed(userId));
        }

        // Assert
        Assert.All(results, r => Assert.True(r));
    }

    [Fact]
    public async Task ChatThrottle_Should_Block_Over_Limit()
    {
        // Arrange
        var options = new ChatThrottleOptions { MaxRequestsPerUser = 5, WindowMinutes = 1 };
        var throttle = new InMemoryChatThrottle(options);
        var userId = "user_test";

        // Act
        for (int i = 0; i < 5; i++)
        {
            throttle.IsAllowed(userId);
        }
        var blocked = throttle.IsAllowed(userId);

        // Assert
        Assert.False(blocked);
    }

    [Fact]
    public async Task ChatThrottle_Should_Reset_After_Window()
    {
        // Arrange
        var options = new ChatThrottleOptions { MaxRequestsPerUser = 2, WindowMinutes = 0 }; // 0 = immediate expiry
        var throttle = new InMemoryChatThrottle(options);
        var userId = "user_test";

        // Act
        throttle.IsAllowed(userId);
        throttle.IsAllowed(userId);
        await Task.Delay(100); // Allow window expiry
        var allowed = throttle.IsAllowed(userId);

        // Assert
        Assert.True(allowed);
    }

    [Fact]
    public async Task BasketSessionGuard_Should_Block_Rapid_Checkout()
    {
        // Arrange
        var options = new ChatThrottleOptions { PaymentSessionGuardWindowMinutes = 2 };
        var guard = new InMemoryBasketSessionGuard(options);
        var userId = "user_test";

        // Act
        await guard.RecordCheckoutAttemptAsync(userId);
        var canCheckout = await guard.CanCreateSessionAsync(userId);

        // Assert
        Assert.False(canCheckout);
    }

    [Fact]
    public async Task BasketSessionGuard_Should_Allow_After_Window()
    {
        // Arrange
        var options = new ChatThrottleOptions { PaymentSessionGuardWindowMinutes = 0 }; // 0 = immediate expiry
        var guard = new InMemoryBasketSessionGuard(options);
        var userId = "user_test";

        // Act
        await guard.RecordCheckoutAttemptAsync(userId);
        await Task.Delay(100); // Simulate window expiry
        var canCheckout = await guard.CanCreateSessionAsync(userId);

        // Assert
        Assert.True(canCheckout);
    }
}
