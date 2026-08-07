<#
.SYNOPSIS
    Shared Foundation validation lock. Dot-sourced by both the canonical
    `validate-foundation.ps1` command and its focused lock verification
    harness so both exercise the identical lock logic (MESP-94 F1/F2).

.DESCRIPTION
    The automatic `MSSQLLocalDB` instance is owned/scoped by Windows user,
    not by terminal/logon session, so two processes for the same Windows
    user running from different sessions can reach the identical LocalDB
    instance. A session-scoped `Local\` named object is not visible to a
    second logon session for that same user, so this lock instead lives in
    the machine-wide `Global\` kernel namespace, with its name suffixed by
    the current user's SID and its access control list restricted to that
    exact SID.

    This coordinates every validation process for the same Windows user
    across sessions, cannot be opened or signaled by a different Windows
    user (the ACL grants no rights to anyone else), and does not serialize
    unrelated Windows users against each other, since each user's lock has a
    distinct name. It does not prove that no other process on the machine
    can ever interfere with LocalDB by some unrelated means -- only that
    this specific lock is user-scoped and cross-session.
#>

function Get-FoundationValidationLockName {
    [OutputType([string])]
    param()

    $currentSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    return "Global\MiniErpFoundationValidation_$($currentSid.Value)"
}

function New-FoundationValidationLock {
    [OutputType([System.Threading.Mutex])]
    param()

    $currentSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $lockName = Get-FoundationValidationLockName

    $mutexSecurity = New-Object System.Security.AccessControl.MutexSecurity
    $accessRule = New-Object System.Security.AccessControl.MutexAccessRule(
        $currentSid,
        [System.Security.AccessControl.MutexRights]::FullControl,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $mutexSecurity.AddAccessRule($accessRule)

    $createdNew = $false
    return New-Object System.Threading.Mutex($false, $lockName, [ref]$createdNew, $mutexSecurity)
}

function Wait-FoundationValidationLock {
    <#
    Waits on the Foundation validation lock and safely recovers from an
    abandoned lock (MESP-94 F2): if the prior owner for this Windows user
    terminated without releasing it, ownership transfers to this caller,
    Acquired is true, and RecoveredFromAbandonedLock is true so the caller
    can log the interrupted-prior-owner condition and continue into the same
    stale-database discovery/cleanup flow as an uncontested acquisition --
    never treated as an ordinary competing active run.
    #>
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory = $true)][System.Threading.Mutex]$Mutex,
        [Parameter(Mandatory = $true)][TimeSpan]$Timeout
    )

    $acquired = $false
    $recoveredFromAbandonedLock = $false
    try {
        $acquired = $Mutex.WaitOne($Timeout)
    }
    catch [System.Threading.AbandonedMutexException] {
        $acquired = $true
        $recoveredFromAbandonedLock = $true
    }

    return [pscustomobject]@{
        Acquired                   = $acquired
        RecoveredFromAbandonedLock = $recoveredFromAbandonedLock
    }
}
