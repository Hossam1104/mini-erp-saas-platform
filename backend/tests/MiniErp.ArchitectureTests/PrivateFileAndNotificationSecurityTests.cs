using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// MESP-93 focused correction: closes M-1 (foreign vs missing file existence
/// oracle), M-4 (invalid-state object overwrite), M-5 (unsafe Unicode
/// filename controls), M-7 (unbounded notification retry), M-8 (unverified
/// notification recipient), M-9 (untested returned-content immutability) and
/// L-4 (dead AnonymousDenied outcome / misleading disposition cleanup).
/// </summary>
public sealed class PrivateFileAndNotificationSecurityTests
{
    // -------------------------------------------------------------------
    // M-1: foreign vs missing file existence oracle
    // -------------------------------------------------------------------

    [Fact]
    public async Task Foreign_tenant_object_and_missing_object_are_indistinguishable_on_read()
    {
        var owner = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(owner, DurableWorkTestSupport.TenantWideScope(owner), "secret.txt", "text/plain", Content("secret"));

        var foreignResult = await storage.ReadAsync(CreateContext(), metadata.ObjectId);
        var missingResult = await storage.ReadAsync(CreateContext(), Guid.NewGuid());

        Assert.Equal(PrivateFileAccessOutcome.NotFound, foreignResult.Outcome);
        Assert.Equal(missingResult.Outcome, foreignResult.Outcome);
        Assert.Null(foreignResult.Metadata);
        Assert.Null(foreignResult.Content);
        Assert.False(foreignResult.Allowed);
    }

    [Fact]
    public async Task Foreign_tenant_object_and_missing_object_are_indistinguishable_on_overwrite()
    {
        var owner = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(owner, DurableWorkTestSupport.TenantWideScope(owner), "secret.txt", "text/plain", Content("secret"));

        var foreignResult = await storage.OverwriteAsync(CreateContext(), metadata.ObjectId, metadata.ConcurrencyVersion, Content("forged"));
        var missingResult = await storage.OverwriteAsync(CreateContext(), Guid.NewGuid(), 1, Content("forged"));

        Assert.Equal(PrivateFileAccessOutcome.NotFound, foreignResult.Outcome);
        Assert.Equal(missingResult.Outcome, foreignResult.Outcome);
    }

    // -------------------------------------------------------------------
    // M-4: expired/invalid objects fail closed on overwrite
    // -------------------------------------------------------------------

    [Fact]
    public async Task Expired_object_cannot_be_overwritten()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), "old.txt", "text/plain", Content("old"), DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("new"));

        Assert.Equal(PrivateFileAccessOutcome.Expired, result.Outcome);
        Assert.True(storage.ExistsForValidation(metadata.ObjectId));
    }

    [Fact]
    public async Task Checksum_failed_object_cannot_be_overwritten()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "tampered.txt", "text/plain", Content("safe"));
        storage.TamperForValidation(metadata.ObjectId, "tampered"u8.ToArray());

        var result = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("new"));

        Assert.Equal(PrivateFileAccessOutcome.ChecksumFailed, result.Outcome);
    }

    [Fact]
    public async Task Available_unexpired_object_can_still_be_overwritten()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "ok.txt", "text/plain", Content("ok"));

        var result = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("updated"));

        Assert.True(result.Allowed);
        Assert.Equal("updated", Encoding.UTF8.GetString(result.Content!));
    }

    // -------------------------------------------------------------------
    // M-5: Unicode filename security
    // -------------------------------------------------------------------

    [Fact]
    public async Task Valid_english_filename_is_accepted()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string fileName = "invoice.pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("ok"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Valid_arabic_filename_is_accepted()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        // "فاتورة.pdf" spells the Arabic word for
        // "invoice" (fatura) followed by the .pdf extension.
        const string fileName = "فاتورة.pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("ok"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Mixed_arabic_and_english_filename_is_accepted()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string fileName = "invoice-فاتورة-2026.pdf";

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("ok"));

        Assert.Equal(fileName, metadata.OriginalFileName);
    }

    [Fact]
    public async Task Composed_and_decomposed_unicode_filenames_normalize_to_the_same_stored_name()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string composed = "café.txt"; // single composed code point U+00E9 (e with acute accent)
        const string decomposed = "café.txt"; // base 'e' (U+0065) + combining acute accent U+0301

        var storedComposed = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), composed, "text/plain", Content("a"));
        var storedDecomposed = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), decomposed, "text/plain", Content("b"));

        Assert.Equal(storedComposed.OriginalFileName, storedDecomposed.OriginalFileName);
    }

    [Theory]
    [InlineData("invoice‮pdf.exe")] // right-to-left override
    [InlineData("invoice‪pdf.exe")] // left-to-right embedding
    [InlineData("invoice‫pdf.exe")] // right-to-left embedding
    [InlineData("invoice‬pdf.exe")] // pop directional formatting
    [InlineData("invoice‭pdf.exe")] // left-to-right override (LRO)
    [InlineData("invoice⁦pdf.exe")] // left-to-right isolate
    [InlineData("invoice⁧pdf.exe")] // right-to-left isolate
    [InlineData("invoice⁨pdf.exe")] // first strong isolate
    [InlineData("invoice⁩pdf.exe")] // pop directional isolate
    [InlineData("invoice‎pdf.exe")] // left-to-right mark
    [InlineData("invoice‏pdf.exe")] // right-to-left mark
    [InlineData("invoice​pdf.exe")] // zero width space
    [InlineData("invoice﻿pdf.exe")] // zero width no-break space / BOM
    public async Task Bidi_and_deceptive_format_characters_are_rejected(string fileName)
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x")).AsTask());
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config")]
    [InlineData("..")]
    [InlineData(".")]
    public async Task Traversal_sequences_and_reserved_names_are_rejected(string fileName)
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x")).AsTask());
    }

    [Fact]
    public async Task Control_characters_are_rejected()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        const string fileName = "invoice.pdf";

        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), fileName, "text/plain", Content("x")).AsTask());
    }

    [Fact]
    public async Task Filename_at_maximum_length_is_accepted_and_beyond_is_rejected()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var atLimit = new string('a', 251) + ".txt"; // exactly 255 characters
        var overLimit = new string('a', 252) + ".txt"; // 256 characters

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), atLimit, "text/plain", Content("x"));

        Assert.Equal(255, metadata.OriginalFileName.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => storage.StoreAsync(
            context, DurableWorkTestSupport.TenantWideScope(context), overLimit, "text/plain", Content("x")).AsTask());
    }

    // -------------------------------------------------------------------
    // M-9: returned content is a defensive copy
    // -------------------------------------------------------------------

    [Fact]
    public async Task Mutating_returned_read_content_does_not_affect_a_second_read()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "immutable.txt", "text/plain", Content("original"));

        var first = await storage.ReadAsync(context, metadata.ObjectId);
        Array.Clear(first.Content!, 0, first.Content!.Length);
        var second = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal("original", Encoding.UTF8.GetString(second.Content!));
    }

    [Fact]
    public async Task Mutating_caller_buffer_after_store_does_not_affect_stored_content()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var bytes = Encoding.UTF8.GetBytes("original");
        using var stream = new MemoryStream(bytes);

        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "input.txt", "text/plain", stream);
        Array.Clear(bytes, 0, bytes.Length);
        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal("original", Encoding.UTF8.GetString(read.Content!));
    }

    [Fact]
    public async Task Mutating_returned_overwrite_content_does_not_affect_a_subsequent_read()
    {
        var context = CreateContext();
        var storage = new InMemoryPrivateObjectStorage();
        var metadata = await storage.StoreAsync(context, DurableWorkTestSupport.TenantWideScope(context), "double.txt", "text/plain", Content("v1"));

        var overwritten = await storage.OverwriteAsync(context, metadata.ObjectId, metadata.ConcurrencyVersion, Content("v2"));
        Array.Clear(overwritten.Content!, 0, overwritten.Content!.Length);
        var read = await storage.ReadAsync(context, metadata.ObjectId);

        Assert.Equal("v2", Encoding.UTF8.GetString(read.Content!));
    }

    // -------------------------------------------------------------------
    // L-4: dead enum member removed
    // -------------------------------------------------------------------

    [Fact]
    public void AnonymousDenied_outcome_no_longer_exists()
    {
        Assert.DoesNotContain("AnonymousDenied", Enum.GetNames<PrivateFileAccessOutcome>());
    }

    // -------------------------------------------------------------------
    // M-7: bounded notification retry with terminal dead-letter
    // -------------------------------------------------------------------

    [Fact]
    public async Task Notification_first_attempt_and_retry_transitions_are_explicit()
    {
        var context = CreateContext();
        var failFirst = true;
        var adapter = new InMemoryNotificationAdapter(_ =>
        {
            if (!failFirst)
            {
                return null;
            }

            failFirst = false;
            return DurableWorkFailureCategory.ProviderUnavailable;
        });
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-first-retry");

        var firstAttempt = await adapter.DeliverAsync(context, intent);
        Assert.Equal(NotificationDeliveryState.RetryScheduled, firstAttempt.State);
        Assert.Equal(1, intent.AttemptCount);

        var retry = await adapter.DeliverAsync(context, intent);
        Assert.Equal(NotificationDeliveryState.Delivered, retry.State);
        Assert.Equal(2, intent.AttemptCount);
    }

    [Fact]
    public async Task Notification_dead_letters_at_max_attempts_and_never_retries_again()
    {
        var context = CreateContext();
        var invocations = 0;
        var adapter = new InMemoryNotificationAdapter(_ =>
        {
            invocations++;
            return DurableWorkFailureCategory.ProviderUnavailable;
        });
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-bounded");

        NotificationDeliveryResult? last = null;
        for (var attempt = 0; attempt < TenantNotificationIntent.MaxDeliveryAttempts; attempt++)
        {
            last = await adapter.DeliverAsync(context, intent);
        }

        Assert.Equal(NotificationDeliveryState.DeadLetter, last!.State);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, intent.AttemptCount);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, invocations);

        var afterDeadLetter = await adapter.DeliverAsync(context, intent);

        Assert.Equal(NotificationDeliveryState.DeadLetter, afterDeadLetter.State);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, intent.AttemptCount);
        Assert.Equal(TenantNotificationIntent.MaxDeliveryAttempts, invocations);
    }

    [Fact]
    public async Task Notification_success_before_max_attempts_stops_further_delivery()
    {
        var context = CreateContext();
        var callCount = 0;
        var adapter = new InMemoryNotificationAdapter(_ =>
        {
            callCount++;
            return callCount < 2 ? DurableWorkFailureCategory.ProviderUnavailable : null;
        });
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-success-before-max");

        var first = await adapter.DeliverAsync(context, intent);
        var second = await adapter.DeliverAsync(context, intent);
        var third = await adapter.DeliverAsync(context, intent);

        Assert.Equal(NotificationDeliveryState.RetryScheduled, first.State);
        Assert.Equal(NotificationDeliveryState.Delivered, second.State);
        Assert.True(third.Duplicate);
        Assert.Equal(2, intent.AttemptCount);
    }

    [Fact]
    public async Task Notification_cancellation_is_explicit_and_does_not_mutate_state()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-cancel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => adapter.DeliverAsync(context, intent, cts.Token).AsTask());

        Assert.Equal(NotificationDeliveryState.Pending, intent.DeliveryState);
        Assert.Equal(0, intent.AttemptCount);
    }

    [Fact]
    public async Task Concurrent_duplicate_delivery_claims_are_a_single_effect()
    {
        var context = CreateContext();
        var adapter = new InMemoryNotificationAdapter();
        var intent = TenantNotificationIntent.Create(context, DurableWorkTestSupport.TenantWideScope(context), VerifiedRecipient(context.TenantId), "welcome", "en", "notify-concurrent");

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => adapter.DeliverAsync(context, intent).AsTask()));

        Assert.Single(results, result => result.Delivered && !result.Duplicate);
        Assert.Equal(7, results.Count(result => result.Duplicate));
        Assert.Equal(1, intent.AttemptCount);
    }

    // -------------------------------------------------------------------
    // M-8: notification recipient must be Identity-verified for the Tenant
    // -------------------------------------------------------------------

    [Fact]
    public async Task Active_recipient_with_membership_in_the_exact_tenant_is_authorized()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(userId, tenant);
        var context = CallerContext(tenant);

        var result = await service.AuthorizeAsync(context, new NotificationRecipientReference(userId.Value));

        Assert.True(result.Allowed);
        Assert.Equal(tenant, result.Recipient!.TenantId);
        Assert.Equal(userId.Value, result.Recipient.UserId);
    }

    [Fact]
    public async Task Recipient_belonging_to_a_foreign_tenant_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var ownTenant = new TenantId(Guid.NewGuid());
        var foreignTenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(userId, foreignTenant);

        var result = await service.AuthorizeAsync(CallerContext(ownTenant), new NotificationRecipientReference(userId.Value));

        Assert.False(result.Allowed);
        Assert.Null(result.Recipient);
    }

    [Fact]
    public async Task Unknown_recipient_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());

        var result = await service.AuthorizeAsync(CallerContext(tenant), new NotificationRecipientReference(Guid.NewGuid()));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Suspended_global_user_recipient_is_denied()
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        service.AddMembership(userId, tenant);
        service.Store.Users[userId].Status = GlobalUserStatus.Suspended;

        var result = await service.AuthorizeAsync(CallerContext(tenant), new NotificationRecipientReference(userId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task Suspended_membership_recipient_is_denied() =>
        await AssertNonActiveMembershipDeniedAsync(MembershipStatus.Suspended);

    [Fact]
    public async Task Revoked_membership_recipient_is_denied() =>
        await AssertNonActiveMembershipDeniedAsync(MembershipStatus.Revoked);

    [Fact]
    public async Task Pending_invitation_membership_recipient_is_denied() =>
        await AssertNonActiveMembershipDeniedAsync(MembershipStatus.PendingInvitation);

    private static async Task AssertNonActiveMembershipDeniedAsync(MembershipStatus status)
    {
        var service = new IdentityAuthorizationService();
        var tenant = new TenantId(Guid.NewGuid());
        var userId = service.CreateUser($"recipient-{Guid.NewGuid():N}@example.com", "Sup3r$ecret!1");
        var membershipId = service.AddMembership(userId, tenant);
        service.Store.Memberships[membershipId].Status = status;

        var result = await service.AuthorizeAsync(CallerContext(tenant), new NotificationRecipientReference(userId.Value));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Notification_recipient_authorizer_requires_a_trusted_tenant_context_not_platform_governance()
    {
        var parameter = typeof(INotificationRecipientAuthorizer)
            .GetMethod(nameof(INotificationRecipientAuthorizer.AuthorizeAsync))!
            .GetParameters()[0];

        Assert.Equal(typeof(TenantContext), parameter.ParameterType);
        Assert.NotEqual(typeof(PlatformGovernanceContext), parameter.ParameterType);
    }

    // -------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------

    private static TenantContext CreateContext(TenantId? tenantId = null) =>
        TenantContext.ForOrdinaryMembership(
            tenantId ?? new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private static TenantContext CallerContext(TenantId tenantId) =>
        TenantContext.ForOrdinaryMembership(
            tenantId,
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private static VerifiedNotificationRecipient VerifiedRecipient(TenantId tenantId) =>
        new(tenantId, Guid.NewGuid());

    private static MemoryStream Content(string value) =>
        new(Encoding.UTF8.GetBytes(value));
}
