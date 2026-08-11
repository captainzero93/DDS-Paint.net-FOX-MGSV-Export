$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $RepoRoot

$LogPath = Join-Path $RepoRoot "BUILD_MGSV_LOG.txt"
$OutputDir = Join-Path $RepoRoot "BUILD_OUTPUT"

function Write-Section([string]$Text) {
    Write-Host ""
    Write-Host ("=" * 68) -ForegroundColor Cyan
    Write-Host ("  " + $Text) -ForegroundColor Cyan
    Write-Host ("=" * 68) -ForegroundColor Cyan
    Write-Host ""
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Fail([string]$Message) {
    Write-Host ""
    Write-Host "ERROR: $Message" -ForegroundColor Red
    Write-Host ""
    throw $Message
}

# Always keep a log even if the build itself fails.
try {
    if (Test-Path -LiteralPath $LogPath) {
        Remove-Item -LiteralPath $LogPath -Force
    }
    Start-Transcript -LiteralPath $LogPath -Force | Out-Null
} catch {
    Write-Host "WARNING: Could not start transcript: $($_.Exception.Message)" -ForegroundColor Yellow
}

try {
    Write-Section "MGSV DDSFileTypePlus - VS2026 Build"

    Write-Host "Repository:"
    Write-Host "  $RepoRoot"
    Write-Host ""

    $isAdmin = Test-IsAdministrator
    Write-Host ("Administrator: " + $(if ($isAdmin) { "YES" } else { "NO" }))
    if (-not $isAdmin) {
        Write-Host "  Build will still work. If successful, DLLs will be placed in BUILD_OUTPUT." -ForegroundColor Yellow
        Write-Host "  Run this BAT as Administrator only if you want automatic installation." -ForegroundColor Yellow
    }
    Write-Host ""

    # ------------------------------------------------------------
    # Paint.NET
    # ------------------------------------------------------------
    $paintCandidates = @(
        (Join-Path $env:ProgramFiles "paint.net"),
        "C:\Program Files\paint.net"
    ) | Select-Object -Unique

    $Pdn = $null
    foreach ($candidate in $paintCandidates) {
        if ($candidate -and (Test-Path -LiteralPath (Join-Path $candidate "PaintDotNet.Base.dll"))) {
            $Pdn = $candidate
            break
        }
    }

    if (-not $Pdn) {
        Write-Host "Paint.NET was not found in the normal Program Files location."
        Write-Host "Choose the folder containing PaintDotNet.Base.dll."
        Add-Type -AssemblyName System.Windows.Forms
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = "Select your Paint.NET installation folder"
        $dialog.ShowNewFolderButton = $false
        if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $Pdn = $dialog.SelectedPath
        }
    }

    if (-not $Pdn) {
        Fail "No Paint.NET installation folder was selected."
    }

    if (-not (Test-Path -LiteralPath (Join-Path $Pdn "PaintDotNet.Base.dll"))) {
        Fail "PaintDotNet.Base.dll was not found in '$Pdn'."
    }

    Write-Host "Paint.NET:"
    Write-Host "  $Pdn"
    Write-Host ""

    # ------------------------------------------------------------
    # Visual Studio / MSBuild
    # ------------------------------------------------------------
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    Write-Host "vswhere:"
    Write-Host "  $vswhere"

    $VsRoot = $null

    if (Test-Path -LiteralPath $vswhere) {
        $vsArgs = @(
            "-latest",
            "-products", "*",
            "-requires",
            "Microsoft.Component.MSBuild",
            "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
            "-property", "installationPath"
        )

        $vsResult = @(& $vswhere @vsArgs 2>$null)
        if ($LASTEXITCODE -eq 0 -and $vsResult.Count -gt 0) {
            $VsRoot = ($vsResult | Where-Object { $_ -and $_.Trim() } | Select-Object -First 1).Trim()
        }
    }

    # Fallback search. This intentionally doesn't depend on the edition name.
    if (-not $VsRoot) {
        $vsBase = Join-Path $env:ProgramFiles "Microsoft Visual Studio"
        if (Test-Path -LiteralPath $vsBase) {
            $vcvars = Get-ChildItem -LiteralPath $vsBase -Filter "vcvars64.bat" -File -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '\\VC\\Auxiliary\\Build\\vcvars64\.bat$' } |
                Sort-Object FullName -Descending |
                Select-Object -First 1

            if ($vcvars) {
                # ...\<edition>\VC\Auxiliary\Build\vcvars64.bat -> edition root
                $VsRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $vcvars.FullName)))
            }
        }
    }

    if (-not $VsRoot) {
        Fail "Visual Studio with MSBuild and the x64 C++ tools could not be found. Check the Visual Studio Installer 'Desktop development with C++' workload."
    }

    $MsBuild = Join-Path $VsRoot "MSBuild\Current\Bin\MSBuild.exe"
    if (-not (Test-Path -LiteralPath $MsBuild)) {
        Fail "MSBuild.exe was not found at '$MsBuild'."
    }

    Write-Host ""
    Write-Host "Visual Studio:"
    Write-Host "  $VsRoot"
    Write-Host "MSBuild:"
    Write-Host "  $MsBuild"
    Write-Host ""

    # ------------------------------------------------------------
    # .NET SDK
    # ------------------------------------------------------------
    $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        Fail "dotnet.exe was not found."
    }

    Write-Host ".NET SDKs:"
    $sdkLines = @(& dotnet --list-sdks)
    foreach ($line in $sdkLines) {
        Write-Host "  $line"
    }

    if ($sdkLines.Count -eq 0) {
        Fail "No .NET SDK is installed."
    }
    Write-Host ""

    # ------------------------------------------------------------
    # Build
    # ------------------------------------------------------------
    $Solution = Join-Path $RepoRoot "src\DdsFileTypePlus.sln"
    if (-not (Test-Path -LiteralPath $Solution)) {
        Fail "Solution not found: $Solution"
    }

    Write-Section "Restoring"

    $restoreArgs = @(
        $Solution,
        "/t:Restore",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        "/p:PaintNetInstallDir=$Pdn",
        "/verbosity:minimal",
        "/nologo"
    )

    & $MsBuild @restoreArgs
    if ($LASTEXITCODE -ne 0) {
        Fail "MSBuild restore failed with exit code $LASTEXITCODE."
    }

    Write-Section "Building Release / x64"

    $buildArgs = @(
        $Solution,
        "/m",
        "/t:Rebuild",
        "/p:Configuration=Release",
        "/p:Platform=x64",
        "/p:PaintNetInstallDir=$Pdn",
        "/verbosity:minimal",
        "/nologo"
    )

    & $MsBuild @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Fail "MSBuild failed with exit code $LASTEXITCODE."
    }

    # ------------------------------------------------------------
    # Find outputs and stage them locally.
    # ------------------------------------------------------------
    Write-Section "Finding Build Outputs"

    $managedCandidates = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "src") -Filter "DdsFileTypePlus.dll" -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\bin\\Release\\' } |
        Sort-Object LastWriteTime -Descending

    $nativeCandidates = Get-ChildItem -LiteralPath (Join-Path $RepoRoot "src") -Filter "DdsFileTypePlusIO_x64.dll" -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\Release\\' } |
        Sort-Object LastWriteTime -Descending

    $Managed = $managedCandidates | Select-Object -First 1
    $Native = $nativeCandidates | Select-Object -First 1

    if (-not $Managed) {
        Fail "Build reported success but DdsFileTypePlus.dll could not be found under src\bin\Release."
    }
    if (-not $Native) {
        Fail "Build reported success but DdsFileTypePlusIO_x64.dll could not be found under a Release output folder."
    }

    Write-Host "Managed:"
    Write-Host "  $($Managed.FullName)"
    Write-Host "Native:"
    Write-Host "  $($Native.FullName)"
    Write-Host ""

    if (Test-Path -LiteralPath $OutputDir) {
        Remove-Item -LiteralPath $OutputDir -Force -Recurse
    }
    New-Item -ItemType Directory -Path $OutputDir | Out-Null

    Copy-Item -LiteralPath $Managed.FullName -Destination (Join-Path $OutputDir "DdsFileTypePlus.dll") -Force
    Copy-Item -LiteralPath $Native.FullName -Destination (Join-Path $OutputDir "DdsFileTypePlusIO_x64.dll") -Force

    Write-Host "Staged output:"
    Write-Host "  $OutputDir"
    Write-Host ""

    # ------------------------------------------------------------
    # Install only when this SAME console was started as Administrator.
    # No second console is ever spawned.
    # ------------------------------------------------------------
    if ($isAdmin) {
        $FileTypes = Join-Path $Pdn "FileTypes"
        if (-not (Test-Path -LiteralPath $FileTypes)) {
            New-Item -ItemType Directory -Path $FileTypes | Out-Null
        }

        $running = Get-Process paintdotnet -ErrorAction SilentlyContinue
        if ($running) {
            Write-Host "Paint.NET is running, so automatic installation was skipped." -ForegroundColor Yellow
            Write-Host "Close Paint.NET and copy the two BUILD_OUTPUT DLLs into:" -ForegroundColor Yellow
            Write-Host "  $FileTypes"
        }
        else {
            $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $backup = Join-Path $FileTypes ("MGSV_DDS_Backup_" + $stamp)
            $existing = @(
                (Join-Path $FileTypes "DdsFileTypePlus.dll"),
                (Join-Path $FileTypes "DdsFileTypePlusIO_x64.dll")
            ) | Where-Object { Test-Path -LiteralPath $_ }

            if ($existing.Count -gt 0) {
                New-Item -ItemType Directory -Path $backup | Out-Null
                foreach ($old in $existing) {
                    Copy-Item -LiteralPath $old -Destination $backup -Force
                }
                Write-Host "Backed up existing external plugin to:"
                Write-Host "  $backup"
            }

            Copy-Item -LiteralPath (Join-Path $OutputDir "DdsFileTypePlus.dll") -Destination $FileTypes -Force
            Copy-Item -LiteralPath (Join-Path $OutputDir "DdsFileTypePlusIO_x64.dll") -Destination $FileTypes -Force

            Write-Section "SUCCESS - BUILT AND INSTALLED"
            Write-Host "Installed to:"
            Write-Host "  $FileTypes"
        }
    }
    else {
        Write-Section "SUCCESS - BUILD COMPLETE"
        Write-Host "The build succeeded."
        Write-Host ""
        Write-Host "The two DLLs are here:"
        Write-Host "  $OutputDir"
        Write-Host ""
        Write-Host "To install automatically, close Paint.NET and RIGHT-CLICK" -ForegroundColor Yellow
        Write-Host "BUILD_MGSV_VS2026.bat -> Run as administrator." -ForegroundColor Yellow
        Write-Host "Or manually copy both DLLs into:"
        Write-Host "  $(Join-Path $Pdn 'FileTypes')"
    }

    Write-Host ""
    Write-Host "For the Quiet 2048x2048 test, the DDS Inspector should report 10 mip levels."
}
catch {
    Write-Section "BUILD STOPPED"
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Full log:"
    Write-Host "  $LogPath" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Send me the FIRST real MSBuild/compiler/linker error from that log."
}
finally {
    try { Stop-Transcript | Out-Null } catch {}
    Write-Host ""
    Write-Host "This window will remain open." -ForegroundColor Green
    Write-Host "Press ENTER when you are finished reading/copying the result."
    [void](Read-Host)
}
