#!/usr/bin/env python3
"""生成「甲基橙」主题标题栏用的化学式像素图（**不依赖任何字体文件**）。

化学式：C₁₄H₁₄N₃NaO₃S（甲基橙 / methyl orange）
字形是下面手写的 5×7 点阵，下标用 3×5 点阵下移排版 —— 不内嵌 Minecraft 字体、也不读系统字体。

用法：python3 tools/make-methyl-orange-formula.py
输出：Plain Craft Launcher 2/Images/methyl_orange_formula.png（159×27，RGBA）
"""
import os
import struct
import zlib

# 5×7 点阵字形
GLYPHS = {
    'C': ['.###.', '#...#', '#....', '#....', '#....', '#...#', '.###.'],
    'H': ['#...#', '#...#', '#...#', '#####', '#...#', '#...#', '#...#'],
    'N': ['#...#', '##..#', '#.#.#', '#..##', '#...#', '#...#', '#...#'],
    'O': ['.###.', '#...#', '#...#', '#...#', '#...#', '#...#', '.###.'],
    'S': ['.####', '#....', '#....', '.###.', '....#', '....#', '####.'],
    'a': ['.....', '.....', '.###.', '....#', '.####', '#...#', '.####'],
}
# 3×5 点阵下标数字
SUBGLYPHS = {
    '1': ['.#.', '##.', '.#.', '.#.', '###'],
    '2': ['##.', '..#', '.#.', '#..', '###'],
    '3': ['##.', '..#', '.#.', '..#', '##.'],
    '4': ['#.#', '#.#', '###', '..#', '..#'],
}

COLOR = (255, 246, 240, 255)
UNIT = 3          # 正常字形每个点 3 px
SUB_UNIT = 2      # 下标每个点 2 px
WIDTH, HEIGHT = 210, 27


def new_canvas():
    return [[(0, 0, 0, 0)] * WIDTH for _ in range(HEIGHT)]


def draw(canvas, pattern, x0, y0, unit):
    for gy, line in enumerate(pattern):
        for gx, ch in enumerate(line):
            if ch != '#':
                continue
            for dy in range(unit):
                for dx in range(unit):
                    x, y = x0 + gx * unit + dx, y0 + gy * unit + dy
                    if 0 <= x < WIDTH and 0 <= y < HEIGHT:
                        canvas[y][x] = COLOR


def encode(path, rows):
    h, w = len(rows), len(rows[0])
    raw = b''.join(b'\x00' + b''.join(bytes(px) for px in row) for row in rows)

    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    png += chunk(b'IDAT', zlib.compress(raw, 9))
    png += chunk(b'IEND', b'')
    open(path, 'wb').write(png)


def main():
    canvas = new_canvas()
    x = 1
    top = 1
    sub_top = top + 5 * UNIT          # 下标落在基线以下
    # 「Na」里的那个 N 是元素符号的一部分，**不带下标**，且与 a 紧排
    tokens = [('C', '14', False), ('H', '14', False), ('N', '3', False),
              ('N', None, True), ('a', None, True), ('O', '3', False), ('S', None, False)]
    for ch, sub, tight in tokens:
        draw(canvas, GLYPHS[ch], x, top, UNIT)
        x += 5 * UNIT
        if sub:
            x += UNIT // 2
            for digit in sub:
                draw(canvas, SUBGLYPHS[digit], x, sub_top, SUB_UNIT)
                x += 3 * SUB_UNIT + 1
            x += UNIT // 2
        elif tight:
            x += UNIT // 3
        else:
            x += UNIT

    used = 0
    for row in canvas:
        for i, px in enumerate(row):
            if px[3]:
                used = max(used, i + 1)
    canvas = [row[:min(WIDTH, used + 1)] for row in canvas]

    out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       'Plain Craft Launcher 2', 'Images', 'methyl_orange_formula.png')
    encode(out, canvas)
    print(f'已生成 {out}（{used + 1}×{HEIGHT}）')


if __name__ == '__main__':
    main()
