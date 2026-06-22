param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePath,

    [Parameter(Mandatory = $false)]
    [string] $TargetPath = $PSScriptRoot,

    [Parameter(Mandatory = $false)]
    [int] $HostProcessId = 0,

    [Parameter(Mandatory = $false)]
    [int] $WaitSeconds = 300,

    [Parameter(Mandatory = $false)]
    [string[]] $FilePatterns = @("VeloxapEDGEWpfLib*.*"),

    [Parameter(Mandatory = $false)]
    [switch] $KeepOpen
)

$ErrorActionPreference = "Stop"

function Normalize-PathArgument {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    return $Value.Trim().Trim('"')
}

function Write-UpdateLog {
    param([string] $Message)

    $line = "{0} {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line

    try {
        $logDirectory = $TargetPath
        if ([string]::IsNullOrWhiteSpace($logDirectory)) {
            $logDirectory = $PSScriptRoot
        }

        if (-not (Test-Path -LiteralPath $logDirectory -PathType Container)) {
            New-Item -Path $logDirectory -ItemType Directory -Force | Out-Null
        }

        $logPath = Join-Path -Path $logDirectory -ChildPath "VeloxapEdgeUpdate.log"
        Add-Content -Path $logPath -Value $line
    }
    catch {
        $fallbackLogPath = Join-Path -Path $env:TEMP -ChildPath "VeloxapEdgeUpdate.log"
        Add-Content -Path $fallbackLogPath -Value $line
        Write-Host "Could not write target log. Fallback log: $fallbackLogPath"
    }
}

function Complete-UpdateScript {
    param([int] $ExitCode)

    if ($KeepOpen) {
        Write-Host ""
        Read-Host "Updater finished. Press Enter to close this window"
        return
    }

    [Environment]::Exit($ExitCode)
}

try {
    $SourcePath = Normalize-PathArgument $SourcePath
    $TargetPath = Normalize-PathArgument $TargetPath

    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        $TargetPath = $PSScriptRoot
    }

    if (-not (Test-Path -LiteralPath $TargetPath -PathType Container)) {
        New-Item -Path $TargetPath -ItemType Directory -Force | Out-Null
    }

    Write-UpdateLog "Updater started. SourcePath=$SourcePath TargetPath=$TargetPath HostProcessId=$HostProcessId"

    if ($HostProcessId -gt 0) {
        Write-UpdateLog "Waiting for host process $HostProcessId to exit."
        $deadline = (Get-Date).AddSeconds($WaitSeconds)

        while ((Get-Date) -lt $deadline) {
            $process = Get-Process -Id $HostProcessId -ErrorAction SilentlyContinue
            if ($null -eq $process) {
                break
            }

            Start-Sleep -Seconds 2
        }

        $process = Get-Process -Id $HostProcessId -ErrorAction SilentlyContinue
        if ($null -ne $process) {
            throw "Timed out waiting for host process $HostProcessId to exit."
        }
    }

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        throw "Source path does not exist: $SourcePath"
    }

    $copiedCount = 0
    foreach ($pattern in $FilePatterns) {
        $files = Get-ChildItem -Path $SourcePath -Filter $pattern -File
        foreach ($file in $files) {
            $destination = Join-Path -Path $TargetPath -ChildPath $file.Name
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            Write-UpdateLog "Copied $($file.FullName) to $destination."
            $copiedCount++
        }
    }

    if ($copiedCount -eq 0) {
        throw "No library files were found in source path: $SourcePath"
    }

    Write-UpdateLog "Update completed. Copied $copiedCount file(s)."
    Complete-UpdateScript 0
}
catch {
    Write-UpdateLog "Update failed: $($_.Exception.Message)"
    Complete-UpdateScript 1
}
