#!/usr/bin/env bash
# Checks that this machine has a Docker engine that runs Linux containers, which the API tests (Testcontainers) and
# the whole-system tests (Aspire) need. Fails with a message that says what to do, instead of failing inside a test.
# Podman works too when it exposes a Docker-compatible socket (DOCKER_HOST).
set -euo pipefail

if ! command -v docker >/dev/null 2>&1; then
  echo "::error::No docker command on this machine. Install Docker (or Podman with its Docker-compatible socket), or set ENTRYHUB_TEST_CONNECTION to a PostgreSQL of your own to run the API tests without containers."
  exit 1
fi

if ! os_type="$(docker info --format '{{.OSType}}' 2>/dev/null)"; then
  echo "::error::The docker command is there but no engine answers (is the daemon running, and can this user reach it?). Set ENTRYHUB_TEST_CONNECTION to run the API tests without containers."
  exit 1
fi

if [ "$os_type" != "linux" ]; then
  echo "::error::Docker runs $os_type containers here, and the tests need Linux containers (postgres:17-alpine)."
  exit 1
fi

docker info --format 'Docker {{.ServerVersion}}, {{.OSType}}/{{.Architecture}}'
