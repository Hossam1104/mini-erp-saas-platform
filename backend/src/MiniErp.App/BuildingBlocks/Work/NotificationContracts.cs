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

/// <summary>
/// An Identity-proven recipient: <see cref="INotificationRecipientAuthorizer"/>
/// is the only constructor. A raw <see cref="NotificationRecipientReference"/>
/// alone is never sufficient to create a <see cref="TenantNotificationIntent"/>
/// -- the referenced user must be live-verified as an active member of the
/// exact target Tenant.
/// </summary>
public sealed class VerifiedNotificationRecipient
{
    internal VerifiedNotificationRecipient(TenantId tenantId, Guid userId)
    {
        TenantId = tenantId;
        UserId = userId;
    }

    public TenantId TenantId { get; }

    public Guid UserId { get; }
}

/// <summary>Safe outcome of a notification-recipient authorization check.</summary>
public enum NotificationRecipientAuthorizationOutcome
{
    Approved = 1,
    Denied = 2
}

/// <summary>Safe result of resolving and authorizing a notification recipient.</summary>
public sealed class NotificationRecipientAuthorizationResult
{
    private NotificationRecipientAuthorizationResult(
        NotificationRecipientAuthorizationOutcome outcome,
        string safeReason,
        VerifiedNotificationRecipient? recipient)
    {
        Outcome = outcome;
        SafeReason = safeReason;
        Recipient = recipient;
    }

    public NotificationRecipientAuthorizationOutcome Outcome { get; }

    public bool Allowed => Outcome == NotificationRecipientAuthorizationOutcome.Approved;

    public string SafeReason { get; }

    public VerifiedNotificationRecipient? Recipient { get; }

    public static NotificationRecipientAuthorizationResult Approved(VerifiedNotificationRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return new(NotificationRecipientAuthorizationOutcome.Approved, "recipient_authorized", recipient);
    }

    public static NotificationRecipientAuthorizationResult Denied(string safeReason) =>
        new(NotificationRecipientAuthorizationOutcome.Denied, safeReason, null);
}

/// <summary>
/// Identity-owned port that proves a caller-supplied recipient reference is an
/// active member of the caller's exact Tenant before any notification intent
/// can be created for it (M-8). A foreign-Tenant, suspended, revoked or
/// unknown user is always denied; a Platform governance actor has no Tenant
/// Membership or SupportGrant path and can never satisfy this authorizer.
/// </summary>
public interface INotificationRecipientAuthorizer
{
    ValueTask<NotificationRecipientAuthorizationResult> AuthorizeAsync(
        TenantContext currentTenantContext,
        NotificationRecipientReference recipient,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider-neutral Tenant-owned notification intent.</summary>
public sealed class TenantNotificationIntent : ITenantOwned
{
    /// <summary>
    /// The bounded, explicit maximum delivery attempts before an intent
    /// transitions to the terminal <see cref="NotificationDeliveryState.DeadLetter"/>
    /// state and is never automatically retried again (M-7).
    /// </summary>
    public const int MaxDeliveryAttempts = 5;

    private TenantNotificationIntent(
        Guid intentId,
        TenantId tenantId,
        TenantWorkScope scope,
        VerifiedNotificationRecipient recipient,
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

    public VerifiedNotificationRecipient Recipient { get; }

    public string Template { get; }

    public string Locale { get; }

    public CorrelationId CorrelationId { get; }

    public string IdempotencyKey { get; }

    public int AttemptCount { get; internal set; }

    public NotificationDeliveryState DeliveryState { get; internal set; }

    public DurableWorkFailureCategory FailureCategory { get; internal set; }

    public DateTimeOffset CreatedAt { get; }

    public long ConcurrencyVersion { get; internal set; }

    /// <summary>
    /// Creates an intent from trusted server context and a recipient already
    /// proven eligible by <see cref="INotificationRecipientAuthorizer"/>.
    /// </summary>
    public static TenantNotificationIntent Create(
        TenantContext trustedTenantContext,
        TenantWorkScope scope,
        VerifiedNotificationRecipient recipient,
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

        if (recipient.TenantId != trustedTenantContext.TenantId)
        {
            throw new ArgumentException("Notification recipient must be verified for the exact trusted Tenant.", nameof(recipient));
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
            intent.DeliveryState = NotificationDeliveryState.DeadLetter;
            intent.FailureCategory = DurableWorkFailureCategory.TenantMismatch;
            return ValueTask.FromResult(new NotificationDeliveryResult(
                false,
                false,
                NotificationDeliveryState.DeadLetter,
                DurableWorkFailureCategory.TenantMismatch,
                "tenant_denied"));
        }

        lock (syncRoot)
        {
            // Terminal: once dead-lettered, an intent is never retried again,
            // regardless of caller retries or worker duplicate claims (M-7).
            if (intent.DeliveryState == NotificationDeliveryState.DeadLetter)
            {
                return ValueTask.FromResult(new NotificationDeliveryResult(
                    false,
                    false,
                    NotificationDeliveryState.DeadLetter,
                    intent.FailureCategory,
                    "dead_lettered"));
            }

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
                intent.FailureCategory = failureCategory.Value;
                if (intent.AttemptCount >= TenantNotificationIntent.MaxDeliveryAttempts)
                {
                    intent.DeliveryState = NotificationDeliveryState.DeadLetter;
                    decisions.Add((intent.TenantId, intent.IntentId, intent.DeliveryState));
                    return ValueTask.FromResult(new NotificationDeliveryResult(
                        false,
                        false,
                        NotificationDeliveryState.DeadLetter,
                        failureCategory.Value,
                        "dead_lettered"));
                }

                intent.DeliveryState = NotificationDeliveryState.RetryScheduled;
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
