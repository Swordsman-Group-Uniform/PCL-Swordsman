#!/bin/sh
# 打包插件为 .pclpkg（zip：plugin.json + dll），产物到 plugins/dist/
#
# 用法：
#   ./plugins/package.sh                 # Release / x64
#   ./plugins/package.sh Release ARM64
#
# 说明：Android/Termux 没有 /usr/bin/env 与 zip 命令，因此先用 POSIX sh 兜底再切到 bash，
#      并用 python3 的 zipfile 打包。

if [ -z "${BASH_VERSION:-}" ]; then
    exec bash "$0" "$@"
fi

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${1:-Release}"
PLATFORM="${2:-x64}"
DIST="$ROOT/plugins/dist"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

mkdir -p "$DIST"

for name in SguPack PCLSCore AdvancedMaterials Welcome FadedGray HelloPlugin; do
    # HelloPlugin 在 samples/ 下，其余在 plugins/ 下
    if [ "$name" = "HelloPlugin" ]; then
        base="$ROOT/samples/$name"
    else
        base="$ROOT/plugins/$name"
    fi
    project="$base/$name.csproj"
    echo "==> 构建插件 $name（${CONFIG}/${PLATFORM}）"
    dotnet build "$project" -c "$CONFIG" -p:Platform="$PLATFORM" \
        -p:RunAnalyzers=false -m:1 -p:UseSharedCompilation=false -v q >/dev/null

    out="$base/bin/$PLATFORM/$CONFIG/net10.0-windows"
    if [ ! -f "$out/plugin.json" ] || [ ! -f "$out/$name.dll" ]; then
        echo "!! 未找到插件输出：$out" >&2
        exit 1
    fi

    pkg="$DIST/$name.pclpkg"
    rm -f "$pkg"
    python3 - "$out" "$pkg" "$name" <<'PYZIP'
import os
import sys
import zipfile

src, dst, name = sys.argv[1], sys.argv[2], sys.argv[3]
files = ["plugin.json", name + ".dll"]
with zipfile.ZipFile(dst, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for file in files:
        path = os.path.join(src, file)
        if not os.path.isfile(path):
            raise SystemExit("missing " + path)
        archive.write(path, file)
print("packed %d files -> %s" % (len(files), dst))
PYZIP

    # 写入文件哈希（宿主安装 / 加载时会校验；包被改过就会被拒绝加载）
    python3 "$ROOT/tools/plugin-hash.py" "$pkg"
    echo "==> 已打包：$pkg"
done

echo "==> 插件包全部就绪：$DIST"
