#!/usr/bin/env bash
#
# Build PentaGrammata .deb and/or .rpm packages inside Podman containers,
# so the host needs only `podman` — no .NET SDK, dpkg-deb, rpmbuild, or WSL.
#
# This is a thin wrapper: it builds (once, per distro) the relevant builder
# image and then runs the existing scripts/Build-{Deb,Rpm}-Installer.sh inside
# that container with the repo bind-mounted at /src. Output artifacts land in
# installer/deb and installer/rpm, owned by the host user (via --userns=keep-id).
#
# Each package is built on a distro-faithful base:
#   deb -> Ubuntu LTS  (container/Containerfile.deb)
#   rpm -> Rocky Linux 10 (container/Containerfile.rpm)

set -euo pipefail

usage() {
  cat <<'EOF'
Build PentaGrammata .deb/.rpm packages in Podman containers.

Usage:
  ./scripts/Build-Installer.Podman.sh --format <deb|rpm|all> [options]

Required:
  -f, --format <fmt>    Package format: deb, rpm, or all (both)

Options (passed through to the underlying build script):
  -v, --version <ver>    Package version (defaults to root version.txt)
  -r, --runtime <rid>     .NET runtime identifier (default: linux-x64)
      --release <n>       RPM release number (default: 1, RPM only)
      --skip-publish      Reuse existing publish/<rid> output

Image:
      --build             Force a rebuild of the builder image(s)
      --no-build          Do not build even if an image is missing
  -h, --help              Show this help message

Examples:
  ./scripts/Build-Installer.Podman.sh --format deb
  ./scripts/Build-Installer.Podman.sh --format rpm --runtime linux-arm64
  ./scripts/Build-Installer.Podman.sh --format all --version 1.2.0.0
EOF
}

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
CONTAINER_DIR="${REPO_ROOT}/container"

IMG_DEB="localhost/pentagrammata-builder-deb:latest"
IMG_RPM="localhost/pentagrammata-builder-rpm:latest"
CF_DEB="${CONTAINER_DIR}/Containerfile.deb"
CF_RPM="${CONTAINER_DIR}/Containerfile.rpm"

FORMAT=""
FORCE_BUILD="false"
SKIP_BUILD="false"
PASSTHROUGH=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    -f|--format)
      FORMAT="$2"
      shift 2
      ;;
    --build)
      FORCE_BUILD="true"
      shift
      ;;
    --no-build)
      SKIP_BUILD="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      PASSTHROUGH+=("$@")
      break
      ;;
    *)
      PASSTHROUGH+=("$1")
      shift
      ;;
  esac
done

if [[ -z "${FORMAT}" ]]; then
  echo "Error: --format is required (deb, rpm, or all)." >&2
  usage
  exit 1
fi

case "${FORMAT}" in
  deb|rpm|all) ;;
  *)
    echo "Error: unknown format '${FORMAT}'. Use deb, rpm, or all." >&2
    exit 1
    ;;
esac

if ! command -v podman >/dev/null 2>&1; then
  echo "Error: podman not found on PATH. Install Podman (or Podman Desktop) and try again." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Per-format image + containerfile lookup.
# ---------------------------------------------------------------------------
image_for() {
  case "$1" in
    deb) echo "${IMG_DEB}" ;;
    rpm) echo "${IMG_RPM}" ;;
  esac
}
containerfile_for() {
  case "$1" in
    deb) echo "${CF_DEB}" ;;
    rpm) echo "${CF_RPM}" ;;
  esac
}

# Build the distro builder image for a format if missing (or when forced).
ensure_image() {
  local fmt="$1"
  local img cf
  img="$(image_for "${fmt}")"
  cf="$(containerfile_for "${fmt}")"

  if [[ "${FORCE_BUILD}" == "true" ]]; then
    echo "==> Building ${fmt} builder image (forced): ${img}"
    podman build -t "${img}" -f "${cf}" "${CONTAINER_DIR}"
  elif ! podman image inspect "${img}" >/dev/null 2>&1; then
    if [[ "${SKIP_BUILD}" == "true" ]]; then
      echo "Error: image ${img} not found and --no-build was given." >&2
      exit 1
    fi
    echo "==> ${fmt} builder image not found; building: ${img}"
    podman build -t "${img}" -f "${cf}" "${CONTAINER_DIR}"
  else
    echo "==> Using existing ${fmt} builder image: ${img}"
  fi
}

# ---------------------------------------------------------------------------
# Run the underlying build script inside the container.
#   - --userns=keep-id: container runs as the host UID, so artifacts written to
#     the bind-mounted repo are owned by the host user (not root).
#   - :Z on volume mounts: SELinux relabel (no-op on non-SELinux hosts, required
#     on Fedora/Rocky). Also applied to the NuGet volume for the same reason.
#   - HOME=/tmp: the keep-id UID has no /etc/passwd entry in the image; give
#     dotnet a writable home.
#   - NUGET_PACKAGES + named volume: persistent package cache across runs.
# ---------------------------------------------------------------------------
TTY_OPTS=()
if [[ -t 0 ]] && [[ -t 1 ]]; then
  TTY_OPTS=(-it)
fi

run_in_container() {
  local fmt="$1"
  local inner_script="$2"
  shift 2
  local img
  img="$(image_for "${fmt}")"
  podman run --rm \
    "${TTY_OPTS[@]}" \
    --userns=keep-id \
    -v "${REPO_ROOT}:/src:Z" \
    -v "pentagrammata-nuget:/tmp/nuget:Z" \
    -w /src \
    -e HOME=/tmp \
    -e NUGET_PACKAGES=/tmp/nuget \
    "${img}" \
    "./scripts/${inner_script}" "$@"
}

case "${FORMAT}" in
  deb)
    ensure_image "deb"
    echo "==> Building .deb (Ubuntu LTS)"
    run_in_container "deb" "Build-Deb-Installer.sh" "${PASSTHROUGH[@]}"
    ;;
  rpm)
    ensure_image "rpm"
    echo "==> Building .rpm (Rocky Linux 10)"
    run_in_container "rpm" "Build-Rpm-Installer.sh" "${PASSTHROUGH[@]}"
    ;;
  all)
    echo "==> Building .deb then .rpm (sharing one publish output)"
    ensure_image "deb"
    ensure_image "rpm"

    # If the caller did not pass --skip-publish, the deb run publishes; reuse
    # that publish/<rid> tree for the rpm run to avoid publishing twice. The
    # self-contained publish output is distro-portable (glibc baseline), so a
    # tree built under Ubuntu LTS is consumable by the Rocky rpm build.
    skip_present="false"
    for arg in "${PASSTHROUGH[@]}"; do
      [[ "${arg}" == "--skip-publish" ]] && skip_present="true"
    done

    echo "--- .deb (Ubuntu LTS) ---"
    run_in_container "deb" "Build-Deb-Installer.sh" "${PASSTHROUGH[@]}"

    echo "--- .rpm (Rocky Linux 10) ---"
    if [[ "${skip_present}" == "true" ]]; then
      run_in_container "rpm" "Build-Rpm-Installer.sh" "${PASSTHROUGH[@]}"
    else
      run_in_container "rpm" "Build-Rpm-Installer.sh" "${PASSTHROUGH[@]}" --skip-publish
    fi
    ;;
esac

echo "==> Done."
