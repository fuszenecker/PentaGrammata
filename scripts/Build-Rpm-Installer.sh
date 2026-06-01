#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Build and package PentaGrammata as an RPM installer.

Usage:
  ./scripts/Build-Rpm-Installer.sh [options]

Options:
  -v, --version <version>    Package version (defaults to root version.txt)
  -r, --runtime <rid>        .NET runtime identifier (default: linux-x64)
      --release <release>    RPM release number (default: 1)
      --skip-publish         Skip dotnet publish and use existing publish output
  -h, --help                 Show this help message

Examples:
  ./scripts/Build-Rpm-Installer.sh
  ./scripts/Build-Rpm-Installer.sh --runtime linux-arm64
  ./scripts/Build-Rpm-Installer.sh --version 1.2.0.0 --release 2
EOF
}

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"

PROJECT_FILE="${REPO_ROOT}/src/PentaGrammata.csproj"
VERSION_FILE="${REPO_ROOT}/version.txt"
ICON_SOURCE="${REPO_ROOT}/src/Assets/pentagrammata-icon.png"

RUNTIME="linux-x64"
SKIP_PUBLISH="false"
VERSION=""
RELEASE="1"

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
    --release)
      RELEASE="$2"
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

if [[ ! "${RELEASE}" =~ ^[0-9]+$ ]]; then
  echo "RPM release must be a positive integer. Got: ${RELEASE}" >&2
  exit 1
fi

case "${RUNTIME}" in
  linux-x64)
    RPM_ARCH="x86_64"
    ;;
  linux-arm64)
    RPM_ARCH="aarch64"
    ;;
  linux-arm)
    RPM_ARCH="armv7hl"
    ;;
  *)
    echo "Unsupported runtime '${RUNTIME}'. Supported: linux-x64, linux-arm64, linux-arm." >&2
    exit 1
    ;;
esac

if [[ ! -f "${ICON_SOURCE}" ]]; then
  echo "Application icon not found: ${ICON_SOURCE}" >&2
  exit 1
fi

PACKAGE_NAME="pentagrammata"
DISPLAY_NAME="PentaGrammata"
SUMMARY="Morse code practice desktop application"
HOMEPAGE="https://github.com/rfuszenecker/PentaGrammata"
LICENSE="MIT"

PUBLISH_DIR="${REPO_ROOT}/publish/${RUNTIME}"
OUTPUT_DIR="${REPO_ROOT}/installer/rpm"
BUILD_DIR="${OUTPUT_DIR}/build"
RPM_TOPDIR="${BUILD_DIR}/rpmbuild"
SOURCE_ROOT="${RPM_TOPDIR}/SOURCES/${PACKAGE_NAME}-${VERSION}"
SOURCE_ARCHIVE="${RPM_TOPDIR}/SOURCES/${PACKAGE_NAME}-${VERSION}.tar.gz"
SPEC_FILE="${RPM_TOPDIR}/SPECS/${PACKAGE_NAME}.spec"

echo "==> Version: ${VERSION}"
echo "==> Release: ${RELEASE}"
echo "==> Runtime: ${RUNTIME} (${RPM_ARCH})"

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

if ! command -v rpmbuild >/dev/null 2>&1; then
  echo "rpmbuild not found. Install 'rpm-build' and try again." >&2
  exit 1
fi

echo "==> Preparing RPM build layout"
rm -rf "${RPM_TOPDIR}"
mkdir -p \
  "${RPM_TOPDIR}/BUILD" \
  "${RPM_TOPDIR}/RPMS" \
  "${RPM_TOPDIR}/SOURCES" \
  "${RPM_TOPDIR}/SPECS" \
  "${RPM_TOPDIR}/SRPMS"

mkdir -p \
  "${SOURCE_ROOT}/opt/${PACKAGE_NAME}" \
  "${SOURCE_ROOT}/usr/bin" \
  "${SOURCE_ROOT}/usr/share/applications" \
  "${SOURCE_ROOT}/usr/share/icons/hicolor/256x256/apps"

cp -a "${PUBLISH_DIR}/." "${SOURCE_ROOT}/opt/${PACKAGE_NAME}/"
cp "${ICON_SOURCE}" "${SOURCE_ROOT}/usr/share/icons/hicolor/256x256/apps/${PACKAGE_NAME}.png"

cat > "${SOURCE_ROOT}/usr/bin/${PACKAGE_NAME}" <<EOF
#!/usr/bin/env bash
exec /opt/${PACKAGE_NAME}/PentaGrammata "\$@"
EOF

cat > "${SOURCE_ROOT}/usr/share/applications/${PACKAGE_NAME}.desktop" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=${DISPLAY_NAME}
Comment=${SUMMARY}
Exec=${PACKAGE_NAME}
Icon=${PACKAGE_NAME}
Terminal=false
Categories=Utility;HamRadio;
EOF

chmod 0755 "${SOURCE_ROOT}/usr/bin/${PACKAGE_NAME}"
chmod 0644 "${SOURCE_ROOT}/usr/share/applications/${PACKAGE_NAME}.desktop"

if [[ -f "${SOURCE_ROOT}/opt/${PACKAGE_NAME}/PentaGrammata" ]]; then
  chmod 0755 "${SOURCE_ROOT}/opt/${PACKAGE_NAME}/PentaGrammata"
fi

tar -czf "${SOURCE_ARCHIVE}" -C "${RPM_TOPDIR}/SOURCES" "${PACKAGE_NAME}-${VERSION}"

cat > "${SPEC_FILE}" <<EOF
%global debug_package %{nil}
%global __requires_exclude liblttng-ust\\.so\\.0
Name:           ${PACKAGE_NAME}
Version:        ${VERSION}
Release:        ${RELEASE}%{?dist}
Summary:        ${SUMMARY}
License:        ${LICENSE}
URL:            ${HOMEPAGE}
Source0:        %{name}-%{version}.tar.gz
BuildArch:      ${RPM_ARCH}
Requires:       glibc libX11 libICE libSM libXext libXrender fontconfig

%description
${SUMMARY}

%prep
%setup -q

%build

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}
cp -a opt %{buildroot}/
cp -a usr %{buildroot}/

%files
/opt/${PACKAGE_NAME}
/usr/bin/${PACKAGE_NAME}
/usr/share/applications/${PACKAGE_NAME}.desktop
/usr/share/icons/hicolor/256x256/apps/${PACKAGE_NAME}.png

%changelog
* $(LC_ALL=C date "+%a %b %d %Y") ${DISPLAY_NAME} Packaging <noreply@localhost> - ${VERSION}-${RELEASE}
- Automated RPM build
EOF

echo "==> Building RPM package"
rpmbuild --define "_topdir ${RPM_TOPDIR}" --define "_rpmdir ${OUTPUT_DIR}" -bb "${SPEC_FILE}"

RPM_FILE="$(find "${OUTPUT_DIR}/${RPM_ARCH}" -maxdepth 1 -type f -name "${PACKAGE_NAME}-${VERSION}-${RELEASE}*.rpm" | head -n 1)"

if [[ -z "${RPM_FILE}" ]]; then
  echo "RPM build completed, but output file could not be located." >&2
  exit 1
fi

echo "==> RPM package created"
echo "    ${RPM_FILE}"
