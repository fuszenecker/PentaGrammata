#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Build and package PentaGrammata as a macOS .dmg disk image.

Must run on macOS (uses sips, iconutil, codesign and hdiutil).

Usage:
  ./scripts/Build-Dmg-Installer.sh [options]

Options:
  -v, --version <version>    Bundle version (defaults to root version.txt)
  -r, --runtime <rid>        .NET runtime identifier (default: osx-arm64)
      --skip-publish         Skip dotnet publish and use existing publish output
  -h, --help                 Show this help message

Examples:
  ./scripts/Build-Dmg-Installer.sh
  ./scripts/Build-Dmg-Installer.sh --runtime osx-x64
  ./scripts/Build-Dmg-Installer.sh --version 1.2.0.0 --skip-publish
EOF
}

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

PROJECT_FILE="${REPO_ROOT}/src/PentaGrammata.csproj"
VERSION_FILE="${REPO_ROOT}/version.txt"
RUNTIME="osx-arm64"
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

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must run on macOS." >&2
  exit 1
fi

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
  osx-arm64|osx-x64) ;;
  *)
    echo "Unsupported runtime '${RUNTIME}'. Supported: osx-arm64, osx-x64." >&2
    exit 1
    ;;
esac

APP_NAME="PentaGrammata"
BUNDLE_ID="eu.fuszenecker.pentagrammata"
SHORT_VERSION="$(echo "${VERSION}" | cut -d. -f1-3)"

PUBLISH_DIR="${REPO_ROOT}/publish/${RUNTIME}"
OUTPUT_DIR="${REPO_ROOT}/installer/dmg"
BUILD_DIR="${OUTPUT_DIR}/build"
STAGE_DIR="${BUILD_DIR}/stage"
APP_BUNDLE="${STAGE_DIR}/${APP_NAME}.app"
CONTENTS_DIR="${APP_BUNDLE}/Contents"
MACOS_DIR="${CONTENTS_DIR}/MacOS"
RESOURCES_DIR="${CONTENTS_DIR}/Resources"
ICON_SOURCE="${REPO_ROOT}/src/Assets/pentagrammata-icon.png"
ICONSET_DIR="${BUILD_DIR}/AppIcon.iconset"
OUTPUT_DMG="${OUTPUT_DIR}/${APP_NAME}-${VERSION}-${RUNTIME}.dmg"

echo "==> Version: ${VERSION}"
echo "==> Runtime: ${RUNTIME}"

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

echo "==> Preparing ${APP_NAME}.app bundle"
rm -rf "${BUILD_DIR}"
mkdir -p "${MACOS_DIR}" "${RESOURCES_DIR}" "${OUTPUT_DIR}"
cp -a "${PUBLISH_DIR}/." "${MACOS_DIR}/"

cat > "${CONTENTS_DIR}/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>${APP_NAME}</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>${BUNDLE_ID}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>${APP_NAME}</string>
  <key>CFBundleDisplayName</key>
  <string>${APP_NAME}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${SHORT_VERSION}</string>
  <key>CFBundleVersion</key>
  <string>${VERSION}</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>NSPrincipalClass</key>
  <string>NSApplication</string>
</dict>
</plist>
EOF

mkdir -p "${ICONSET_DIR}"
sips -z 16 16     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_16x16.png"      >/dev/null
sips -z 32 32     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_16x16@2x.png"   >/dev/null
sips -z 32 32     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_32x32.png"      >/dev/null
sips -z 64 64     "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_32x32@2x.png"   >/dev/null
sips -z 128 128   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_128x128.png"    >/dev/null
sips -z 256 256   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_128x128@2x.png" >/dev/null
sips -z 256 256   "${ICON_SOURCE}" --out "${ICONSET_DIR}/icon_256x256.png"    >/dev/null
iconutil -c icns "${ICONSET_DIR}" -o "${RESOURCES_DIR}/AppIcon.icns"

chmod 0755 "${MACOS_DIR}/${APP_NAME}"

echo "==> Ad-hoc signing bundle"
codesign --force --deep --sign - "${APP_BUNDLE}"

ln -s /Applications "${STAGE_DIR}/Applications"

echo "==> Creating disk image"
rm -f "${OUTPUT_DMG}"
hdiutil create \
  -volname "${APP_NAME} ${VERSION}" \
  -srcfolder "${STAGE_DIR}" \
  -ov \
  -format UDZO \
  "${OUTPUT_DMG}"

echo "==> Disk image created"
echo "    ${OUTPUT_DMG}"
