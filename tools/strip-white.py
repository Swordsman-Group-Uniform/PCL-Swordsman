#!/usr/bin/env python3
"""把 png 的纯色背景扣成透明（Color to Alpha），只用标准库。

用法：
    python3 tools/strip-white.py <输入.png> <输出.png> [R G B] [--eps 0.02]

说明：
 * 只支持 8 位、非隔行的 RGB(2) / RGBA(6) png —— 品牌图够用，避免引入 Pillow 依赖。
 * 背景色默认纯白 #FFFFFF；alpha 按「像素离背景色多远」计算，边缘会自然变成半透明，
   不会留下白边（简单的阈值抠图会留一圈白毛）。
 * 输出统一为 8 位 RGBA。
"""

import struct
import sys
import zlib


def paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def read_png(path):
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("不是 png 文件")
    pos = 8
    width = height = bitdepth = colortype = interlace = None
    idat = bytearray()
    while pos < len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        ctype = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            width, height, bitdepth, colortype, _, _, interlace = struct.unpack(">IIBBBBB", body)
        elif ctype == b"IDAT":
            idat += body
        elif ctype == b"IEND":
            break
    if bitdepth != 8 or interlace != 0 or colortype not in (2, 6):
        raise SystemExit(f"暂不支持：bitdepth={bitdepth} colortype={colortype} interlace={interlace}")
    channels = 3 if colortype == 2 else 4
    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    pixels = bytearray(width * height * 4)
    prev = bytearray(stride)
    src = 0
    for y in range(height):
        ftype = raw[src]
        src += 1
        line = bytearray(raw[src:src + stride])
        src += stride
        if ftype == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif ftype == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ftype == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif ftype == 4:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                upleft = prev[i - channels] if i >= channels else 0
                line[i] = (line[i] + paeth(left, prev[i], upleft)) & 0xFF
        elif ftype != 0:
            raise SystemExit(f"未知行过滤类型 {ftype}")
        prev = line
        dst = y * width * 4
        if channels == 3:
            for x in range(width):
                pixels[dst:dst + 3] = line[x * 3:x * 3 + 3]
                pixels[dst + 3] = 255
                dst += 4
        else:
            pixels[dst:dst + 4] = line
            dst += width * 0
    return width, height, pixels


def write_png(path, width, height, pixels):
    raw = bytearray()
    stride = width * 4
    for y in range(height):
        raw.append(0)  # filter: None
        raw += pixels[y * stride:(y + 1) * stride]

    def chunk(ctype, body):
        return (struct.pack(">I", len(body)) + ctype + body +
                struct.pack(">I", zlib.crc32(ctype + body) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    out = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(out)


def color_to_alpha() -> tuple[int, int, int]:
    return (255, 255, 255)


def main(argv):
    if len(argv) < 3:
        raise SystemExit(__doc__)
    src, dst = argv[1], argv[2]
    keys = [a for a in argv[3:] if not a.startswith("--")]
    bg = tuple(int(v) for v in keys[:3]) if len(keys) >= 3 else color_to_alpha()

    width, height, pixels = read_png(src)
    transparent = 0
    for i in range(0, len(pixels), 4):
        r, g, b = pixels[i], pixels[i + 1], pixels[i + 2]
        # 与背景色的最大距离决定透明度：越接近背景越透明
        dist = max(abs(r - bg[0]), abs(g - bg[1]), abs(b - bg[2])) / 255.0
        if dist <= 0.02:
            pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 0
            transparent += 1
            continue
        a = dist
        # 反预乘：把混进的白背景减掉，边缘不会发白
        for c in range(3):
            v = (pixels[i + c] - bg[c] * (1 - a)) / a
            pixels[i + c] = 0 if v < 0 else (255 if v > 255 else int(v + 0.5))
        pixels[i + 3] = int(a * 255 + 0.5)

    write_png(dst, width, height, pixels)
    total = width * height
    print(f"{src} → {dst}：{width}×{height}，透明像素 {transparent}/{total}（{transparent / total:.1%}）")


if __name__ == "__main__":
    main(sys.argv)
