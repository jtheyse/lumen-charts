#!/usr/bin/env bash
# Downloads the Lumen.Charts packages from a GitHub release into ./local-packages and registers that
# folder as a NuGet source, because the packages are published as release assets rather than on nuget.org.
# Usage: install.sh [tag]   e.g. install.sh v0.24.0 — the latest release when no tag is given.
set -euo pipefail

repo="jtheyse/lumen-charts"
dir="local-packages"
tag="${1:-}"

if [ -z "$tag" ]; then
  if command -v gh >/dev/null 2>&1; then
    tag=$(gh release view --repo "$repo" --json tagName -q .tagName)
  else
    tag=$(curl -fsSL "https://api.github.com/repos/$repo/releases/latest" | sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p' | head -n 1)
  fi
fi
[ -n "$tag" ] || { echo "Could not find the latest release of $repo; pass a tag such as v0.24.0." >&2; exit 1; }
version="${tag#v}"

if grep -qsE 'Sdk="Microsoft\.NET\.Sdk\.(Web|BlazorWebAssembly|Razor)"' ./*.csproj 2>/dev/null; then
  echo "Note: this folder holds a web project. The Web SDK copies a nuget.config found here into the build output;" >&2
  echo "      running this script from the repository or solution root instead keeps it out." >&2
fi

mkdir -p "$dir"
for package in Lumen.Charts Lumen.Charts.Blazor Lumen.Charts.AspNetCore; do
  curl -fsSL -o "$dir/$package.$version.nupkg" "https://github.com/$repo/releases/download/$tag/$package.$version.nupkg"
done

if [ -f nuget.config ] || [ -f NuGet.Config ] || [ -f NuGet.config ]; then
  echo "A nuget.config already exists, so it was left alone. Add this line inside its <packageSources>:"
  echo "    <add key=\"lumen-local\" value=\"$dir\" />"
else
  cat > nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="lumen-local" value="$dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
fi

echo "Lumen.Charts $version is in ./$dir."
echo "Next: dotnet add package Lumen.Charts.Blazor --version $version   (or Lumen.Charts, Lumen.Charts.AspNetCore)"
