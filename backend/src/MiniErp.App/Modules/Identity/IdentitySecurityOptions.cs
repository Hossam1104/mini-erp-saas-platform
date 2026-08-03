namespace MiniErp.App.Modules.Identity;

internal sealed class IdentitySecurityOptions
{
    internal TimeSpan AbsoluteSessionLifetime { get; init; } = TimeSpan.FromHours(8);

    internal TimeSpan InactivityTimeout { get; init; } = TimeSpan.FromMinutes(30);

    internal int MaxFailedAuthenticationAttempts { get; init; } = 5;

    internal TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    internal TimeSpan InvitationLifetime { get; init; } = TimeSpan.FromDays(7);

    internal TimeSpan RecoveryLifetime { get; init; } = TimeSpan.FromHours(1);

    // The exact reusable fresh-authentication window remains a downstream ADR.
    // This bounded seam uses a conservative technical default for deterministic tests.
    internal TimeSpan FreshAuthenticationWindow { get; init; } = TimeSpan.FromMinutes(5);

    internal static IdentitySecurityOptions Default { get; } = new();

    internal void Validate()
    {
        if (AbsoluteSessionLifetime <= TimeSpan.Zero || AbsoluteSessionLifetime > TimeSpan.FromHours(8))
        {
            throw new ArgumentOutOfRangeException(nameof(AbsoluteSessionLifetime));
        }

        if (InactivityTimeout <= TimeSpan.Zero || InactivityTimeout > AbsoluteSessionLifetime)
        {
            throw new ArgumentOutOfRangeException(nameof(InactivityTimeout));
        }

        if (MaxFailedAuthenticationAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFailedAuthenticationAttempts));
        }

        if (LockoutDuration <= TimeSpan.Zero || InvitationLifetime <= TimeSpan.Zero || RecoveryLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(LockoutDuration));
        }

        if (FreshAuthenticationWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FreshAuthenticationWindow));
        }
    }
}
