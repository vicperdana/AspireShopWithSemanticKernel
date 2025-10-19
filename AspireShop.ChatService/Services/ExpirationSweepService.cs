using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AspireShop.CatalogDb.Repositories;
using AspireShop.CatalogDb;
using System.Diagnostics;

namespace AspireShop.ChatService.Services;

/// <summary>
/// Background service that periodically expires pending payment sessions
/// </summary>
public class ExpirationSweepService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpirationSweepService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private readonly ActivitySource _activitySource;

    public ExpirationSweepService(
        IServiceProvider serviceProvider,
        ILogger<ExpirationSweepService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _activitySource = new ActivitySource("AspireShop.ChatService");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Expiration sweep service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await RunExpirationSweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in expiration sweep service");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("Expiration sweep service stopped");
    }

    private async Task RunExpirationSweepAsync(CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("ExpirationSweep");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionRepository = scope.ServiceProvider.GetRequiredService<PaymentSessionRepository>();
            var orderRepository = scope.ServiceProvider.GetRequiredService<OrderRepository>();

            var expiredSessions = await sessionRepository.GetExpiredSessionsAsync(cancellationToken);
            var sessionList = expiredSessions.ToList();
            
            if (sessionList.Count == 0)
            {
                _logger.LogDebug("No expired sessions found");
                activity?.SetTag("sessions.expired", 0);
                return;
            }

            var expiredCount = 0;
            foreach (var session in sessionList)
            {
                // Double-check it's still pending (could have been updated by webhook)
                if (session.Status != PaymentStatus.Pending)
                    continue;

                // Mark as expired
                await sessionRepository.UpdateSessionStatusAsync(
                    session.StripeSessionId,
                    PaymentStatus.Expired,
                    cancellationToken);

                // Update order
                await orderRepository.UpdateOrderStatusAsync(
                    session.OrderId,
                    PaymentStatus.Expired,
                    cancellationToken);

                expiredCount++;
            }

            if (expiredCount > 0)
            {
                _logger.LogInformation("Expired {Count} pending payment sessions", expiredCount);
            }

            activity?.SetTag("sessions.expired", expiredCount);
            activity?.SetTag("sessions.checked", sessionList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run expiration sweep");
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }
}
