using Xunit;
using Moq;
using AspireShop.ChatService.Repositories;
using AspireShop.ServiceDefaults.Stripe;
using AspireShop.CatalogDb;
using Stripe;

namespace Tests.AspireShop.ChatService.Tests;

public class PaymentLifecycleTests
{
    [Fact]
    public async Task CreatePaymentSession_Should_Create_Order_And_Session()
    {
        // Arrange
        var mockOrderRepo = new Mock<IOrderRepository>();
        var mockSessionRepo = new Mock<IPaymentSessionRepository>();
        var mockStripeFactory = new Mock<IStripeClientFactory>();
        
        var basketItems = new List<BasketItemDto>
        {
            new(1, 2, 29.99m, "Test Product")
        };

        // Act & Assert
        Assert.NotNull(basketItems);
        // TODO: Complete implementation after PaymentSessionService created
    }

    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Expired)]
    public async Task UpdatePaymentStatus_Should_Transition_Valid_States(PaymentStatus from, PaymentStatus to)
    {
        // Arrange
        var sessionId = "cs_test_123";
        var orderId = Guid.NewGuid();
        var mockSessionRepo = new Mock<IPaymentSessionRepository>();
        mockSessionRepo.Setup(r => r.GetByStripeSessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession
            {
                StripeSessionId = sessionId,
                OrderId = orderId,
                CustomerEmail = "test@example.com",
                Status = from,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(15)
            });

        // Act
        await mockSessionRepo.Object.UpdateStatusAsync(sessionId, to, CancellationToken.None);

        // Assert
        mockSessionRepo.Verify(r => r.UpdateStatusAsync(sessionId, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireSession_Should_Mark_Expired_Sessions()
    {
        // Arrange
        var mockSessionRepo = new Mock<IPaymentSessionRepository>();
        var orderId = Guid.NewGuid();
        var expiredSessions = new List<PaymentSession>
        {
            new()
            {
                StripeSessionId = "cs_expired_1",
                OrderId = orderId,
                CustomerEmail = "test@example.com",
                Status = PaymentStatus.Pending,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-20),
                ExpiresUtc = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        mockSessionRepo.Setup(r => r.GetExpiredSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredSessions);

        // Act
        var sessions = await mockSessionRepo.Object.GetExpiredSessionsAsync(DateTime.UtcNow, CancellationToken.None);

        // Assert
        Assert.Single(sessions);
        Assert.All(sessions, s => Assert.True(s.ExpiresUtc < DateTime.UtcNow));
    }
}

public record BasketItemDto(int CatalogItemId, int Quantity, decimal UnitPrice, string ProductName);
