#!/usr/bin/env bash
# Prints the release notes of a version from RELEASE_NOTES.md: the section of the version without its BUILD number
# ("## 2.0.0" for 2.0.0.3), or of the whole version if it has one of its own. Fails if there is none, so that no
# release goes out without notes.
#
# Usage: scripts/Release-Notes.sh VERSION

set -euo pipefail

VERSION="${1:?Usage: Release-Notes.sh VERSION}"
VERSION="${VERSION#v}"
NOTES="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)/RELEASE_NOTES.md"

section() {
  # The lines after "## $1" up to the next "## ", without the blank lines around them
  awk -v heading="## $1" '
    $0 == heading { found = 1; next }
    found && /^## / { exit }
    found { print }
  ' "${NOTES}" | sed -e '/./,$!d' | tac | sed -e '/./,$!d' | tac
}

for key in "${VERSION}" "$(echo "${VERSION}" | cut -d. -f1-3)"; do
  text="$(section "${key}")"
  if [[ -n "${text}" ]]; then
    printf '%s\n' "${text}"
    exit 0
  fi
done

echo "RELEASE_NOTES.md has no section for ${VERSION} (\"## $(echo "${VERSION}" | cut -d. -f1-3)\")." >&2
exit 1
