# Publishes Darkroom as a self-contained, single-file x64 executable.
# Usage:  .\publish.ps1          -> Darkroom.exe (and Versions\Archive\Darkroom-<version>.exe)
#         .\publish.ps1 -Test    -> builds and self-checks into dist\ only; nothing is archived
#         .\publish.ps1 -Run     -> publish and launch
# Refuses a version that's already in Versions\Archive: raise <Version> in the csproj and VERSION in
# wwwroot\index.html first. The build must pass its own self-check (--smoke) before it is kept.
# If Darkroom is running from this folder, it is closed first (it asks about unsaved work) and started again.
param([switch]$Test, [switch]$Run)

$ErrorActionPreference = "Stop"
$name = "Darkroom"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "src\$name\$name.csproj"
$out  = Join-Path $root "dist"
$top  = Join-Path $root "$name.exe"

$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$archive = Join-Path $root "Versions\Archive\$name-$version.exe"
if (-not $Test -and (Test-Path $archive)) { throw "Version $version is already archived. Raise <Version> in the csproj, then publish again." }
$web = Get-Content (Join-Path $root "src\$name\wwwroot\index.html") -Raw
if ($web -notmatch "const VERSION = `"$([regex]::Escape($version))`";") { throw "wwwroot\index.html does not say VERSION = `"$version`". Keep it in step with the csproj." }

# On some PCs the `dotnet` on PATH is the x86 one without an SDK.
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

Write-Host "Publishing $name $version..." -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
& $dotnet publish $proj -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
$exe = Join-Path $out "$name.exe"

Write-Host "Self-check..." -ForegroundColor Cyan
Start-Process $exe "--smoke" -Wait
$report = Get-Content (Join-Path $out "DarkroomData\smoke.txt") -Raw
if ($report -notmatch "^PASS") { throw "self-check failed:`n$report" }
Write-Host $report

# Only the exe is needed; drop side files and the self-check's data.
Get-ChildItem $out | Where-Object { $_.Name -ne "$name.exe" } | Remove-Item -Recurse -Force
if ($Test) { Write-Host "Test build: $exe (not archived)" -ForegroundColor Green; return }

# Ask a running copy to close (it asks about unsaved work itself); never kill it.
$running = @(Get-Process $name -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $top })
foreach ($p in $running) {
    [void]$p.CloseMainWindow()
    if (-not $p.WaitForExit(15000)) { throw "Darkroom is still open (unsaved work?). Close it, then publish again." }
}

# Every build is kept: the current one at the top level, and a copy per version in Versions\Archive.
New-Item -ItemType Directory -Force (Split-Path $archive) | Out-Null
Copy-Item $exe $archive
Copy-Item $exe $top
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Done: $top ($size MB), archived as $archive" -ForegroundColor Green

# Started through Windows so it outlives this script.
if ($Run -or $running) { [void](Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "`"$top`"" }) }
