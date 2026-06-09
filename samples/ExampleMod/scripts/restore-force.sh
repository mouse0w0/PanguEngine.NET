#!/usr/bin/env bash
set -euo pipefail

script_dir="$(dirname -- "${BASH_SOURCE[0]}")"
script_dir="$(cd -- "$script_dir"; pwd)"
mod_root="$(cd -- "$script_dir/.."; pwd)"
project_path="$mod_root/ExampleMod.csproj"

printf 'Restoring ExampleMod packages with --force...\n'
dotnet restore "$project_path" --force --nologo

printf 'Restored ExampleMod packages.\n'
