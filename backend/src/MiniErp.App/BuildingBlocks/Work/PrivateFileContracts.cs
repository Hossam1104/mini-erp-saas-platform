#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>Tenant-owned private-file metadata; object identity is opaque.</summary>
public sealed class PrivateFileMetadata : ITenantOwned
{
    internal PrivateFileMetadata(
        Guid objectId,
        TenantId tenantId,
        TenantWorkScope scope,
        string originalFileName,
        string contentType,
        long length,
        string sha256,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        ObjectId = objectId;
        TenantId = tenantId;
        Scope = scope;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        Length = length;
        Sha256 = sha256;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Disposition = PrivateFileDisposition.Available;
        ConcurrencyVersion = 1;
    }

    public Guid ObjectId { get; }

    public TenantId TenantId { get; }

    public TenantWorkScope Scope { get; }

    public string OriginalFileName { get; }

    public string ContentType { get; }

    public long Length { get; internal set; }

    public string Sha256 { get; internal set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? ExpiresAt { get; }

    public PrivateFileDisposition Disposition { get; internal set; }

    public long ConcurrencyVersion { get; internal set; }
}

/// <summary>Safe result of private-file access.</summary>
public sealed class PrivateFileAccessResult
{
    private PrivateFileAccessResult(
        PrivateFileAccessOutcome outcome,
        PrivateFileMetadata? metadata,
        byte[]? content)
    {
        Outcome = outcome;
        Metadata = metadata;
        Content = content;
    }

    public PrivateFileAccessOutcome Outcome { get; }

    public PrivateFileMetadata? Metadata { get; }

    /// <summary>Content is returned only for an allowed same-Tenant read.</summary>
    public byte[]? Content { get; }

    public bool Allowed => Outcome == PrivateFileAccessOutcome.Allowed;

    internal static PrivateFileAccessResult AllowedResult(PrivateFileMetadata metadata, byte[] content) =>
        new(PrivateFileAccessOutcome.Allowed, metadata, content);

    internal static PrivateFileAccessResult Denied(PrivateFileAccessOutcome outcome) =>
        new(outcome, null, null);
}

/// <summary>Private object-storage abstraction with no public or anonymous path.</summary>
public interface IPrivateObjectStorage
{
    ValueTask<PrivateFileMetadata> StoreAsync(
        TenantContext tenantContext,
        TenantWorkScope scope,
        string originalFileName,
        string contentType,
        Stream content,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default);

    ValueTask<PrivateFileAccessResult> ReadAsync(
        TenantContext tenantContext,
        Guid objectId,
        CancellationToken cancellationToken = default);

    ValueTask<PrivateFileAccessResult> OverwriteAsync(
        TenantContext tenantContext,
        Guid objectId,
        long expectedConcurrencyVersion,
        Stream content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deterministic local private-file adapter for tests/development only. It is
/// not a public object store and does not implement purge, signed URLs or
/// malware scanning.
/// </summary>
public sealed class InMemoryPrivateObjectStorage : IPrivateObjectStorage
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, (PrivateFileMetadata Metadata, byte[] Content)> objects = [];
    private readonly List<(TenantId TenantId, Guid ObjectId, PrivateFileAccessOutcome Outcome)> accessEvidence = [];

    public async ValueTask<PrivateFileMetadata> StoreAsync(
        TenantContext tenantContext,
        TenantWorkScope scope,
        string originalFileName,
        string contentType,
        Stream content,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(tenantContext, scope);
        ArgumentNullException.ThrowIfNull(content);
        var bytes = await ReadBytesAsync(content, cancellationToken);
        var safeName = SafeFileName(originalFileName);
        var safeType = SafeValue(contentType, nameof(contentType));
        var now = DateTimeOffset.UtcNow;
        var metadata = new PrivateFileMetadata(
            Guid.NewGuid(),
            tenantContext.TenantId,
            scope,
            safeName,
            safeType,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)),
            now,
            expiresAt);
        lock (syncRoot)
        {
            objects.Add(metadata.ObjectId, (metadata, bytes));
            accessEvidence.Add((metadata.TenantId, metadata.ObjectId, PrivateFileAccessOutcome.Allowed));
        }

        return metadata;
    }

    public ValueTask<PrivateFileAccessResult> ReadAsync(
        TenantContext tenantContext,
        Guid objectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (objectId == Guid.Empty)
        {
            return ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            if (!objects.TryGetValue(objectId, out var stored))
            {
                return ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));
            }

            if (stored.Metadata.TenantId != tenantContext.TenantId)
            {
                // Internal audit evidence preserves the granular truth; the
                // caller-visible outcome always folds to NotFound so a foreign
                // object can never be distinguished from a missing one (M-1).
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.TenantDenied));
                return ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound));
            }

            if (stored.Metadata.Disposition != PrivateFileDisposition.Available
                || stored.Metadata.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.Expired));
                return ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.Expired));
            }

            var checksum = Convert.ToHexString(SHA256.HashData(stored.Content));
            if (!string.Equals(checksum, stored.Metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                stored.Metadata.Disposition = PrivateFileDisposition.ChecksumFailed;
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.ChecksumFailed));
                return ValueTask.FromResult(PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.ChecksumFailed));
            }

            accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.Allowed));
            return ValueTask.FromResult(PrivateFileAccessResult.AllowedResult(stored.Metadata, stored.Content.ToArray()));
        }
    }

    public async ValueTask<PrivateFileAccessResult> OverwriteAsync(
        TenantContext tenantContext,
        Guid objectId,
        long expectedConcurrencyVersion,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(content);
        if (expectedConcurrencyVersion < 1)
        {
            return PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.ConcurrencyConflict);
        }

        var bytes = await ReadBytesAsync(content, cancellationToken);
        lock (syncRoot)
        {
            if (!objects.TryGetValue(objectId, out var stored))
            {
                return PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound);
            }

            if (stored.Metadata.TenantId != tenantContext.TenantId)
            {
                // Same fold as ReadAsync: a foreign object must not be
                // distinguishable from a missing one to the caller (M-1).
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.TenantDenied));
                return PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.NotFound);
            }

            // An object in any prohibited lifecycle state fails closed instead
            // of being silently overwritten (M-4): expired/disposed metadata,
            // or content whose checksum no longer matches the recorded hash.
            if (stored.Metadata.Disposition != PrivateFileDisposition.Available
                || stored.Metadata.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.Expired));
                return PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.Expired);
            }

            var existingChecksum = Convert.ToHexString(SHA256.HashData(stored.Content));
            if (!string.Equals(existingChecksum, stored.Metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                stored.Metadata.Disposition = PrivateFileDisposition.ChecksumFailed;
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.ChecksumFailed));
                return PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.ChecksumFailed);
            }

            if (stored.Metadata.ConcurrencyVersion != expectedConcurrencyVersion)
            {
                accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.ConcurrencyConflict));
                return PrivateFileAccessResult.Denied(PrivateFileAccessOutcome.ConcurrencyConflict);
            }

            objects[objectId] = (stored.Metadata, bytes);
            stored.Metadata.Length = bytes.LongLength;
            stored.Metadata.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
            stored.Metadata.ConcurrencyVersion++;
            accessEvidence.Add((tenantContext.TenantId, objectId, PrivateFileAccessOutcome.Allowed));
            return PrivateFileAccessResult.AllowedResult(stored.Metadata, bytes.ToArray());
        }
    }

    /// <summary>Test-only tamper hook; no production caller can access it.</summary>
    internal void TamperForValidation(Guid objectId, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        lock (syncRoot)
        {
            if (objects.TryGetValue(objectId, out var stored))
            {
                objects[objectId] = (stored.Metadata, content.ToArray());
            }
        }
    }

    internal IReadOnlyList<(TenantId TenantId, Guid ObjectId, PrivateFileAccessOutcome Outcome)> AccessEvidence =>
        accessEvidence.ToArray();

    internal bool ExistsForValidation(Guid objectId)
    {
        lock (syncRoot)
        {
            return objects.ContainsKey(objectId);
        }
    }

    private static void ValidateContext(TenantContext tenantContext, TenantWorkScope scope)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.TenantId != tenantContext.TenantId)
        {
            throw new ArgumentException("File scope must belong to the trusted Tenant.", nameof(scope));
        }
    }

    private static async Task<byte[]> ReadBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>
    /// Unicode bidirectional-override, embedding, isolate and other deceptive
    /// formatting characters that must never appear in a filename (M-5). Valid
    /// Arabic letters, digits and normalized Unicode text are unaffected --
    /// none of these code points are Arabic script.
    /// </summary>
    private static readonly char[] UnsafeFormatCharacters =
    [
        (char)0x200B, // zero width space
        (char)0x200C, // zero width non-joiner
        (char)0x200D, // zero width joiner
        (char)0x200E, // left-to-right mark
        (char)0x200F, // right-to-left mark
        (char)0x2060, // word joiner
        (char)0x202A, // left-to-right embedding
        (char)0x202B, // right-to-left embedding
        (char)0x202C, // pop directional formatting
        (char)0x202D, // left-to-right override
        (char)0x202E, // right-to-left override
        (char)0x2066, // left-to-right isolate
        (char)0x2067, // right-to-left isolate
        (char)0x2068, // first strong isolate
        (char)0x2069, // pop directional isolate
        (char)0xFEFF  // zero width no-break space / byte order mark
    ];

    private static string SafeFileName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Normalize composed/decomposed Unicode to one comparable form before
        // validation. A path separator anywhere in the supplied name fails
        // closed rather than being silently truncated to a leaf name -- a
        // caller-supplied value that looks like a path is rejected outright,
        // never tolerantly reinterpreted as one (M-5).
        var name = value.Normalize(NormalizationForm.FormC);
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 255
            || name is "." or ".."
            || name.Contains("..", StringComparison.Ordinal)
            || name.Contains('/')
            || name.Contains('\\')
            || name.Any(char.IsControl)
            || name.Any(UnsafeFormatCharacters.Contains))
        {
            throw new ArgumentException("Original filename is invalid or unsafe.", nameof(value));
        }

        return name;
    }

    private static string SafeValue(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Value is invalid or unsafe.", name);
        }

        return value.Trim();
    }
}
