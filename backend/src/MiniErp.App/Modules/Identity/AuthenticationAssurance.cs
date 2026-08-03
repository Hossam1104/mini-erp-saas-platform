namespace MiniErp.App.Modules.Identity;

internal enum AuthenticationAssuranceType
{
    Mfa = 1,
    FreshAuthentication = 2
}

internal sealed record AuthenticationAssuranceEvidence(
    UserId UserId,
    SessionId SessionId,
    AuthenticationAssuranceType Type,
    string? Operation,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string SourceId,
    bool SingleUse);

/// <summary>
/// Trusted seam for authentication factors. Production providers must validate
/// opaque, server-issued evidence; they must never accept client assertions.
/// </summary>
internal interface IAuthenticationAssuranceEvidenceSource
{
    string SourceId { get; }

    bool TryValidateMfaEvidence(
        string opaqueEvidence,
        UserId expectedUserId,
        SessionId expectedSessionId,
        DateTimeOffset now,
        out AuthenticationAssuranceEvidence evidence);

    bool TryValidateFreshAuthenticationEvidence(
        string opaqueEvidence,
        UserId expectedUserId,
        SessionId expectedSessionId,
        string operation,
        DateTimeOffset now,
        out AuthenticationAssuranceEvidence evidence);
}

/// <summary>
/// The Release 1 seam deliberately has no production MFA provider yet.
/// </summary>
internal sealed class UnavailableAuthenticationAssuranceEvidenceSource : IAuthenticationAssuranceEvidenceSource
{
    internal const string UnavailableSourceId = "unavailable";

    public string SourceId => UnavailableSourceId;

    public bool TryValidateMfaEvidence(
        string opaqueEvidence,
        UserId expectedUserId,
        SessionId expectedSessionId,
        DateTimeOffset now,
        out AuthenticationAssuranceEvidence evidence)
    {
        evidence = null!;
        return false;
    }

    public bool TryValidateFreshAuthenticationEvidence(
        string opaqueEvidence,
        UserId expectedUserId,
        SessionId expectedSessionId,
        string operation,
        DateTimeOffset now,
        out AuthenticationAssuranceEvidence evidence)
    {
        evidence = null!;
        return false;
    }
}
