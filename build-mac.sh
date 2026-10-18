#!/usr/bin/env bash
# Buduje SC2CCM.app dla macOS na Apple Silicon (arm64):  ./build-mac.sh
#
# Wymaga .NET 8 SDK:  brew install --cask dotnet-sdk   (albo https://dotnet.microsoft.com/download)
set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="SC2CCM"
BUNDLE_ID="pl.darken.sc2ccm"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/SC2CCM.Mac/SC2CCM.Mac.csproj | head -1)"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Nie znaleziono 'dotnet'. Zainstaluj .NET 8 SDK: brew install --cask dotnet-sdk" >&2
  exit 1
fi

ARCHES=(arm64)

rm -rf build dist
mkdir -p dist

for ARCH in "${ARCHES[@]}"; do
  RID="osx-$ARCH"
  echo "==> Publikuję $RID"
  dotnet publish src/SC2CCM.Mac/SC2CCM.Mac.csproj \
    -c Release -r "$RID" --self-contained true \
    -p:UseAppHost=true -p:DebugType=None \
    -o "build/$RID"

  if [ "${#ARCHES[@]}" -gt 1 ]; then APP="dist/$APP_NAME-$ARCH.app"; else APP="dist/$APP_NAME.app"; fi
  echo "==> Składam $APP"
  mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
  cp -R "build/$RID/." "$APP/Contents/MacOS/"
  cp src/SC2CCM.Mac/Assets/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"

  cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>                 <string>$APP_NAME</string>
  <key>CFBundleDisplayName</key>          <string>SC2 Custom Campaign Manager</string>
  <key>CFBundleIdentifier</key>           <string>$BUNDLE_ID</string>
  <key>CFBundleVersion</key>              <string>$VERSION</string>
  <key>CFBundleShortVersionString</key>   <string>$VERSION</string>
  <key>CFBundleExecutable</key>           <string>$APP_NAME</string>
  <key>CFBundleIconFile</key>             <string>AppIcon</string>
  <key>CFBundlePackageType</key>          <string>APPL</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>LSMinimumSystemVersion</key>       <string>12.0</string>
  <key>LSApplicationCategoryType</key>    <string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key>      <true/>
  <key>NSHumanReadableCopyright</key>     <string>Based on SC2CCM, Copyright (c) 2022 by 7thAce and GiantGrantGames. MIT License.</string>
</dict>
</plist>
PLIST

  chmod +x "$APP/Contents/MacOS/$APP_NAME"

  # Podpis ad-hoc – bez niego Apple Silicon w ogóle nie uruchomi binarki.
  if command -v codesign >/dev/null 2>&1; then
    echo "==> Podpisuję ad-hoc"
    codesign --force --deep --sign - "$APP"
  fi

  ZIP="dist/$(basename "$APP" .app)-$VERSION-macOS-$ARCH.zip"
  if command -v ditto >/dev/null 2>&1; then
    ditto -c -k --sequesterRsrc --keepParent "$APP" "$ZIP"
  else
    (cd dist && zip -qry "$(basename "$ZIP")" "$(basename "$APP")")
  fi
  echo "==> Gotowe: $APP  ($ZIP)"
done

cat <<'EOF'

Pierwsze uruchomienie: aplikacja nie jest notaryzowana przez Apple, więc macOS ją zablokuje.
Kliknij prawym przyciskiem na SC2CCM.app → Otwórz → Otwórz,
albo w Terminalu:  xattr -dr com.apple.quarantine /Applications/SC2CCM.app
EOF
