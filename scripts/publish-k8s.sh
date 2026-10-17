#!/usr/bin/env bash
# Regenerates deploy/k8s, the Helm chart that `aspire publish` produces from the AppHost.
# The Aspire CLI comes with the AppHost SDK (AspireUseCliBundle), so nothing needs installing.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet build "$root/src/EntryHub.AppHost" -c Release --nologo -v quiet

aspire="$(find "${NUGET_PACKAGES:-$HOME/.nuget/packages}/aspire.cli.linux-x64" -type f -name aspire -path '*linux-x64/aspire' | sort | tail -n 1)"

rm -rf "$root/deploy/k8s"
(cd "$root/src/EntryHub.AppHost" && "$aspire" publish --non-interactive -o "$root/deploy/k8s")
