#!/usr/bin/env bash
set -euo pipefail

script_dir="$(dirname -- "${BASH_SOURCE[0]}")"
script_dir="$(cd -- "$script_dir"; pwd)"
mod_root="$(cd -- "$script_dir/.."; pwd)"
repo_root="$(cd -- "$mod_root/../.."; pwd)"

native_path() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
    return
  fi

  if command -v wslpath >/dev/null 2>&1; then
    wslpath -w "$1"
    return
  fi

  printf '%s' "$1"
}

if [ "${1:-}" = "" ]; then
  app_output_path="$repo_root/src/PanguEngine.App/bin/Debug/net10.0"
  app_path="$app_output_path/PanguEngine.App.exe"
else
  app_path="$1"
  app_output_path="$(dirname -- "$app_path")"
fi

mod_output_path="$mod_root/bin/Debug/net10.0"
properties_path="$mod_root/Properties"
launch_settings_path="$properties_path/launchSettings.json"

app_path="$(native_path "$app_path")"
app_output_path="$(native_path "$app_output_path")"
mod_output_path="$(native_path "$mod_output_path")"

json_escape() {
  local value=$1
  value=${value//\\/\\\\}
  value=${value//\"/\\\"}
  value=${value//$'\r'/\\r}
  value=${value//$'\n'/\\n}
  printf '%s' "$value"
}

app_path_json="$(json_escape "$app_path")"
app_output_path_json="$(json_escape "$app_output_path")"
mod_output_path_json="$(json_escape "$mod_output_path")"

mkdir -p "$properties_path"

cat > "$launch_settings_path" <<EOF
{
  "profiles": {
    "PanguEngine.App": {
      "commandName": "Executable",
      "executablePath": "$app_path_json",
      "commandLineArgs": "--mod \"$mod_output_path_json\"",
      "workingDirectory": "$app_output_path_json"
    }
  }
}
EOF

printf 'Wrote %s\n' "$launch_settings_path"
