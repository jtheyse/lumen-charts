# Downloads the Lumen.Charts packages from a GitHub release into ./local-packages and registers that
# folder as a NuGet source, because the packages are published as release assets rather than on nuget.org.
# Usage: install.ps1 [-Tag v0.25.0]   — the latest release when no tag is given.
param([string]$Tag = "")
$ErrorActionPreference = "Stop"

$repo = "jtheyse/lumen-charts"
$dir = "local-packages"

if (-not $Tag) {
    $Tag = (Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest").tag_name
}
if (-not $Tag) { throw "Could not find the latest release of $repo; pass -Tag v0.25.0." }
$version = $Tag.TrimStart("v")

if (Get-ChildItem -Filter *.csproj -ErrorAction SilentlyContinue | Select-String -Pattern 'Sdk="Microsoft\.NET\.Sdk\.(Web|BlazorWebAssembly|Razor)"' -Quiet) {
    Write-Warning "This folder holds a web project. The Web SDK copies a nuget.config found here into the build output; running this script from the repository or solution root instead keeps it out."
}

New-Item -ItemType Directory -Force $dir | Out-Null
foreach ($package in "Lumen.Charts", "Lumen.Charts.Blazor", "Lumen.Charts.AspNetCore") {
    Invoke-WebRequest "https://github.com/$repo/releases/download/$Tag/$package.$version.nupkg" -OutFile "$dir/$package.$version.nupkg"
}

if (Get-ChildItem -Name -Filter "nuget.config" -ErrorAction SilentlyContinue) {
    Write-Host "A nuget.config already exists, so it was left alone. Add this line inside its <packageSources>:"
    Write-Host "    <add key=`"lumen-local`" value=`"$dir`" />"
} else {
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="lumen-local" value="$dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -Encoding utf8 nuget.config
}

Write-Host "Lumen.Charts $version is in ./$dir."
Write-Host "Next: dotnet add package Lumen.Charts.Blazor --version $version   (or Lumen.Charts, Lumen.Charts.AspNetCore)"
