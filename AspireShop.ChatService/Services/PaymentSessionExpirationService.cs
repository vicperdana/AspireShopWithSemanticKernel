using AspireShop.ChatService.Repositories;
using AspireShop.ServiceDefaults.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspireShop.ChatService.Services;

public class PaymentSessionExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentSessionExpirationService> _logger;
    private readonly TimeSpan _sweepInterval = TimeSpan.FromMinutes(5);

    public PaymentSessionExpirationService(
        IServiceProvider serviceProvider,
        ILogger<PaymentSessionExpirationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment session expiration service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during payment session expiration sweep");
            }

            await Task.Delay(_sweepInterval, stoppingToken);
        }

        _logger.LogInformation("Payment session expiration service stopped");
    }

    private async Task ExpireSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<IPaymentSessionRepository>();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var expiredSessions = await sessionRepository.GetExpiredSessionsAsync(DateTime.UtcNow, cancellationToken);

        foreach (var session in expiredSessions)
        {
            await sessionRepository.UpdateStatusAsync(session.StripeSessionId, CatalogDb.PaymentStatus.Expired, cancellationToken);
            await orderRepository.UpdatePaymentStatusAsync(session.OrderId, CatalogDb.PaymentStatus.Expired, cancellationToken);
            
            _logger.LogPaymentSessionExpired(session.StripeSessionId, session.ExpiresUtc ?? DateTime.UtcNow);
        }

        if (expiredSessions.Any())
        {
            _logger.LogInformation("Expired {Count} payment sessions", expiredSessions.Count);
        }
    }
}
