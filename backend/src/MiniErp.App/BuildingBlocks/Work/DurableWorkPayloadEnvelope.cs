#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text.Json;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>Safe, bounded failure for payload capture/decode. Never carries payload content.</summary>
public sealed class DurableWorkPayloadException : Exception
{
    public DurableWorkPayloadException(string message) : base(message)
    {
    }
}

/// <summary>
/// Explicit, bounded identity for one registered payload type. It is never a
/// CLR assembly-qualified or class name; only an approved registry entry can
/// resolve it to a concrete type.
/// </summary>
public readonly struct DurableWorkPayloadTypeId : IEquatable<DurableWorkPayloadTypeId>
{
    public DurableWorkPayloadTypeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 96)
        {
            throw new ArgumentException("A payload type identifier is required and bounded.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("A payload type identifier must not contain a control character.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public bool Equals(DurableWorkPayloadTypeId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DurableWorkPayloadTypeId other && Equals(other);

    public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(DurableWorkPayloadTypeId left, DurableWorkPayloadTypeId right) => left.Equals(right);

    public static bool operator !=(DurableWorkPayloadTypeId left, DurableWorkPayloadTypeId right) => !left.Equals(right);
}

/// <summary>Handler-specific, explicit codec used only through the payload registry.</summary>
public interface IDurableWorkPayloadCodec<TPayload>
    where TPayload : IWorkPayload
{
    byte[] Encode(TPayload payload);

    TPayload Decode(ReadOnlySpan<byte> bytes);
}

/// <summary>
/// Bounded default JSON codec. It never emits polymorphic type metadata: the
/// concrete <typeparamref name="TPayload"/> is fixed at registration, not
/// resolved from payload-controlled data.
/// </summary>
public sealed class JsonDurableWorkPayloadCodec<TPayload> : IDurableWorkPayloadCodec<TPayload>
    where TPayload : IWorkPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 16,
        WriteIndented = false,
        PropertyNameCaseInsensitive = false
    };

    public byte[] Encode(TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(payload, Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new DurableWorkPayloadException("The payload could not be encoded safely.");
        }
    }

    public TPayload Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var value = JsonSerializer.Deserialize<TPayload>(bytes, Options);
            return value ?? throw new DurableWorkPayloadException("The decoded payload must not be null.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new DurableWorkPayloadException("The payload could not be decoded safely.");
        }
    }
}

/// <summary>
/// Immutable, checksummed snapshot of one registered payload. Internal bytes
/// are private; every external access returns a defensive copy.
/// </summary>
public sealed class DurableWorkPayloadEnvelope
{
    /// <summary>Bounded encoded-payload size. Foundation-local; not a production capacity claim.</summary>
    public const int MaxPayloadSizeBytes = 65536;

    private readonly byte[] bytes;

    private DurableWorkPayloadEnvelope(DurableWorkPayloadTypeId typeId, byte[] bytes, string checksum)
    {
        TypeId = typeId;
        this.bytes = bytes;
        Checksum = checksum;
    }

    public DurableWorkPayloadTypeId TypeId { get; }

    /// <summary>SHA-256 checksum, hex-encoded, over the canonical encoded bytes.</summary>
    public string Checksum { get; }

    public int Length => bytes.Length;

    /// <summary>Captures an immutable snapshot. Only the payload registry may call this.</summary>
    internal static DurableWorkPayloadEnvelope Capture(DurableWorkPayloadTypeId typeId, byte[] encodedBytes)
    {
        ArgumentNullException.ThrowIfNull(encodedBytes);
        if (encodedBytes.Length == 0 || encodedBytes.Length > MaxPayloadSizeBytes)
        {
            throw new DurableWorkPayloadException("The encoded payload size is out of the bounded range.");
        }

        var snapshot = new byte[encodedBytes.Length];
        Array.Copy(encodedBytes, snapshot, encodedBytes.Length);
        return new DurableWorkPayloadEnvelope(typeId, snapshot, ComputeChecksum(snapshot));
    }

    /// <summary>Returns a defensive copy; mutating it never affects the stored envelope.</summary>
    public byte[] GetBytesCopy()
    {
        var copy = new byte[bytes.Length];
        Array.Copy(bytes, copy, bytes.Length);
        if (!string.Equals(ComputeChecksum(copy), Checksum, StringComparison.Ordinal))
        {
            throw new DurableWorkPayloadException("The stored payload checksum does not match.");
        }

        return copy;
    }

    /// <summary>Test-only fault injection simulating a corrupted stored envelope.</summary>
    internal void TamperForValidation()
    {
        if (bytes.Length > 0)
        {
            bytes[0] ^= 0xFF;
        }
    }

    private static string ComputeChecksum(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
}

/// <summary>
/// Authoritative payload registry. Registration binds a bounded explicit type
/// identifier to one concrete payload type and its codec; decode selection is
/// a table lookup, never CLR reflection over payload-controlled data.
/// </summary>
public interface IDurableWorkPayloadRegistry
{
    void Register<TPayload>(DurableWorkPayloadTypeId typeId, IDurableWorkPayloadCodec<TPayload> codec)
        where TPayload : IWorkPayload;

    DurableWorkPayloadEnvelope Capture<TPayload>(TPayload payload)
        where TPayload : IWorkPayload;

    TPayload Decode<TPayload>(DurableWorkPayloadEnvelope envelope)
        where TPayload : IWorkPayload;
}

/// <summary>Bounded in-process payload registry. Registration is expected at composition time.</summary>
public sealed class DurableWorkPayloadRegistry : IDurableWorkPayloadRegistry
{
    private readonly object syncRoot = new();
    private readonly Dictionary<string, RegisteredCodec> byTypeId = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, DurableWorkPayloadTypeId> typeIdByClrType = [];

    public void Register<TPayload>(DurableWorkPayloadTypeId typeId, IDurableWorkPayloadCodec<TPayload> codec)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(codec);
        lock (syncRoot)
        {
            var entry = new RegisteredCodec(
                typeof(TPayload),
                payload => codec.Encode((TPayload)payload),
                encoded => codec.Decode(encoded));
            if (!byTypeId.TryAdd(typeId.Value, entry))
            {
                throw new ArgumentException("A payload type identifier may be registered only once.", nameof(typeId));
            }

            if (!typeIdByClrType.TryAdd(typeof(TPayload), typeId))
            {
                byTypeId.Remove(typeId.Value);
                throw new ArgumentException("A payload CLR type may be registered only once.", nameof(TPayload));
            }
        }
    }

    public DurableWorkPayloadEnvelope Capture<TPayload>(TPayload payload)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(payload);
        RegisteredCodec registered;
        DurableWorkPayloadTypeId typeId;
        lock (syncRoot)
        {
            if (!typeIdByClrType.TryGetValue(typeof(TPayload), out typeId)
                || !byTypeId.TryGetValue(typeId.Value, out registered!))
            {
                throw new DurableWorkPayloadException("The payload type is not registered.");
            }
        }

        var bytes = registered.Encode(payload);
        return DurableWorkPayloadEnvelope.Capture(typeId, bytes);
    }

    public TPayload Decode<TPayload>(DurableWorkPayloadEnvelope envelope)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(envelope);
        RegisteredCodec registered;
        lock (syncRoot)
        {
            if (!byTypeId.TryGetValue(envelope.TypeId.Value, out registered!))
            {
                throw new DurableWorkPayloadException("The payload type is not registered.");
            }
        }

        if (registered.ClrType != typeof(TPayload))
        {
            throw new DurableWorkPayloadException("The payload type does not match the requested handler payload.");
        }

        var bytes = envelope.GetBytesCopy();
        return (TPayload)registered.Decode(bytes);
    }

    private sealed record RegisteredCodec(
        Type ClrType,
        Func<IWorkPayload, byte[]> Encode,
        Func<byte[], IWorkPayload> Decode);
}
