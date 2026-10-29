#!/bin/sh
set -eu

# Fail closed when the installer has not provided a backend origin. Never echo it.
if ! printf '%s' "${INTOCHATOS_KERNEL_UPSTREAM:-}" | grep -Eq '^https?://[A-Za-z0-9.-]+(:[0-9]+)?$'; then
    echo 'INTOCHATOS_KERNEL_UPSTREAM must be an absolute http(s) backend origin.' >&2
    exit 1
fi
