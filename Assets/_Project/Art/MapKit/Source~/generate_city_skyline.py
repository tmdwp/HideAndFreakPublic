"""
도시 길거리 맵(Map_CityStreet)의 하늘(skybox)용 밤 도시 파노라마를 그린다. (360° 위도·경도 형식, 가로 = 방위 360°, 세로 = 고도 +90°~−90°)
렌더링 없이 픽셀을 직접 칠한다: 밤하늘 그라데이션·별·달, 먼 곳→가까운 곳 3겹 빌딩 실루엣(가까울수록 어둡고 높음, 불 켜진 창),
옥상 안테나의 붉은 항공등, 지평선 아래는 어두운 땅과 먼 가로등 불빛.
같은 시드라 다시 실행해도 같은 그림이 나온다.

실행:
  blender -b --factory-startup --python generate_city_skyline.py -- <PNG 저장 경로> [<가로 픽셀, 기본 4096>]
"""
import math
import os
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outPath = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Textures", "T_City_NightSkyline.png")
width = int(argv[1]) if len(argv) > 1 else 4096
height = width // 2
horizon = height // 2
rng = np.random.default_rng(20261110)


def elevationRow(degrees):
    """고도(도)를 위에서부터 센 픽셀 줄로 바꾼다. (0° = 지평선)"""
    return int(round(horizon - degrees / 90.0 * horizon))


def paintSky(image):
    """밤하늘: 위는 짙은 남색, 지평선 쪽은 도시 불빛이 비친 보랏빛·주황빛. 아래 반은 어두운 땅."""
    rows = np.arange(height, dtype=np.float32)
    elevation = (horizon - rows) / horizon * 90.0
    top = np.array([0.015, 0.02, 0.05])
    mid = np.array([0.06, 0.06, 0.13])
    glow = np.array([0.30, 0.17, 0.16])
    sky = np.empty((height, 3), dtype=np.float32)
    for i, e in enumerate(elevation):
        if e >= 0:
            t = min(e / 25.0, 1.0)
            low = glow * (1 - t) + mid * t
            u = min(max((e - 25.0) / 65.0, 0.0), 1.0)
            sky[i] = low * (1 - u) + top * u
        else:
            t = min(-e / 12.0, 1.0)
            sky[i] = np.array([0.09, 0.06, 0.07]) * (1 - t) + np.array([0.02, 0.02, 0.025]) * t
    image[:, :, :3] = sky[:, None, :]
    image[:, :, 3] = 1.0


def paintStars(image, count):
    """고도 20° 위에 작은 별을 뿌린다. (밝기 무작위)"""
    lowest = elevationRow(20)
    ys = rng.integers(0, lowest, count)
    xs = rng.integers(0, width, count)
    brightness = rng.uniform(0.35, 0.9, count)
    for x, y, b in zip(xs, ys, brightness):
        image[y, x, :3] = np.maximum(image[y, x, :3], b)


def paintMoon(image, azimuth, elevation, radius):
    """은은한 빛무리가 있는 달."""
    cx = int(azimuth / 360.0 * width)
    cy = elevationRow(elevation)
    halo = radius * 5
    for y in range(cy - halo, cy + halo + 1):
        for x in range(cx - halo, cx + halo + 1):
            d = math.hypot(x - cx, y - cy)
            px = x % width
            if d <= radius:
                shade = 0.88 - 0.12 * (d / radius)
                image[y, px, :3] = (shade, shade * 0.97, shade * 0.9)
            elif d <= halo:
                a = (1 - (d - radius) / (halo - radius)) ** 2 * 0.25
                image[y, px, :3] = image[y, px, :3] * (1 - a) + np.array([0.6, 0.6, 0.7]) * a


def paintLayer(image, count, widthRange, heightRange, color, windowChance, windowScale, beacons):
    """
    빌딩 한 겹: 지평선에서 솟은 직사각형 실루엣. 가로는 방위를 따라 이어 붙고(360° 넘으면 감김),
    창은 격자로 일부만 켠다. 일부 높은 건물은 옥상에 안테나와 붉은 항공등.
    """
    x = int(rng.integers(0, width))
    base = elevationRow(-0.6)
    for _ in range(count):
        w = int(rng.integers(*widthRange))
        top = elevationRow(float(rng.uniform(*heightRange)))
        shade = np.array(color) * float(rng.uniform(0.8, 1.15))
        columns = [(x + i) % width for i in range(w)]
        image[top:base, columns, :3] = shade
        # 계단식 옥상
        if rng.random() < 0.35 and w > 8:
            inset = w // 4
            step = int(rng.integers(3, max(4, (base - top) // 6)))
            image[top - step:top, columns[inset:w - inset], :3] = shade

        cell = max(3, int(windowScale))
        for wy in range(top + cell, base - cell, cell * 2):
            for wx in range(cell, w - cell, cell * 2):
                if rng.random() < windowChance:
                    warm = rng.random() < 0.75
                    lit = np.array([1.0, 0.78, 0.45]) if warm else np.array([0.7, 0.85, 1.0])
                    lit = lit * float(rng.uniform(0.55, 1.0))
                    cols = [columns[c] for c in range(wx, min(wx + cell, w))]
                    image[wy:wy + max(1, cell - 1), cols, :3] = lit

        if beacons and rng.random() < 0.18 and w > 6:
            mast = columns[w // 2]
            mastTop = top - int(rng.integers(8, 22))
            image[mastTop:top, mast, :3] = shade
            image[mastTop - 2:mastTop + 1, [(mast - 1) % width, mast, (mast + 1) % width], :3] = (1.0, 0.12, 0.08)

        x = (x + w + int(rng.integers(0, max(2, w // 3)))) % width


def paintGroundLights(image, count):
    """지평선 바로 아래 먼 거리의 가로등·차 불빛 점."""
    for _ in range(count):
        y = elevationRow(-float(rng.uniform(0.8, 6.0)))
        x = int(rng.integers(0, width))
        lit = np.array([1.0, 0.75, 0.4]) * float(rng.uniform(0.3, 0.8))
        image[y, x, :3] = np.maximum(image[y, x, :3], lit)


def main():
    """파노라마를 그려 PNG로 저장한다."""
    image = np.zeros((height, width, 4), dtype=np.float32)
    paintSky(image)
    paintStars(image, width // 3)
    paintMoon(image, azimuth=118.0, elevation=34.0, radius=max(6, width // 230))
    scale = width / 4096.0
    paintLayer(image, int(260 * scale + 40), (int(10 * scale) + 4, int(34 * scale) + 6), (2.0, 7.0), (0.11, 0.10, 0.16), 0.10, 2 * scale, False)
    paintLayer(image, int(200 * scale + 30), (int(16 * scale) + 4, int(52 * scale) + 6), (4.0, 13.0), (0.065, 0.06, 0.10), 0.16, 3 * scale, True)
    paintLayer(image, int(140 * scale + 20), (int(30 * scale) + 6, int(90 * scale) + 8), (7.0, 24.0), (0.03, 0.03, 0.05), 0.22, 4 * scale, True)
    paintGroundLights(image, width // 2)

    os.makedirs(os.path.dirname(os.path.abspath(outPath)), exist_ok=True)
    img = bpy.data.images.new("T_City_NightSkyline", width=width, height=height, alpha=False)
    img.pixels.foreach_set(np.flipud(np.clip(image, 0.0, 1.0)).ravel())
    img.filepath_raw = os.path.abspath(outPath)
    img.file_format = "PNG"
    img.save()
    print("[CitySkyline] saved", outPath, width, "x", height)


main()
