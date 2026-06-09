#!/usr/bin/env bash
set -euo pipefail

script_dir="$(dirname -- "${BASH_SOURCE[0]}")"
script_dir="$(cd -- "$script_dir"; pwd)"
repo_root="$(cd -- "$script_dir/.."; pwd)"

timestamp="$(date +%Y%m%d%H%M%S)"
version="0.1.0-dev.$timestamp"

project_path="$repo_root/src/PanguEngine/PanguEngine.csproj"
output_path="$repo_root/LocalNuGet"

mkdir -p "$output_path"

rm -f "$output_path"/PanguEngine.*.nupkg
rm -f "$output_path"/PanguEngine.*.snupkg

printf 'Packing PanguEngine %s...\n' "$version"
dotnet pack "$project_path" -c Debug -p:PackageVersion="$version" -o "$output_path" --nologo

printf '\nPacked PanguEngine %s to %s\n' "$version" "$output_path"
