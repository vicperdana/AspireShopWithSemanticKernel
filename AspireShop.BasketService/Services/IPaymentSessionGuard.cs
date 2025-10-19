namespace AspireShop.BasketService.Services;

/// <summary>
/// Guards against multiple simultaneous payment session creation attempts for the same basket.
/// Prevents race conditions and duplicate sessions.
/// </summary>
public interface IPaymentSessionGuard
{
    /// <summary>
    /// Checks if a checkout attempt is allowed for the given user/basket.
    /// Returns true if allowed, false if a recent checkout is already in progress.
    /// </summary>
    Task<bool> CanCheckoutAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a checkout attempt for the given user/basket.
    /// Prevents duplicate checkouts within the guard window (e.g., 2 minutes).
    /// </summary>
    Task RecordCheckoutAttemptAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the checkout guard for a user (e.g., after successful payment or expiration).
    /// </summary>
    Task ClearCheckoutGuardAsync(string userId, CancellationToken cancellationToken = default);
}
