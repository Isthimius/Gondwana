#!/usr/bin/env bash
# Shared, checksum-pinned Linux x64 installation for every API publisher.
set -euo pipefail

if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  echo 'This installer requires Linux x86_64.' >&2
  exit 1
fi

version=1.14.0
# Official Release_1_14_0 Linux archive; re-verify this digest when upgrading.
sha256=e5d6ae24d0bf3f0cdc4d8f146726b89ca323922f19441af99b1872d503665ad6
install_root=$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/gondwana-doxygen.XXXXXX")
archive="$install_root/doxygen.tar.gz"
curl --fail --location --silent --show-error --retry 3 --connect-timeout 30 --max-time 300 \
  "https://github.com/doxygen/doxygen/releases/download/Release_1_14_0/doxygen-${version}.linux.bin.tar.gz" \
  --output "$archive"
echo "$sha256  $archive" | sha256sum --check --strict
tar --extract --gzip --no-same-owner --file "$archive" --directory "$install_root" \
  "doxygen-${version}/bin/doxygen"
bin="$install_root/doxygen-${version}/bin"
actual=$("$bin/doxygen" --version)
if [[ "$actual" != "$version" && "$actual" != "$version ("* ]]; then
  echo "Expected Doxygen $version; got $actual" >&2
  exit 1
fi
echo "Verified Doxygen $actual"
"$bin/doxygen" -g "$install_root/smoke.Doxyfile"
if [[ -n "${GITHUB_PATH:-}" ]]; then
  echo "$bin" >> "$GITHUB_PATH"
fi
echo "For local builds: export PATH=\"$bin:\$PATH\""
