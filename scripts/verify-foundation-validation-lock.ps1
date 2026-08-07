<#
.SYNOPSIS
    Focused verification of the Foundation validation lock (MESP-94 F1/F2).
    Exercises the exact same `FoundationValidationLock.ps1` functions that
    `validate-foundation.ps1` uses, via real separate processes, and proves:

      1. an active owner prevents another run from entering cleanup;
      2. a second same-user process/session cannot bypass the lock;
      3. an abandoned/interrupted owner is recovered safely;
      4. the lock is released after normal completion;
      5. the lock is released after a simulated validation failure.

.DESCRIPTION
    Run with no arguments to execute all five checks and print a PASS/FAIL
    summary; the process exits non-zero if any check fails. `-ChildMode` is
    an internal parameter used only when this script re-invokes itself as a
    lock-holding child process for checks 1-3; it is not a normal entry
    point.
#>

[CmdletBinding()]
param(
    [ValidateSet('Orchestrate', 'Hold', 'AbandonHold')]
    [string]$ChildMode = 'Orchestrate',

    [int]$HoldSeconds = 6,

    [string]$MarkerPath
)

. (Join-Path $PSScriptRoot 'FoundationValidationLock.ps1')

if ($ChildMode -ne 'Orchestrate') {
    # Child-process mode: acquire the real production lock (via the same
    # Wait-FoundationValidationLock helper validate-foundation.ps1 uses,
    # so an already-abandoned lock from a prior failed run is recovered
    # instead of crashing this child), signal the orchestrator once held,
    # then either release normally (Hold) or terminate the process while
    # still holding it (AbandonHold) to produce a genuine OS-abandoned
    # mutex -- the same condition a crashed or killed validation run would
    # leave behind.
    $mutex = New-FoundationValidationLock
    $lockResult = Wait-FoundationValidationLock -Mutex $mutex -Timeout ([TimeSpan]::FromSeconds(30))
    if (-not $lockResult.Acquired) {
        Set-Content -LiteralPath $MarkerPath -Value 'NOT_ACQUIRED'
        exit 1
    }
    Set-Content -LiteralPath $MarkerPath -Value 'ACQUIRED'
    Start-Sleep -Seconds $HoldSeconds
    if ($ChildMode -eq 'AbandonHold') {
        [Environment]::Exit(0)
    }
    $mutex.ReleaseMutex()
    $mutex.Dispose()
    exit 0
}

$results = New-Object System.Collections.Generic.List[object]
$markerDir = Join-Path ([System.IO.Path]::GetTempPath()) ("MiniErpLockVerify_{0}" -f ([Guid]::NewGuid().ToString('N').Substring(0, 8)))
New-Item -ItemType Directory -Path $markerDir -Force | Out-Null

function Add-Result {
    param([string]$Name, [bool]$Passed, [string]$Detail)
    $results.Add([pscustomobject]@{ Check = $Name; Result = if ($Passed) { 'PASS' } else { 'FAIL' }; Detail = $Detail })
    Write-Host "[$(if ($Passed) { 'PASS' } else { 'FAIL' })] $Name -- $Detail"
}

function Wait-ForMarker {
    param([string]$Path, [int]$TimeoutSeconds = 15)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return (Get-Content -LiteralPath $Path -Raw).Trim()
        }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

try {
    # --- Checks 1 & 2: an active owner blocks a second same-user process,
    #     which cannot bypass the lock and therefore cannot enter cleanup ---
    $marker12 = Join-Path $markerDir 'holder-12.txt'
    $holder = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
        '-ChildMode', 'Hold', '-HoldSeconds', '6', '-MarkerPath', "`"$marker12`""
    ) -PassThru -WindowStyle Hidden

    $state = Wait-ForMarker -Path $marker12 -TimeoutSeconds 15
    if ($state -ne 'ACQUIRED') {
        Add-Result 'Active owner blocks a second process (setup)' $false "Holder process never reported ACQUIRED (got '$state')."
    }
    else {
        $waiter = New-FoundationValidationLock
        try {
            $waitResult = Wait-FoundationValidationLock -Mutex $waiter -Timeout ([TimeSpan]::FromSeconds(2))
            $bypassed = $waitResult.Acquired
            Add-Result 'Second same-user process cannot bypass the lock' (-not $bypassed) "WaitOne while holder is active returned $bypassed (expected `$false)."
            Add-Result 'Active owner prevents entering cleanup' (-not $bypassed) 'A real validation run would throw here and never reach stale-database cleanup, since it never acquired the lock.'
            if ($bypassed) {
                $waiter.ReleaseMutex()
            }
        }
        finally {
            $waiter.Dispose()
        }
    }

    Wait-Process -Id $holder.Id -Timeout 20 -ErrorAction SilentlyContinue

    # --- Check 3: an abandoned/interrupted owner is recovered safely ---
    $marker3 = Join-Path $markerDir 'holder-3.txt'
    $abandoner = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
        '-ChildMode', 'AbandonHold', '-HoldSeconds', '2', '-MarkerPath', "`"$marker3`""
    ) -PassThru -WindowStyle Hidden

    $state3 = Wait-ForMarker -Path $marker3 -TimeoutSeconds 15
    if ($state3 -ne 'ACQUIRED') {
        Add-Result 'Abandoned lock recovery (setup)' $false "Abandoning process never reported ACQUIRED (got '$state3')."
    }
    else {
        # The recovering handle must be opened WHILE the abandoner still
        # holds the named kernel object, before that process exits. If no
        # process holds an open handle at the moment the sole owner
        # terminates, Windows destroys the named object outright (there is
        # nothing left to mark abandoned) and the next opener simply gets a
        # fresh, uncontested mutex -- which would silently defeat this
        # check. Opening the handle first, then blocking on it, is what
        # lets the OS actually deliver the abandonment to this waiter.
        $recoveryMutex = New-FoundationValidationLock
        $recoveryResult = $null
        try {
            $recoveryResult = Wait-FoundationValidationLock -Mutex $recoveryMutex -Timeout ([TimeSpan]::FromSeconds(15))
            Add-Result 'Abandoned owner is recovered safely' ($recoveryResult.Acquired -and $recoveryResult.RecoveredFromAbandonedLock) "AbandonedMutexException observed=$($recoveryResult.RecoveredFromAbandonedLock); ownership recovered=$($recoveryResult.Acquired)."
        }
        finally {
            if ($null -ne $recoveryResult -and $recoveryResult.Acquired) {
                $recoveryMutex.ReleaseMutex()
            }
            $recoveryMutex.Dispose()
        }

        Wait-Process -Id $abandoner.Id -Timeout 20 -ErrorAction SilentlyContinue
    }

    # --- Check 4: lock is released after normal completion ---
    $normalMutex = New-FoundationValidationLock
    $normalResult = Wait-FoundationValidationLock -Mutex $normalMutex -Timeout ([TimeSpan]::FromSeconds(10))
    if ($normalResult.Acquired) {
        $normalMutex.ReleaseMutex()
    }
    $normalMutex.Dispose()

    $reacquireMutex = New-FoundationValidationLock
    try {
        $reacquireResult = Wait-FoundationValidationLock -Mutex $reacquireMutex -Timeout ([TimeSpan]::FromSeconds(3))
        Add-Result 'Lock released after normal completion' $reacquireResult.Acquired "Immediate re-acquisition after release returned $($reacquireResult.Acquired)."
        if ($reacquireResult.Acquired) {
            $reacquireMutex.ReleaseMutex()
        }
    }
    finally {
        $reacquireMutex.Dispose()
    }

    # --- Check 5: lock is released after a simulated validation failure ---
    $failureMutex = New-FoundationValidationLock
    $failureResult = Wait-FoundationValidationLock -Mutex $failureMutex -Timeout ([TimeSpan]::FromSeconds(10))
    try {
        if ($failureResult.Acquired) {
            try {
                throw 'Simulated validation failure for MESP-94 F2 lock-release verification.'
            }
            finally {
                $failureMutex.ReleaseMutex()
            }
        }
    }
    catch {
        # Expected: the simulated failure itself; only the release behavior
        # in the finally block above is under test.
    }
    finally {
        $failureMutex.Dispose()
    }

    $reacquireAfterFailureMutex = New-FoundationValidationLock
    try {
        $reacquireAfterFailureResult = Wait-FoundationValidationLock -Mutex $reacquireAfterFailureMutex -Timeout ([TimeSpan]::FromSeconds(3))
        Add-Result 'Lock released after validation failure' $reacquireAfterFailureResult.Acquired "Immediate re-acquisition after a failed-and-caught run returned $($reacquireAfterFailureResult.Acquired)."
        if ($reacquireAfterFailureResult.Acquired) {
            $reacquireAfterFailureMutex.ReleaseMutex()
        }
    }
    finally {
        $reacquireAfterFailureMutex.Dispose()
    }
}
finally {
    Remove-Item -LiteralPath $markerDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '--- Foundation validation lock verification summary ---'
$results | Format-Table -AutoSize | Out-Host

$failed = @($results | Where-Object { $_.Result -eq 'FAIL' })
if ($failed.Count -gt 0) {
    Write-Error "$($failed.Count) of $($results.Count) lock verification check(s) failed."
    exit 1
}

Write-Host "All $($results.Count) Foundation validation lock checks passed."
exit 0
