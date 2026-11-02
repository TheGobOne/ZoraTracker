#!/bin/zsh
# Builds the Mac app: Z1R_Avalonia_Desktop/bin/macOS/<arch>/ZORA Tracker.app, self-contained (no .NET install needed), one per
# processor: arm64 (Apple silicon) and x64 (Intel). Unsigned apart from the ad-hoc signature every Mac app needs to run on Apple silicon,
# so a copy downloaded from the internet must be opened with right-click > Open the first time.
#   Usage: Z1R_Avalonia_Desktop/macOS/make-app.sh [arm64|x64 ...]   (default: both)
set -e
here=${0:A:h}
project=${here:h}
repo=${project:h}
# the tracker's version, as shown in the app (GlobalAssemblyInfo.fs, AssemblyInformationalVersion)
version=$(sed -n 's/.*AssemblyInformationalVersion("\([^"]*\)").*/\1/p' $repo/GlobalAssemblyInfo.fs)
archs=(${@:-arm64 x64})
for arch in $archs; do
    out=$project/bin/macOS/$arch
    app="$out/ZORA Tracker.app"
    echo "== $arch: publishing"
    rm -rf $out/publish "$app"
    dotnet publish $project -c Release -r osx-$arch --self-contained -o $out/publish -v q -nologo
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    cp -R $out/publish/. "$app/Contents/MacOS/"
    sed "s/__VERSION__/$version/g" $here/Info.plist > "$app/Contents/Info.plist"
    cp $here/AppIcon.icns "$app/Contents/Resources/"
    codesign --force --deep --sign - "$app"
    echo "== $arch: $app (version $version)"
done
