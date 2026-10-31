#!/usr/bin/env python3
"""给 .pclpkg 插件包写入文件哈希（`plugin.json` 的 `files` 字段）。

用法：
    python3 tools/plugin-hash.py <包.pclpkg> [更多包...]

作用：算出包里除 `plugin.json` 以外每个文件的 SHA-256，写回 `plugin.json` 的 `files` 字段，
宿主在**安装时**与**每次加载前**都会校验，包被改过就会拒绝加载。

- 不含签名（签名见 tools/PluginSigner，需要公钥信任列表）。
- 幂等：重复运行只会刷新哈希。
- 这是 `plugins/package.sh` 的收尾步骤，内置插件包因此都带哈希。
"""
import hashlib
import json
import os
import shutil
import sys
import tempfile
import zipfile


def hash_entry(archive: zipfile.ZipFile, name: str) -> str:
    digest = hashlib.sha256()
    with archive.open(name) as stream:
        for chunk in iter(lambda: stream.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def add_hashes(package_path: str) -> int:
    if not zipfile.is_zipfile(package_path):
        print(f"!! 不是 zip 包：{package_path}")
        return 1

    with zipfile.ZipFile(package_path) as archive:
        names = [name for name in archive.namelist() if name and not name.endswith("/")]

        manifest_name = None
        for candidate in ("plugin.json",):
            if candidate in names:
                manifest_name = candidate
                break
        if manifest_name is None:
            manifest_name = next((n for n in names if n.lower() == "plugin.json"), None)
        if manifest_name is None:
            print(f"!! 包里没有 plugin.json：{package_path}")
            return 1

        manifest = json.loads(archive.read(manifest_name).decode("utf-8-sig"))
        files = {}
        for name in names:
            if name == manifest_name:
                continue
            files[name] = hash_entry(archive, name)

        manifest["files"] = dict(sorted(files.items()))
        payloads = {name: archive.read(name) for name in names}

    payloads[manifest_name] = json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8")

    # 重写 zip（保持条目顺序，plugin.json 放最前）
    order = [manifest_name] + [n for n in payloads if n != manifest_name]
    fd, temp_path = tempfile.mkstemp(suffix=".pclpkg")
    os.close(fd)
    try:
        with zipfile.ZipFile(temp_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as out:
            for name in order:
                out.writestr(name, payloads[name])
        shutil.move(temp_path, package_path)
    finally:
        if os.path.exists(temp_path):
            os.remove(temp_path)

    print(f"  {os.path.basename(package_path)}：已写入 {len(files)} 个文件的 SHA-256")
    return 0


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    code = 0
    for package in sys.argv[1:]:
        code |= add_hashes(package)
    return code


if __name__ == "__main__":
    raise SystemExit(main())
