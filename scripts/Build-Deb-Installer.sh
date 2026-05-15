#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Build and package PentaGrammata as a .deb installer.

Usage:
  ./scripts/Build-Deb-Installer.sh [options]

Options:
  -v, --version <version>    Package version (defaults to root version.txt)
  -r, --runtime <rid>        .NET runtime identifier (default: linux-x64)
      --skip-publish         Skip dotnet publish and use existing publish output
  -h, --help                 Show this help message

Examples:
  ./scripts/Build-Deb-Installer.sh
  ./scripts/Build-Deb-Installer.sh --runtime linux-arm64
  ./scripts/Build-Deb-Installer.sh --version 1.2.0.0 --skip-publish
EOF
}

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

PROJECT_FILE="${REPO_ROOT}/src/PentaGrammata.csproj"
VERSION_FILE="${REPO_ROOT}/version.txt"
RUNTIME="linux-x64"
SKIP_PUBLISH="false"
VERSION=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -v|--version)
      VERSION="$2"
      shift 2
      ;;
    -r|--runtime)
      RUNTIME="$2"
      shift 2
      ;;
    --skip-publish)
      SKIP_PUBLISH="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "${VERSION}" ]]; then
  if [[ ! -f "${VERSION_FILE}" ]]; then
    echo "version.txt not found at ${VERSION_FILE}. Pass --version or create version.txt." >&2
    exit 1
  fi
  VERSION="$(tr -d '\r\n' < "${VERSION_FILE}")"
fi

if [[ -z "${VERSION}" ]]; then
  echo "Version is empty. Update version.txt or pass --version." >&2
  exit 1
fi

case "${RUNTIME}" in
  linux-x64) DEB_ARCH="amd64" ;;
  linux-arm64) DEB_ARCH="arm64" ;;
  linux-arm) DEB_ARCH="armhf" ;;
  *)
    echo "Unsupported runtime '${RUNTIME}'. Supported: linux-x64, linux-arm64, linux-arm." >&2
    exit 1
    ;;
esac

PACKAGE_NAME="pentagrammata"
DISPLAY_NAME="PentaGrammata"
MAINTAINER="PentaGrammata"
HOMEPAGE="https://github.com/rfuszenecker/PentaGrammata"
DESCRIPTION="Morse code practice desktop application"

PUBLISH_DIR="${REPO_ROOT}/publish/${RUNTIME}"
OUTPUT_DIR="${REPO_ROOT}/installer/deb"
BUILD_DIR="${OUTPUT_DIR}/build"
PKG_DIR="${BUILD_DIR}/${PACKAGE_NAME}_${VERSION}_${DEB_ARCH}"
DEBIAN_DIR="${PKG_DIR}/DEBIAN"
APP_DIR="${PKG_DIR}/opt/${PACKAGE_NAME}"
BIN_DIR="${PKG_DIR}/usr/bin"
DESKTOP_DIR="${PKG_DIR}/usr/share/applications"
ICON_DIR="${PKG_DIR}/usr/share/icons/hicolor/256x256/apps"
ICON_SOURCE="${REPO_ROOT}/src/Assets/pentagrammata-icon.png"
OUTPUT_DEB="${OUTPUT_DIR}/${PACKAGE_NAME}_${VERSION}_${DEB_ARCH}.deb"

echo "==> Version: ${VERSION}"
echo "==> Runtime: ${RUNTIME} (${DEB_ARCH})"

if [[ "${SKIP_PUBLISH}" != "true" ]]; then
  echo "==> Publishing ${RUNTIME}"
  dotnet publish "${PROJECT_FILE}" \
    -c Release \
    -r "${RUNTIME}" \
    --self-contained true \
    -p:Version="${VERSION}" \
    -p:AssemblyVersion="${VERSION}" \
    -p:FileVersion="${VERSION}" \
    -o "${PUBLISH_DIR}"
else
  echo "==> Skipping publish"
fi

if [[ ! -d "${PUBLISH_DIR}" ]]; then
  echo "Publish directory does not exist: ${PUBLISH_DIR}" >&2
  exit 1
fi

if ! command -v dpkg-deb >/dev/null 2>&1; then
  echo "dpkg-deb not found. Install 'dpkg-dev' and try again." >&2
  exit 1
fi

echo "==> Preparing package structure"
rm -rf "${PKG_DIR}"
mkdir -p "${DEBIAN_DIR}" "${APP_DIR}" "${BIN_DIR}" "${DESKTOP_DIR}" "${ICON_DIR}"

cp -a "${PUBLISH_DIR}/." "${APP_DIR}/"

cat > "${BIN_DIR}/${PACKAGE_NAME}" <<EOF
#!/usr/bin/env bash
exec /opt/${PACKAGE_NAME}/PentaGrammata "\$@"
EOF
chmod 0755 "${BIN_DIR}/${PACKAGE_NAME}"

if [[ -f "${ICON_SOURCE}" ]]; then
  cp "${ICON_SOURCE}" "${ICON_DIR}/${PACKAGE_NAME}.png"
fi

cat > "${DESKTOP_DIR}/${PACKAGE_NAME}.desktop" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=${DISPLAY_NAME}
Comment=${DESCRIPTION}
Exec=${PACKAGE_NAME}
Icon=${PACKAGE_NAME}
Terminal=false
Categories=Education;Utility;
EOF

chmod 0644 "${DESKTOP_DIR}/${PACKAGE_NAME}.desktop"

if [[ -f "${APP_DIR}/PentaGrammata" ]]; then
  chmod 0755 "${APP_DIR}/PentaGrammata"
fi

INSTALLED_SIZE="$(du -sk "${PKG_DIR}" | cut -f1)"

cat > "${DEBIAN_DIR}/control" <<EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${DEB_ARCH}
Maintainer: ${MAINTAINER}
Depends: libc6
Homepage: ${HOMEPAGE}
Installed-Size: ${INSTALLED_SIZE}
Description: ${DESCRIPTION}
EOF

echo "==> Building Debian package"
dpkg-deb --build --root-owner-group "${PKG_DIR}" "${OUTPUT_DEB}"

echo "==> Debian package created"
echo "    ${OUTPUT_DEB}"
