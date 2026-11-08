#!/usr/bin/env python3
"""从原版（Minecraft）客户端 jar 里解出本分支要复用的原版材质。

用法：
    python3 tools/extract-vanilla-textures.py <client.jar>

`client.jar` 是原版客户端 jar（含 assets），例如 NeoForge 开发环境的
`~/.gradle/caches/minecraft/versions/1.21.1/client.jar`。

解出的文件（都是**原版贴图，未做任何重绘**）：
    Plain Craft Launcher 2/Images/Blocks/NetherWartBlock.png      地狱疣块（「下界红」主题）
    Plain Craft Launcher 2/Images/Blocks/AmethystBlock.png        紫水晶块（「水晶紫」主题）
    Plain Craft Launcher 2/Images/Particles/cherry_0..11.png      樱花粒子（「樱花粉」主题，12 张 3×3 精灵）

    # snapshot-86 的四个新主题
    Plain Craft Launcher 2/Images/Blocks/Glass.png                玻璃（「琉璃彩」内部纹理 / 粒子）
    Plain Craft Launcher 2/Images/Blocks/GlassPaneTop.png         玻璃板顶边（「琉璃彩」8px 边缘）
    Plain Craft Launcher 2/Images/Blocks/Obsidian.png             黑曜石（「曜石黑」80%）
    Plain Craft Launcher 2/Images/Blocks/CryingObsidian.png       哭泣黑曜石（「曜石黑」20%）
    Plain Craft Launcher 2/Images/Blocks/HoneycombBlock.png       蜂巢块（「蜂蜜黄」40%）
    Plain Craft Launcher 2/Images/Blocks/Honeycomb.png            蜜脾（「蜂蜜黄」40%，注意是 item/honeycomb）
    Plain Craft Launcher 2/Images/Blocks/BeeNestSide.png          蜂巢侧面（「蜂蜜黄」10%）
    Plain Craft Launcher 2/Images/Blocks/BeehiveSide.png          蜂箱侧面（「蜂蜜黄」10%）
    Plain Craft Launcher 2/Images/Blocks/SlimeBlock.png           黏液块（「黏液绿」）

注意：这些是 Mojang 的原版美术资源，仅供本下游分支自用；不要再往别处分发，
也不要提交进仓库（`.gitignore` 里逐条排除了它们）。
"""
import sys
import os
import zipfile

BLOCKS = {
    "assets/minecraft/textures/block/nether_wart_block.png": "NetherWartBlock.png",
    "assets/minecraft/textures/block/amethyst_block.png": "AmethystBlock.png",
    # 「琉璃彩」主题：原版玻璃（内部粒子）+ 玻璃板顶边（8px 边缘）
    "assets/minecraft/textures/block/glass.png": "Glass.png",
    "assets/minecraft/textures/block/glass_pane_top.png": "GlassPaneTop.png",
    # 「曜石黑」主题
    "assets/minecraft/textures/block/obsidian.png": "Obsidian.png",
    "assets/minecraft/textures/block/crying_obsidian.png": "CryingObsidian.png",
    # 「蜂蜜黄」主题（蜂巢块 / 蜜脾 / 蜂巢侧面 / 蜂箱侧面）
    "assets/minecraft/textures/block/honeycomb_block.png": "HoneycombBlock.png",
    "assets/minecraft/textures/item/honeycomb.png": "Honeycomb.png",
    "assets/minecraft/textures/block/bee_nest_side.png": "BeeNestSide.png",
    "assets/minecraft/textures/block/beehive_side.png": "BeehiveSide.png",
    # 「黏液绿」主题
    "assets/minecraft/textures/block/slime_block.png": "SlimeBlock.png",
}
PARTICLES = [f"assets/minecraft/textures/particle/cherry_{i}.png" for i in range(12)]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BLOCK_DIR = os.path.join(ROOT, "Plain Craft Launcher 2", "Images", "Blocks")
PARTICLE_DIR = os.path.join(ROOT, "Plain Craft Launcher 2", "Images", "Particles")


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    jar = sys.argv[1]
    if not os.path.isfile(jar):
        print(f"!! 找不到 jar：{jar}")
        return 2

    os.makedirs(BLOCK_DIR, exist_ok=True)
    os.makedirs(PARTICLE_DIR, exist_ok=True)

    with zipfile.ZipFile(jar) as z:
        names = set(z.namelist())
        written = 0
        for src, dst_name in BLOCKS.items():
            if src not in names:
                print(f"!! jar 里没有 {src}")
                continue
            data = z.read(src)
            open(os.path.join(BLOCK_DIR, dst_name), "wb").write(data)
            print(f"  {src} -> Images/Blocks/{dst_name}（{len(data)} B）")
            written += 1

        for src in PARTICLES:
            if src not in names:
                print(f"!! jar 里没有 {src}")
                continue
            data = z.read(src)
            open(os.path.join(PARTICLE_DIR, os.path.basename(src)), "wb").write(data)
            written += 1
        print(f"  {len(PARTICLES)} 张樱花粒子 -> Images/Particles/")

    print(f"完成：共写出 {written} 个文件")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
