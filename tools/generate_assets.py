# -*- coding: utf-8 -*-
"""
文件：tools/generate_assets.py
用途：生成"明眸"应用所需的全部视觉资产（托盘 ICO 图标 + MSIX 包 PNG 资源）
说明：仅依赖 Python 标准库（zlib / struct），无需安装 Pillow 等第三方库。
      重新生成资产时执行：python tools/generate_assets.py
"""

import os
import struct
import zlib

# ---------------------------------------------------------------------------
# 品牌配色（与 XAML 中的设计规范保持一致）
# ---------------------------------------------------------------------------
BRAND_TOP = (0x3B, 0x8C, 0xFF)      # 眼形上缘渐变色
BRAND_BOTTOM = (0x0A, 0x6E, 0xFF)   # 主色 #0A6EFF
IRIS_COLOR = (0xFF, 0xFF, 0xFF)     # 虹膜（白）
PUPIL_COLOR = (0x08, 0x2C, 0x6B)    # 瞳孔（深蓝）
ACCENT_COLOR = (0x00, 0xB8, 0xD4)   # 辅助色 #00B8D4

SUPER_SAMPLE = 4  # 每个像素的抗锯齿采样倍数（4 表示 4x4 = 16 次采样）

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets")


# ---------------------------------------------------------------------------
# 基础绘制工具
# ---------------------------------------------------------------------------
def create_canvas(width, height):
    """创建一块全透明的 RGBA 画布，以 [r, g, b, a] 浮点列表形式存储。"""
    return [[0.0, 0.0, 0.0, 0.0] for _ in range(width * height)]


def blend_pixel(canvas, index, color, alpha):
    """把颜色以 source-over 方式混合到画布指定像素上。"""
    if alpha <= 0.0:
        return
    dst = canvas[index]
    inv = 1.0 - alpha
    dst[0] = color[0] * alpha + dst[0] * inv
    dst[1] = color[1] * alpha + dst[1] * inv
    dst[2] = color[2] * alpha + dst[2] * inv
    dst[3] = alpha + dst[3] * inv


def lens_params(half_width, half_height):
    """
    根据眼形的半宽 a 与半高 b，计算构成"透镜形"的两个圆的偏移量 k 与半径 R。
    透镜形 = 圆心在中心上方与下方的两个等半径圆的交集，正好是眼睛的抽象轮廓。
    """
    k = (half_width * half_width - half_height * half_height) / (2.0 * half_height)
    r = k + half_height
    return k, r


def draw_eye(canvas, width, height, center_x, center_y, half_width, half_height):
    """
    在画布上绘制一只抽象的眼睛：透镜形眼廓 + 白色虹膜 + 深色瞳孔 + 高光点。
    使用 SUPER_SAMPLE x SUPER_SAMPLE 超采样实现抗锯齿。
    """
    k, radius = lens_params(half_width, half_height)
    circle_top = (center_x, center_y + k)      # 决定眼睛上缘的圆（圆心在下方）
    circle_bottom = (center_x, center_y - k)   # 决定眼睛下缘的圆（圆心在上方）

    iris_radius = half_height * 0.60
    pupil_radius = half_height * 0.30
    highlight_radius = half_height * 0.14
    highlight_center = (center_x - iris_radius * 0.34, center_y - iris_radius * 0.34)

    step = 1.0 / SUPER_SAMPLE
    samples_per_pixel = float(SUPER_SAMPLE * SUPER_SAMPLE)

    # 计算眼形的包围盒，避免遍历整张画布
    x_start = max(0, int(center_x - half_width) - 2)
    x_end = min(width, int(center_x + half_width) + 3)
    y_start = max(0, int(center_y - half_height) - 2)
    y_end = min(height, int(center_y + half_height) + 3)

    for py in range(y_start, y_end):
        for px in range(x_start, x_end):
            lens_hits = 0
            iris_hits = 0
            pupil_hits = 0
            highlight_hits = 0
            gradient_sum = 0.0

            for sy in range(SUPER_SAMPLE):
                sample_y = py + (sy + 0.5) * step
                for sx in range(SUPER_SAMPLE):
                    sample_x = px + (sx + 0.5) * step

                    dx_top = sample_x - circle_top[0]
                    dy_top = sample_y - circle_top[1]
                    dx_bottom = sample_x - circle_bottom[0]
                    dy_bottom = sample_y - circle_bottom[1]

                    inside_lens = (dx_top * dx_top + dy_top * dy_top <= radius * radius and
                                   dx_bottom * dx_bottom + dy_bottom * dy_bottom <= radius * radius)
                    if not inside_lens:
                        continue

                    lens_hits += 1
                    # 记录纵向位置比例，用于生成上下渐变
                    gradient_sum += min(1.0, max(0.0,
                                                 (sample_y - (center_y - half_height)) / (2.0 * half_height)))

                    dx_c = sample_x - center_x
                    dy_c = sample_y - center_y
                    dist_center_sq = dx_c * dx_c + dy_c * dy_c
                    if dist_center_sq <= iris_radius * iris_radius:
                        iris_hits += 1
                    if dist_center_sq <= pupil_radius * pupil_radius:
                        pupil_hits += 1

                    dx_h = sample_x - highlight_center[0]
                    dy_h = sample_y - highlight_center[1]
                    if dx_h * dx_h + dy_h * dy_h <= highlight_radius * highlight_radius:
                        highlight_hits += 1

            if lens_hits == 0:
                continue

            index = py * width + px

            # 第一层：眼廓（上浅下深的品牌蓝渐变）
            ratio = gradient_sum / lens_hits
            lens_color = tuple(
                BRAND_TOP[i] + (BRAND_BOTTOM[i] - BRAND_TOP[i]) * ratio for i in range(3)
            )
            blend_pixel(canvas, index, lens_color, lens_hits / samples_per_pixel)

            # 第二层：虹膜
            if iris_hits:
                blend_pixel(canvas, index, IRIS_COLOR, iris_hits / samples_per_pixel)
            # 第三层：瞳孔
            if pupil_hits:
                blend_pixel(canvas, index, PUPIL_COLOR, pupil_hits / samples_per_pixel)
            # 第四层：高光（半透明，柔和一些）
            if highlight_hits:
                blend_pixel(canvas, index, ACCENT_COLOR, (highlight_hits / samples_per_pixel) * 0.85)


def render_icon(width, height, coverage=0.86):
    """
    渲染一张指定尺寸的眼睛图标画布。
    coverage 表示眼形宽度占画布宽度的比例，用于给图标留出安全边距。
    """
    canvas = create_canvas(width, height)
    half_width = width * coverage / 2.0
    half_height = half_width * 0.62  # 眼睛的高宽比，保证形状自然
    # 高度不足时按高度反推，避免眼形被裁切
    max_half_height = height * 0.86 / 2.0
    if half_height > max_half_height:
        half_height = max_half_height
        half_width = half_height / 0.62
    draw_eye(canvas, width, height, width / 2.0, height / 2.0, half_width, half_height)
    return canvas


def canvas_to_rgba_bytes(canvas):
    """把浮点画布转换为 8 位 RGBA 字节序列。"""
    data = bytearray()
    for r, g, b, a in canvas:
        data += bytes((
            max(0, min(255, int(round(r)))),
            max(0, min(255, int(round(g)))),
            max(0, min(255, int(round(b)))),
            max(0, min(255, int(round(a * 255)))),
        ))
    return data


# ---------------------------------------------------------------------------
# PNG 编码
# ---------------------------------------------------------------------------
def encode_png(width, height, rgba_bytes):
    """把 RGBA 字节序列编码为 PNG 文件内容（8 位真彩 + Alpha，无滤波）。"""
    raw = bytearray()
    stride = width * 4
    for y in range(height):
        raw.append(0)  # 每行的滤波类型：0 = None
        raw += rgba_bytes[y * stride:(y + 1) * stride]

    def chunk(tag, payload):
        body = tag + payload
        return struct.pack(">I", len(payload)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


# ---------------------------------------------------------------------------
# ICO 编码
# ---------------------------------------------------------------------------
def encode_ico_bmp_entry(width, height, rgba_bytes):
    """
    把 RGBA 数据编码为 ICO 内部使用的 BMP 结构（BITMAPINFOHEADER + BGRA 位图 + AND 掩码）。
    注意：ICO 中的 BMP 高度需写成实际高度的两倍，且像素行为自下而上排列。
    """
    header = struct.pack("<IiiHHIIiiII",
                         40,            # biSize
                         width,         # biWidth
                         height * 2,    # biHeight（XOR 位图 + AND 掩码）
                         1,             # biPlanes
                         32,            # biBitCount
                         0,             # biCompression = BI_RGB
                         0, 0, 0, 0, 0)

    xor_data = bytearray()
    for y in range(height - 1, -1, -1):  # 自下而上
        row_start = y * width * 4
        for x in range(width):
            offset = row_start + x * 4
            r, g, b, a = rgba_bytes[offset:offset + 4]
            xor_data += bytes((b, g, r, a))  # BMP 采用 BGRA 顺序

    # AND 掩码：32 位图标不再依赖它，全部置 0 即可（每行按 4 字节对齐）
    mask_row_bytes = ((width + 31) // 32) * 4
    and_data = bytes(mask_row_bytes * height)

    return header + bytes(xor_data) + and_data


def encode_ico(images):
    """
    把多个尺寸的图像打包为 ICO 文件。
    images 为 [(size, rgba_bytes), ...]；256 尺寸使用 PNG 压缩以减小体积。
    """
    entries = []
    payloads = []
    for size, rgba in images:
        if size >= 256:
            payload = encode_png(size, size, rgba)
        else:
            payload = encode_ico_bmp_entry(size, size, rgba)
        payloads.append(payload)
        entries.append(size)

    offset = 6 + 16 * len(images)
    directory = struct.pack("<HHH", 0, 1, len(images))
    for i, size in enumerate(entries):
        byte_size = len(payloads[i])
        directory += struct.pack("<BBBBHHII",
                                 0 if size >= 256 else size,
                                 0 if size >= 256 else size,
                                 0, 0, 1, 32,
                                 byte_size, offset)
        offset += byte_size

    return directory + b"".join(payloads)


# ---------------------------------------------------------------------------
# 入口
# ---------------------------------------------------------------------------
def write_png_asset(file_name, width, height, coverage):
    """渲染并写出一张 PNG 资产。"""
    canvas = create_canvas(width, height)
    half_width = min(width, height) * coverage / 2.0
    half_height = half_width * 0.62
    draw_eye(canvas, width, height, width / 2.0, height / 2.0, half_width, half_height)
    path = os.path.join(OUTPUT_DIR, file_name)
    with open(path, "wb") as fp:
        fp.write(encode_png(width, height, canvas_to_rgba_bytes(canvas)))
    print("  生成 {0:<34} {1}x{2}".format(file_name, width, height))


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print("开始生成明眸应用视觉资产 ->", OUTPUT_DIR)

    # 1) 托盘与可执行文件图标（多尺寸 ICO）
    ico_sizes = [16, 20, 24, 32, 40, 48, 64, 256]
    ico_images = []
    for size in ico_sizes:
        # 小尺寸下适当放大眼形，避免细节丢失
        coverage = 0.96 if size <= 24 else 0.90
        canvas = create_canvas(size, size)
        half_width = size * coverage / 2.0
        half_height = half_width * 0.62
        draw_eye(canvas, size, size, size / 2.0, size / 2.0, half_width, half_height)
        ico_images.append((size, canvas_to_rgba_bytes(canvas)))
    ico_path = os.path.join(OUTPUT_DIR, "icon.ico")
    with open(ico_path, "wb") as fp:
        fp.write(encode_ico(ico_images))
    print("  生成 {0:<34} {1}".format("icon.ico", ico_sizes))

    # 2) MSIX 包所需的各类图标
    write_png_asset("Square44x44Logo.png", 44, 44, 0.92)
    write_png_asset("Square150x150Logo.png", 150, 150, 0.64)
    write_png_asset("Wide310x150Logo.png", 310, 150, 0.62)
    write_png_asset("StoreLogo.png", 50, 50, 0.90)
    write_png_asset("SplashScreen.png", 620, 300, 0.34)
    write_png_asset("LockScreenLogo.png", 24, 24, 0.96)

    # 3) 预览图（仅用于查看效果，不参与打包）
    write_png_asset("icon-preview.png", 256, 256, 0.88)

    print("全部资产生成完成。")


if __name__ == "__main__":
    main()
