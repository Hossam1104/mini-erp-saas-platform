#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>Opaque recipient reference; contact values never enter the contract.</summary>
public sealed class NotificationRecipientReference
{
    public NotificationRecipientReference(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Recipient reference must not be empty.", nameof(userId));
        }

        UserId = userId;
    }

    public Guid UserId { get; }
}
/// <summary>Provider-neutral Tenant-owned notification intent.</summary>
public sealed class TenantNotificationIntent : ITenantOwned
{
    private TenantNotificationIntent(
        Guid intentId,
        TenantId tenantId,
        TenantWorkScope scope,
        NotificationRecipientReference recipient,
        string template,
        string locale,
        CorrelationId correlationId,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        IntentId = intentId;
        TenantId = tenantId;
        Scope = scope;
        Recipient = recipient;
        Template = template;
        Locale = locale;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        DeliveryState = NotificationDeliveryState.Pending;
        ConcurrencyVersion = 1;
    }

    public Guid IntentId { get; }

    public TenantId TenantId { get; }

    public TenantWorkScope Scope { get; }

    public NotificationRecipientReference Recipient { get; }

    public string Template { get; }

    public string Locale { get; }

    public CorrelationId CorrelationId { get; }

    public string IdempotencyKey { get; }

    public int AttemptCount { get; internal set; }

    public NotificationDeliveryState DeliveryState { get; internal set; }

    public DurableWorkFailureCategory FailureCategory { get; internal set; }

    public DateTimeOffset CreatedAt { get; }

    public long ConcurrencyVersion { get; internal set; }

    /// <summary>Creates an intent from trusted server context and safe references.</summary>
    public static TenantNotificationIntent Create(
        TenantContext trustedTenantContext,
        TenantWorkScope scope,
        NotificationRecipientReference recipient,
        string template,
        string locale,
        string idempotencyKey,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(trustedTenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(recipient);
        if (scope.TenantId != trustedTenantContext.TenantId)
        {
            throw new ArgumentException("Notification scope must belong to the trusted Tenant.", nameof(scope));
        }

        return new TenantNotificationIntent(
            Guid.NewGuid(),
            trustedTenantContext.TenantId,
            scope,
            recipient,
            Bounded(template, nameof(template)),
            BoundedLocale(locale),
            trustedTenantContext.CorrelationId
                ?? throw new ArgumentException("Notification requires a trusted correlation identifier.", nameof(trustedTenantContext)),
            Bounded(idempotencyKey, nameof(idempotencyKey)),
            createdAt ?? DateTimeOffset.UtcNow);
    }

    private static string Bounded(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Notification value is required and bounded.", name);
        }

        return value.Trim();
    }

    private static string BoundedLocale(string value)
    {
        var locale = Bounded(value, nameof(value)).ToLowerInvariant();
        if (locale is not ("en" or "ar"))
        {
            throw new ArgumentException("Only approved Release 1 locales are accepted.", nameof(value));
        }

        return locale;
    }
}

/// <summary>Safe outcome returned by a notification adapter.</summary>
public sealed record NotificationDeliveryResult(
    bool Delivered,
    bool Duplicate,
    NotificationDeliveryState State,
    DurableWorkFailureCategory FailureCategory,
    string SafeOutcome);

/// <summary>Provider-neutral notification adapter contract.</summary>
public interface INotificationDeliveryAdapter
{
    ValueTask<NotificationDeliveryResult> DeliverAsync(
        TenantContext tenantContext,
        TenantNotificationIntent intent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deterministic local adapter for tests and development. It stores no contact
/// data and does not represent an email, SMS, push or production provider.
/// </summary>
public sealed class InMemoryNotificationAdapter : INotificationDeliveryAdapter
{
    private readonly object syncRoot = new();
    private readonly HashSet<(TenantId TenantId, string Key)> delivered = [];
    private readonly List<(TenantId TenantId, Guid IntentId, NotificationDeliveryState State)> decisions = [];
    private readonly Func<TenantNotificationIntent, DurableWorkFailureCategory?>? failure;

    public InMemoryNotificationAdapter(
        Func<TenantNotificationIntent, DurableWorkFailureCategory?>? failure = null)
    {
        this.failure = failure;
    }

    public ValueTask<NotificationDeliveryResult> DeliverAsync(
        TenantContext tenantContext,
        TenantNotificationIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantContext.TenantId != intent.TenantId)
        {
            return ValueTask.FromResult(new NotificationDeliveryResult(
                false,
                false,
                NotificationDeliveryState.DeadLetter,
                DurableWorkFailureCategory.TenantMismatch,
                "tenant_denied"));
        }

        lock (syncRoot)
        {
            var key = (intent.TenantId, intent.IdempotencyKey);
            if (!delivered.Add(key))
            {
                intent.DeliveryState = NotificationDeliveryState.Duplicate;
                decisions.Add((intent.TenantId, intent.IntentId, intent.DeliveryState));
                return ValueTask.FromResult(new NotificationDeliveryResult(
                    true,
                    true,
                    NotificationDeliveryState.Duplicate,
                    DurableWorkFailureCategory.None,
                    "duplicate"));
            }

            intent.AttemptCount++;
            var failureCategory = failure?.Invoke(intent);
            if (failureCategory.HasValue)
            {
                delivered.Remove(key);
                intent.DeliveryState = NotificationDeliveryState.RetryScheduled;
                intent.FailureCategory = failureCategory.Value;
                decisions.Add((intent.TenantId, intent.IntentId, intent.DeliveryState));
                return ValueTask.FromResult(new NotificationDeliveryResult(
                    false,
                    false,
                    NotificationDeliveryState.RetryScheduled,
                    failureCategory.Value,
                    "provider_unavailable"));
            }

            intent.DeliveryState = NotificationDeliveryState.Delivered;
            decisions.Add((intent.TenantId, intent.IntentId, intent.DeliveryState));
            return ValueTask.FromResult(new NotificationDeliveryResult(
                true,
                false,
                NotificationDeliveryState.Delivered,
                DurableWorkFailureCategory.None,
                "delivered"));
        }
    }

    internal IReadOnlyList<(TenantId TenantId, Guid IntentId, NotificationDeliveryState State)> Decisions => decisions.ToArray();
}
