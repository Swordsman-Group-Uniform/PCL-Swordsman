#!/bin/sh
# 构建 PCLCE For SGU（PCL: CE 下游分支）
#
# 用法：
#   ./build.sh                # 默认 Release + x64
#   ./build.sh ARM64          # Release + win-arm64
#   ./build.sh x64 Debug      # 指定平台与配置
#
# 产物命名规范（见 VERSIONING.md）：PCLCE For SGU [版本号].exe
#   例如：PCLCE For SGU 27.1-snapshot-8(2.15.1).exe
#
# 说明：Android/Termux 没有 /usr/bin/env，因此这里先用 POSIX sh 兜底再切到 bash。

if [ -z "${BASH_VERSION:-}" ]; then
    exec bash "$0" "$@"
fi

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLATFORM="${1:-x64}"
CONFIG="${2:-Release}"
OUT="$ROOT/artifact-$(echo "$PLATFORM" | tr '[:upper:]' '[:lower:]')"

# 读取 metadata.json 中的版本号，拼成 下游版本号(上游版本号)
VERSION_OUTPUT="$(python3 - "$ROOT" <<'PY'
import json, os, sys
meta_path = os.path.join(sys.argv[1], "Plain Craft Launcher 2", "metadata.json")
with open(meta_path, encoding="utf-8-sig") as fp:
    version = json.load(fp)["version"]
suffix = version.get("suffix") or ""
base = version["base"] + (("-" + suffix) if suffix else "")
print(f"{base}({version['upstream']})")
print(suffix)
PY
)"
VERSION="$(printf '%s\n' "$VERSION_OUTPUT" | sed -n '1p')"
VERSION_SUFFIX="$(printf '%s\n' "$VERSION_OUTPUT" | sed -n '2p')"

echo "==> 构建 PCLCE For SGU ${VERSION}（${CONFIG}/${PLATFORM}）"

# 构建前校验：本地化 XAML 里不允许出现重复的 x:Key。
# 重复键会让 ResourceDictionary 加载抛异常，本地化静默失败后整个界面所有文字都会变空。
for f in "$ROOT"/PCL.Core/App/Localization/Languages/*.xaml; do
    [ -f "$f" ] || continue
    DUPS="$(grep -o 'x:Key="[^"]*"' "$f" | sort | uniq -d || true)"
    if [ -n "$DUPS" ]; then
        echo "!! 本地化文件 $(basename "$f") 含重复键：$DUPS" >&2
        exit 1
    fi
done

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# 插件包必须先打出来：非正式版会把它们嵌进 exe（IncludeBundledPlugins=true）
echo "==> 构建插件包"
"$ROOT/plugins/package.sh" "$CONFIG" "$PLATFORM" || echo "!! 插件打包失败，继续构建本体" >&2

BUNDLE_FLAG="false"
if [ -n "${VERSION_SUFFIX:-}" ]; then
    BUNDLE_FLAG="true"
fi

dotnet publish "$ROOT/Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" \
    -p:Configuration="$CONFIG" \
    -p:Platform="$PLATFORM" \
    -p:DeleteExistingFiles=true \
    -p:IncludeBundledPlugins="$BUNDLE_FLAG" \
    -m:1 \
    -p:UseSharedCompilation=false \
    -o "$OUT" \
    --no-self-contained

# 按命名规范重命名产物（程序集名不能改，否则会破坏 pack:// 资源 URI）
SRC="$OUT/Plain Craft Launcher 2.exe"
DST="$OUT/PCLCE For SGU ${VERSION}.exe"
if [ -f "$SRC" ]; then
    mv -f "$SRC" "$DST"
    # 同步一份到 dist/（发布包目录，与 samples 的 .pclpkg 放在一起）
    DIST="$ROOT/dist"
    mkdir -p "$DIST"
    cp -f "$DST" "$DIST/"
    echo "==> 构建完成：$DST"
    echo "==> 已同步到发布包目录：$DIST/$(basename "$DST")"

    # 插件包：始终给发布包目录留一份（正式版用户要手动装）
    if [ -d "$ROOT/plugins/dist" ]; then
        mkdir -p "$DIST/plugins"
        cp -f "$ROOT"/plugins/dist/*.pclpkg "$DIST/" 2>/dev/null || true
        cp -f "$ROOT"/plugins/dist/*.pclpkg "$DIST/plugins/" 2>/dev/null || true
    fi

    # 非正式版：把插件包放到 exe 同级 plugins/ 目录，启动器首次运行会自动安装
    if [ -n "${VERSION_SUFFIX:-}" ]; then
        # 先清掉上一次构建留下的插件包，否则删掉的插件（如 FadedGray）会一直留在产物目录里
        rm -rf "$OUT/plugins"
        mkdir -p "$OUT/plugins"
        for pkg in "$ROOT"/plugins/dist/*.pclpkg; do
            [ -f "$pkg" ] || continue
            case "$(basename "$pkg")" in
                # FadedGray（Faded灰主题）按要求**不内置**：只在 dist/ 里放一份供手动安装
                FadedGray.pclpkg) continue ;;
            esac
            cp -f "$pkg" "$OUT/plugins/" 2>/dev/null || true
        done
        echo "==> 非正式版构建：已附带插件包（首次启动自动安装；FadedGray 不内置，需手动安装 dist/ 里的那份）"
    else
        rm -rf "$OUT/plugins"
        echo "==> 正式版构建：不随包附带插件（插件包已放入 dist/plugins/）"
    fi
else
    echo "!! 未找到预期产物：$SRC" >&2
    exit 1
fi
