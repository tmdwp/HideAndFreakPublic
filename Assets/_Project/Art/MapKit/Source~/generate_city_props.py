"""
도시 길거리 소품(MapKit City) 생성 스크립트. 밤의 도심 사거리(Map_CityStreet)에 쓰는 거리·가게·주차장·공사장 소품을 만든다.
목록과 크기는 Docs/CityStreetMap.md "오브젝트 모델 목록"과 같다.

실행:
  blender -b --factory-startup --python generate_city_props.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 바닥 중앙, 앞면은 블렌더 -Y = Unity +Z)
발광 머티리얼(M_City_Glow*)은 Unity 맵 빌더가 이름으로 찾아 발광 머티리얼로 바꿔 끼운다.
디테일(볼트·손잡이·라벨·이음선)은 MeshBuilder의 면 디테일 도우미로 붙여 베벨을 받지 않게 한다(변신 사물로 많이 깔려 폴리곤을 아낀다).
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import MeshBuilder, configure, exportFbx, layoutGrid, renderPreview, resetScene, rotX, rotY, rotZ, surfaceOf

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models", "City")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "PoleGray":     ((0.32, 0.34, 0.36), 0.7, 0.45, 0.0),
    "PoleDark":     ((0.12, 0.13, 0.14), 0.6, 0.5, 0.0),
    "Steel":        ((0.70, 0.72, 0.74), 0.85, 0.3, 0.0),
    "Black":        ((0.05, 0.05, 0.06), 0.0, 0.6, 0.0),
    "Rubber":       ((0.04, 0.04, 0.04), 0.0, 0.9, 0.0),
    "White":        ((0.88, 0.88, 0.86), 0.0, 0.45, 0.0),
    "Gray":         ((0.55, 0.56, 0.57), 0.1, 0.55, 0.0),
    "Concrete":     ((0.50, 0.49, 0.47), 0.0, 0.9, 0.0),
    "Red":          ((0.72, 0.07, 0.06), 0.1, 0.45, 0.0),
    "Yellow":       ((0.92, 0.72, 0.07), 0.0, 0.5, 0.0),
    "Orange":       ((0.92, 0.40, 0.06), 0.0, 0.5, 0.0),
    "Green":        ((0.10, 0.36, 0.20), 0.1, 0.5, 0.0),
    "Blue":         ((0.10, 0.26, 0.62), 0.1, 0.45, 0.0),
    "Wood":         ((0.45, 0.30, 0.17), 0.0, 0.7, 0.0),
    "WoodDark":     ((0.26, 0.17, 0.10), 0.0, 0.75, 0.0),
    "Plant":        ((0.18, 0.40, 0.16), 0.0, 0.85, 0.0),
    "Soil":         ((0.20, 0.14, 0.09), 0.0, 1.0, 0.0),
    "TrashBag":     ((0.08, 0.09, 0.10), 0.0, 0.25, 0.0),
    "CarPaint":     ((0.55, 0.08, 0.08), 0.6, 0.3, 0.0),
    "CarPaintB":    ((0.75, 0.76, 0.78), 0.7, 0.3, 0.0),
    "Glass":        ((0.12, 0.16, 0.20), 0.3, 0.1, 0.0),
    "CrateBlue":    ((0.10, 0.30, 0.65), 0.0, 0.6, 0.0),
    "Canvas":       ((0.78, 0.20, 0.16), 0.0, 0.85, 0.0),
    "GlowWarm":     ((1.00, 0.82, 0.55), 0.0, 0.3, 3.0),
    "GlowWhite":    ((0.92, 0.95, 1.00), 0.0, 0.3, 2.5),
    "GlowRed":      ((1.00, 0.12, 0.08), 0.0, 0.3, 3.0),
    "GlowGreen":    ((0.15, 1.00, 0.45), 0.0, 0.3, 3.0),
    "GlowAmber":    ((1.00, 0.60, 0.10), 0.0, 0.3, 3.0),
}
configure(palette, "M_City_")


def wheel(b, x, y, z, radius, width, tire="Rubber", hub="Steel", segments=16, lugs=0):
    """옆에서 보는 바퀴(축이 블렌더 Y): 타이어 + 휠 + 가운데 허브 캡(+ lugs개 휠 너트)."""
    b.cylinder(radius, radius, width, (x, y, z), tire, segments=segments, rot=rotX(90))
    b.cylinder(radius * 0.55, radius * 0.55, width + 0.01, (x, y, z), hub, segments=segments, rot=rotX(90))
    with b.noBevel():
        b.cylinder(radius * 0.16, radius * 0.16, width + 0.03, (x, y, z), "PoleDark", segments=8, rot=rotX(90))
        side = 1 if y >= 0 else -1
        for k in range(lugs):
            a = 2 * math.pi * k / lugs
            b.cylinder(radius * 0.045, radius * 0.045, 0.01,
                       (x + radius * 0.3 * math.cos(a), y + side * (width / 2 + 0.008), z + radius * 0.3 * math.sin(a)),
                       "PoleDark", segments=6, rot=rotX(90))


def spokedWheel(b, x, z, radius, width):
    """자전거 바퀴: 얇은 타이어 고리 + 림 + 허브 + 십자 살. 꽉 찬 원판보다 자전거답게 보이게."""
    b.hollowCylinder(radius, radius, width, 0.035, (x, 0, z), "Rubber", segments=20, rot=rotX(90))
    b.hollowCylinder(radius - 0.035, radius - 0.035, width * 0.6, 0.012, (x, 0, z), "Steel", segments=20, rot=rotX(90))
    b.cylinder(0.03, 0.03, width + 0.04, (x, 0, z), "Steel", segments=8, rot=rotX(90))
    for k in range(4):
        b.box((2 * (radius - 0.04), 0.006, 0.006), (x, 0, z), "Steel", rot=rotY(k * 45))


# ---------------------------------------------------------------- 도로 시설

def makeStreetLamp():
    """가로등(높이 5.2m): 받침(볼트·칼라) + 기둥(점검구) + 앞(-Y)으로 뻗은 팔(보강대) + 등갓 아래 발광판. 빛은 Unity 맵 조명이 낸다."""
    b = MeshBuilder("City_StreetLamp", "City", bevel=0.01)
    b.cylinder(0.2, 0.2, 0.3, (0, 0, 0.15), "PoleDark", segments=12)
    b.cylinder(0.08, 0.06, 4.9, (0, 0, 2.75), "PoleGray", segments=12)
    b.box((0.08, 1.2, 0.08), (0, -0.6, 5.1), "PoleGray")
    b.box((0.36, 0.6, 0.14), (0, -1.15, 5.05), "PoleDark")
    b.box((0.28, 0.5, 0.02), (0, -1.15, 4.97), "GlowWarm")
    b.bolts("+Z", (0, 0, 0.3), [(0.15 * math.cos(math.radians(a)), 0.15 * math.sin(math.radians(a))) for a in (45, 135, 225, 315)],
            "Steel", radius=0.018, depth=0.014)
    with b.noBevel():
        b.cylinder(0.1, 0.095, 0.08, (0, 0, 0.34), "PoleDark", segments=12)
        b.cylinder(0.075, 0.075, 0.05, (0, 0, 4.2), "PoleDark", segments=12)
        b.tube((0, -0.05, 4.65), (0, -0.55, 5.06), 0.02, "PoleGray")
        b.box((0.3, 0.5, 0.04), (0, -1.15, 5.14), "PoleGray")
    b.plate("-Y", (0, -0.077, 0), 0.07, 0.24, 0.006, "PoleDark", v=0.75)
    b.bolts("-Y", (0, -0.083, 0), [(0, 0.85), (0, 0.65)], "Steel", radius=0.007, depth=0.004)
    return b.build()


def makeTrafficLight():
    """신호등(높이 4.4m): 기둥 + 앞(-Y)으로 뻗은 팔 + 3색 신호(차양·검은 테두리, 빨강만 켜짐) + 기둥의 보행 신호·보행 버튼 함."""
    b = MeshBuilder("City_TrafficLight", "City", bevel=0.01)
    b.cylinder(0.12, 0.12, 0.2, (0, 0, 0.1), "PoleDark", segments=12)
    b.cylinder(0.07, 0.07, 4.3, (0, 0, 2.25), "Yellow", segments=12)
    b.box((0.08, 2.4, 0.08), (0, -1.2, 4.2), "Yellow")
    b.box((1.0, 0.3, 0.34), (0, -2.2, 4.0), "PoleDark")
    for x, glow in ((-0.32, "GlowRed"), (0.0, "Black"), (0.32, "Black")):
        b.cylinder(0.11, 0.11, 0.04, (x, -2.37, 4.0), glow, segments=14, rot=rotX(90))
        with b.noBevel():
            b.box((0.26, 0.16, 0.02), (x, -2.43, 4.13), "PoleDark", rot=rotX(-8))
    b.frame("-Y", (0, -2.35, 4.0), 1.12, 0.46, 0.05, 0.012, "Black")
    with b.noBevel():
        for x in (-0.3, 0.3):
            b.box((0.06, 0.06, 0.08), (x, -2.2, 4.2), "PoleDark")
        b.box((0.26, 0.1, 0.02), (0, -0.37, 2.84), "PoleDark")
        b.box((0.26, 0.1, 0.02), (0, -0.37, 2.58), "PoleDark")
    b.box((0.3, 0.25, 0.55), (0, -0.2, 2.6), "PoleDark")
    b.box((0.22, 0.02, 0.2), (0, -0.33, 2.73), "GlowRed")
    b.box((0.22, 0.02, 0.2), (0, -0.33, 2.47), "Black")
    b.box((0.12, 0.1, 0.18), (0, -0.11, 1.1), "Yellow")
    b.plate("-Y", (0, -0.16, 0), 0.08, 0.06, 0.004, "White", v=1.14)
    b.bolts("-Y", (0, -0.16, 0), [(0, 1.06)], "Black", radius=0.022, depth=0.012)
    b.bolts("+Z", (0, 0, 0.2), [(0.09 * math.cos(math.radians(a)), 0.09 * math.sin(math.radians(a))) for a in (45, 135, 225, 315)],
            "Steel", radius=0.012, depth=0.01)
    return b.build()


def makeFireHydrant():
    """지상식 소화전(0.8m): 빨간 몸통, 둥근 머리(오각 너트), 양옆·앞 배출구 캡(육각 너트), 플랜지·받침 볼트."""
    b = MeshBuilder("City_FireHydrant", "City", bevel=0.006)
    b.cylinder(0.2, 0.2, 0.06, (0, 0, 0.03), "Red", segments=16)
    b.cylinder(0.14, 0.14, 0.58, (0, 0, 0.35), "Red", segments=16)
    b.cylinder(0.17, 0.17, 0.05, (0, 0, 0.64), "Red", segments=16)
    b.sphere(0.14, (0, 0, 0.68), "Red", segments=14, scale=(1, 1, 0.8))
    b.cylinder(0.03, 0.03, 0.06, (0, 0, 0.8), "Steel", segments=5)
    for side in (-1, 1):
        b.cylinder(0.06, 0.06, 0.12, (side * 0.18, 0, 0.45), "Red", segments=12, rot=rotY(90))
        b.cylinder(0.065, 0.065, 0.03, (side * 0.245, 0, 0.45), "Steel", segments=12, rot=rotY(90))
        b.bolts("+X" if side > 0 else "-X", (side * 0.26, 0, 0), [(0, 0.45)], "Steel", radius=0.022, depth=0.016)
    b.cylinder(0.07, 0.07, 0.12, (0, -0.18, 0.4), "Red", segments=12, rot=rotX(90))
    b.cylinder(0.075, 0.075, 0.03, (0, -0.245, 0.4), "Steel", segments=12, rot=rotX(90))
    b.bolts("-Y", (0, -0.26, 0), [(0, 0.4)], "Steel", radius=0.024, depth=0.016)
    ring = [(0.155 * math.cos(math.radians(a)), 0.155 * math.sin(math.radians(a))) for a in range(0, 360, 60)]
    b.bolts("+Z", (0, 0, 0.665), ring, "Steel", radius=0.012, depth=0.01)
    ring = [(0.18 * math.cos(math.radians(a + 30)), 0.18 * math.sin(math.radians(a + 30))) for a in range(0, 360, 60)]
    b.bolts("+Z", (0, 0, 0.06), ring, "Steel", radius=0.013, depth=0.012)
    return b.build()


def makeMailbox():
    """우체통(1.3m): 빨간 몸통, 둥근 지붕, 앞 투입구(테두리·덮개), 안내판 글씨, 아래 수거문(열쇠), 짧은 다리(받침판)."""
    b = MeshBuilder("City_Mailbox", "City", bevel=0.01)
    b.box((0.5, 0.42, 0.9), (0, 0, 0.62), "Red")
    b.cylinder(0.25, 0.25, 0.42, (0, 0, 1.07), "Red", segments=18, rot=rotX(90))
    b.box((0.3, 0.02, 0.05), (0, -0.215, 0.95), "Black")
    b.box((0.22, 0.015, 0.14), (0, -0.215, 0.7), "White")
    for x in (-0.18, 0.18):
        b.box((0.06, 0.32, 0.17), (x, 0, 0.085), "PoleDark")
    front = (0, -0.21, 0)
    b.frame("-Y", front, 0.36, 0.1, 0.02, 0.012, "Steel", v=0.95)
    with b.noBevel():
        b.box((0.32, 0.035, 0.012), (0, -0.235, 0.985), "Steel", rot=rotX(-25))
    b.textBars("-Y", (0, -0.2225, 0), 0.16, 0.014, (1.0, 0.7, 0.85), "Black", v=0.74)
    b.frame("-Y", front, 0.42, 0.32, 0.012, 0.004, "PoleDark", v=0.36)
    b.bolts("-Y", front, [(0.15, 0.36)], "Steel", radius=0.014, depth=0.006)
    with b.noBevel():
        b.cylinder(0.07, 0.07, 0.006, (0, -0.212, 1.12), "White", segments=12, rot=rotX(90))
    return b.build()


def makeStreetBin():
    """거리 휴지통(1.0m): 짙은 녹색 원통(세로 줄무늬·가운데 띠), 위 덮개와 투입구 2개(테두리), 라벨 판, 받침."""
    b = MeshBuilder("City_StreetBin", "City", bevel=0.006)
    b.cylinder(0.26, 0.24, 0.85, (0, 0, 0.45), "Green", segments=18)
    b.cylinder(0.28, 0.28, 0.08, (0, 0, 0.9), "PoleDark", segments=18)
    b.sphere(0.27, (0, 0, 0.94), "PoleDark", segments=16, scale=(1, 1, 0.25))
    for y in (-0.22, 0.22):
        b.box((0.22, 0.06, 0.1), (0, y, 0.82), "Black")
        b.frame("-Y" if y < 0 else "+Y", (0, y * 1.2, 0), 0.26, 0.14, 0.02, 0.008, "Steel", v=0.82)
    b.cylinder(0.27, 0.27, 0.04, (0, 0, 0.02), "PoleDark", segments=18)
    with b.noBevel():
        b.cylinder(0.257, 0.257, 0.05, (0, 0, 0.3), "PoleDark", segments=18)
        for k in range(12):
            a = math.radians(k * 30 + 15)
            b.box((0.02, 0.01, 0.6), (0.252 * math.cos(a), 0.252 * math.sin(a), 0.38), "PoleDark", rot=rotZ(k * 30 + 15 + 90))
    b.plate("-Y", (0, -0.255, 0), 0.12, 0.1, 0.006, "White", v=0.55)
    b.textBars("-Y", (0, -0.261, 0), 0.08, 0.012, (1.0, 0.6), "Green", v=0.57)
    return b.build()


def makeParkBench():
    """공원 벤치(1.6m): 나무 좌판 3장 + 등받이 2장(+Y), 주철 다리·팔걸이, 판 고정 볼트."""
    b = MeshBuilder("City_ParkBench", "City", bevel=0.006)
    backRot = rotX(-10)
    for y in (-0.16, 0.0, 0.16):
        b.box((1.6, 0.13, 0.04), (0, y, 0.45), "Wood")
        b.bolts("+Z", (0, y, 0.47), [(-0.68, 0), (0.68, 0)], "Steel", radius=0.008, depth=0.004)
    for z in (0.62, 0.78):
        b.box((1.6, 0.03, 0.11), (0, 0.27, z), "Wood", rot=backRot)
        b.bolts(backRot, surfaceOf((0, 0.27, z), backRot, 0.015), [(-0.68, 0), (0.68, 0)], "Steel", radius=0.008, depth=0.004)
    for x in (-0.68, 0.68):
        b.box((0.05, 0.5, 0.05), (x, 0.02, 0.42), "PoleDark")
        b.box((0.05, 0.05, 0.42), (x, -0.2, 0.21), "PoleDark")
        b.box((0.05, 0.05, 0.85), (x, 0.24, 0.425), "PoleDark")
        b.box((0.05, 0.4, 0.04), (x, 0.0, 0.65), "PoleDark")
        with b.noBevel():
            b.box((0.07, 0.42, 0.03), (x * 1.04, -0.01, 0.675), "WoodDark")
            b.box((0.05, 0.05, 0.2), (x, -0.2, 0.55), "PoleDark")
            b.box((0.1, 0.1, 0.02), (x, -0.2, 0.01), "PoleDark")
            b.box((0.1, 0.1, 0.02), (x, 0.24, 0.01), "PoleDark")
    return b.build()


def makeBusStop():
    """버스 정류장(3.6 x 1.5 x 2.6): 뒤(+Y) 유리벽(문살·아래 띠), 옆 광고판(틀), 지붕(앞 간판 띠·물받이), 긴 의자, 노선 안내판(글씨)."""
    b = MeshBuilder("City_BusStop", "City", bevel=0.008)
    for x in (-1.75, 1.75):
        b.box((0.08, 0.08, 2.5), (x, 0.65, 1.25), "PoleGray")
        b.box((0.08, 0.08, 2.5), (x, -0.6, 1.25), "PoleGray")
        b.bolts("+Z", (x, 0, 0), [(0, -0.6), (0, 0.65)], "PoleDark", radius=0.05, depth=0.02)
    b.box((3.6, 1.5, 0.08), (0, 0.05, 2.56), "PoleDark")
    b.box((3.4, 0.03, 1.8), (0, 0.68, 1.3), "Glass")
    b.box((0.04, 1.2, 1.9), (1.75, 0.03, 1.25), "PoleDark")
    b.box((0.05, 1.1, 1.6), (1.75, 0.03, 1.25), "GlowWhite")
    b.box((2.4, 0.35, 0.05), (0, 0.45, 0.45), "Steel")
    for x in (-1.4, 0.6):
        b.box((0.05, 0.3, 0.43), (x, 0.45, 0.215), "PoleDark")
    b.box((1.0, 0.04, 0.4), (-1.0, 0.65, 2.1), "Blue")
    with b.noBevel():
        for x in (-0.6, 0.6):
            b.box((0.05, 0.05, 1.8), (x, 0.68, 1.3), "PoleGray")
        b.box((3.4, 0.05, 0.05), (0, 0.68, 0.42), "PoleGray")
        b.box((3.4, 0.05, 0.05), (0, 0.68, 2.2), "PoleGray")
        b.box((3.6, 0.04, 0.16), (0, -0.7, 2.5), "Blue")
        b.box((0.04, 1.5, 0.16), (-1.8, 0.05, 2.5), "Blue")
        b.box((0.04, 1.5, 0.16), (1.8, 0.05, 2.5), "Blue")
    b.textBars("-Y", (0, -0.72, 0), 0.8, 0.05, (1.0,), "White", u=-1.0, v=2.5)
    b.plate("-Y", (0, -0.72, 0), 0.22, 0.12, 0.004, "White", u=1.3, v=2.5)
    b.textBars("-Y", (0, 0.63, 0), 0.8, 0.025, (1.0, 0.6, 0.8, 0.5), "White", u=-1.0, v=2.22)
    b.frame("-X", (1.73, 0, 0), 1.2, 1.9, 0.05, 0.012, "Steel", v=1.25, u=-0.03)
    b.frame("+X", (1.77, 0, 0), 1.2, 1.9, 0.05, 0.012, "Steel", v=1.25, u=0.03)
    return b.build()


def makeNewsBox():
    """신문 가판함(1.1m): 파란 상자, 앞 유리창(테두리), 손잡이, 동전 투입 장치, 위 비스듬한 지붕(이름판), 받침(볼트)."""
    b = MeshBuilder("City_NewsBox", "City", bevel=0.008)
    b.box((0.5, 0.42, 0.75), (0, 0, 0.62), "Blue")
    b.box((0.4, 0.02, 0.32), (0, -0.215, 0.75), "Glass")
    b.box((0.42, 0.02, 0.05), (0, -0.215, 0.5), "Steel")
    b.box((0.54, 0.46, 0.04), (0, 0, 1.02), "Blue", rot=rotX(-6))
    b.box((0.4, 0.4, 0.25), (0, 0, 0.125), "PoleDark")
    front = (0, -0.225, 0)
    b.frame("-Y", front, 0.43, 0.35, 0.02, 0.008, "Steel", v=0.75)
    b.textBars("-Y", (0, -0.225, 0), 0.36, 0.022, (1.0,), "White", v=0.95)
    with b.noBevel():
        b.box((0.12, 0.08, 0.14), (0.14, -0.24, 0.47), "Steel")
        b.box((0.012, 0.01, 0.04), (0.14, -0.282, 0.5), "Black")
        b.box((0.06, 0.01, 0.02), (0.14, -0.282, 0.43), "Black")
    b.bolts("+Z", (0, 0, 0.25), [(-0.15, -0.15), (0.15, -0.15)], "Steel", radius=0.012)
    return b.build()


def makeBollard():
    """볼라드(0.9m): 회색 기둥 + 노란 반사띠 2줄 + 둥근 머리(테두리) + 바닥 고정 링."""
    b = MeshBuilder("City_Bollard", "City", bevel=0.004)
    b.cylinder(0.1, 0.09, 0.8, (0, 0, 0.4), "PoleGray", segments=14)
    b.cylinder(0.102, 0.102, 0.06, (0, 0, 0.66), "Yellow", segments=14)
    b.sphere(0.09, (0, 0, 0.8), "PoleGray", segments=12, scale=(1, 1, 0.8))
    with b.noBevel():
        b.cylinder(0.1, 0.1, 0.025, (0, 0, 0.56), "Yellow", segments=14)
        b.cylinder(0.096, 0.096, 0.02, (0, 0, 0.79), "PoleDark", segments=14)
        b.cylinder(0.11, 0.11, 0.03, (0, 0, 0.015), "PoleDark", segments=14)
    return b.build()


def makePlanterBox():
    """화단 상자(1.2 x 0.6, 높이 1.1): 콘크리트 상자(위 갓돌·아래 홈) + 흙 + 낮은 관목 덩어리 + 작은 꽃."""
    b = MeshBuilder("City_PlanterBox", "City", bevel=0.01)
    b.box((1.2, 0.6, 0.5), (0, 0, 0.25), "Concrete")
    b.box((1.1, 0.5, 0.02), (0, 0, 0.49), "Soil")
    for x, r in ((-0.35, 0.3), (0.05, 0.34), (0.38, 0.27)):
        b.sphere(r, (x, 0, 0.5 + r * 0.9), "Plant", segments=12, scale=(1, 0.85, 1))
    with b.noBevel():
        for facing, center, width in (("-Y", (0, -0.3, 0), 1.2), ("+Y", (0, 0.3, 0), 1.2),
                                      ("-X", (-0.6, 0, 0), 0.6), ("+X", (0.6, 0, 0), 0.6)):
            b.plate(facing, center, width + 0.04, 0.05, 0.02, "Concrete", v=0.475)
            b.plate(facing, center, width, 0.012, 0.002, "PoleDark", v=0.08)
        for x, y, z, color in ((-0.55, -0.2, 0.56, "Red"), (-0.1, -0.25, 0.54, "Yellow"), (0.25, -0.22, 0.55, "Red"),
                               (0.52, 0.18, 0.56, "Yellow"), (-0.5, 0.2, 0.55, "White"), (0.12, 0.24, 0.55, "White")):
            b.sphere(0.04, (x, y, z), color, segments=6)
    return b.build()


# ---------------------------------------------------------------- 뒷골목·설비

def makeDumpster():
    """대형 쓰레기 수거함(1.9 x 1.1 x 1.3): 녹색 철통(세로 보강대), 비스듬한 뚜껑 2장(경첩), 옆 손잡이·지게차 구멍, 경고 띠 글씨, 바퀴."""
    b = MeshBuilder("City_Dumpster", "City", bevel=0.01)
    b.box((1.8, 1.0, 1.05), (0, 0, 0.66), "Green")
    b.box((1.86, 1.06, 0.06), (0, 0, 1.18), "PoleDark")
    for x in (-0.46, 0.46):
        b.box((0.9, 1.08, 0.04), (x, 0, 1.24), "Black", rot=rotX(-4))
        b.handle("+Z", (x, -0.45, 1.27), 0.3, "PoleDark", vertical=False, standoff=0.02, thickness=0.02)
    for side in (-1, 1):
        b.box((0.06, 0.3, 0.06), (side * 0.93, 0, 0.95), "PoleDark")
    for sx in (-0.75, 0.75):
        for sy in (-0.38, 0.38):
            b.cylinder(0.07, 0.07, 0.05, (sx, sy, 0.07), "Rubber", segments=10, rot=rotX(90))
            with b.noBevel():
                b.box((0.1, 0.12, 0.04), (sx, sy, 0.15), "PoleDark")
    b.box((1.6, 0.02, 0.14), (0, -0.505, 0.85), "Yellow")
    b.textBars("-Y", (0, -0.515, 0), 0.6, 0.03, (1.0,), "Black", v=0.85)
    with b.noBevel():
        for facing, y in (("-Y", -0.5), ("+Y", 0.5)):
            for x in (-0.6, -0.2, 0.2, 0.6):
                if facing == "-Y":
                    b.plate(facing, (0, y, 0), 0.06, 0.6, 0.025, "Green", u=x, v=0.45)
                else:
                    b.plate(facing, (0, y, 0), 0.06, 0.9, 0.025, "Green", u=x, v=0.62)
        for side in (-1, 1):
            b.box((0.02, 0.24, 0.12), (side * 0.905, 0, 0.35), "Black")
    with b.noBevel():
        for x in (-0.75, -0.2, 0.2, 0.75):
            b.cylinder(0.018, 0.018, 0.1, (x, 0.53, 1.2), "PoleDark", segments=8, rot=rotY(90))
    return b.build()


def makeTrashBags():
    """검은 쓰레기봉투 더미(1.0 x 0.8 x 0.6): 크기가 다른 봉투 4개, 묶은 매듭과 흰 끈."""
    b = MeshBuilder("City_TrashBags", "City", bevel=0.0)
    for x, y, r, h in ((-0.25, 0.05, 0.28, 0.85), (0.22, -0.1, 0.3, 0.8), (0.05, 0.22, 0.24, 0.9), (0.0, -0.05, 0.22, 1.25)):
        z = r * h if h < 1.0 else 0.42
        b.sphere(r, (x, y, z), "TrashBag", segments=12, scale=(1, 0.9, h))
        b.cylinder(0.04, 0.02, 0.1, (x, y, z + r * h + 0.03), "TrashBag", segments=8)
        b.cylinder(0.025, 0.025, 0.015, (x, y, z + r * h + 0.02), "White", segments=6)
        b.box((0.08, 0.012, 0.004), (x + 0.04, y, z + r * h + 0.08), "TrashBag", rot=rotY(-30))
    return b.build()


def makeACUnit():
    """에어컨 실외기(0.85 x 0.35 x 0.65): 흰 상자, 앞 원형 팬(링 그릴), 옆·위 환기 줄, 오른쪽 배관 연결부, 명판, 받침 다리(볼트)."""
    b = MeshBuilder("City_ACUnit", "City", bevel=0.008)
    b.box((0.85, 0.32, 0.58), (0, 0, 0.36), "White")
    b.cylinder(0.22, 0.22, 0.02, (-0.12, -0.165, 0.36), "Gray", segments=22, rot=rotX(90))
    b.cylinder(0.19, 0.19, 0.025, (-0.12, -0.17, 0.36), "Black", segments=22, rot=rotX(90))
    for i in range(3):
        b.box((0.36, 0.01, 0.02), (-0.12, -0.185, 0.36), "Gray", rot=rotY(i * 60))
    for z in (0.18, 0.26, 0.34, 0.42, 0.5):
        b.box((0.18, 0.01, 0.02), (0.28, -0.165, z), "Gray")
    for x in (-0.36, 0.36):
        b.box((0.05, 0.35, 0.07), (x, 0, 0.035), "PoleDark")
        b.bolts("+Z", (x, 0, 0.07), [(0, -0.13), (0, 0.13)], "Steel", radius=0.01, depth=0.006)
    with b.noBevel():
        b.hollowCylinder(0.13, 0.13, 0.01, 0.008, (-0.12, -0.19, 0.36), "Gray", segments=16, rot=rotX(90))
        b.cylinder(0.03, 0.03, 0.015, (-0.12, -0.192, 0.36), "Gray", segments=8, rot=rotX(90))
        for z in (0.32, 0.4):
            b.cylinder(0.018, 0.018, 0.08, (0.465, 0.06, z), "Steel", segments=8, rot=rotY(90))
            b.cylinder(0.026, 0.026, 0.02, (0.44, 0.06, z), "Steel", segments=6, rot=rotY(90))
    b.louver("-X", (-0.425, 0, 0), 0.22, 0.36, 6, "Gray", depth=0.006, v=0.38)
    b.louver("+Z", (0, 0, 0.65), 0.6, 0.2, 5, "Gray", depth=0.004, u=0.0)
    b.plate("-Y", (0, -0.16, 0), 0.12, 0.05, 0.003, "Gray", u=0.28, v=0.6)
    return b.build()


def makeUtilityBox():
    """한전 변압기함(0.9 x 0.5 x 1.4): 회녹색 함, 앞문 2짝(테두리·손잡이·경첩·아래 환기구), 경고 표지, 명판, 콘크리트 받침(볼트)."""
    b = MeshBuilder("City_UtilityBox", "City", bevel=0.008)
    b.box((1.0, 0.6, 0.12), (0, 0, 0.06), "Concrete")
    b.box((0.9, 0.5, 1.22), (0, 0, 0.73), "Gray")
    b.box((0.94, 0.54, 0.05), (0, 0, 1.365), "PoleGray")
    b.box((0.002, 0.01, 1.1), (0, -0.255, 0.73), "PoleDark")
    for x in (-0.2, 0.2):
        b.box((0.03, 0.03, 0.12), (x + (0.15 if x < 0 else -0.15), -0.265, 0.75), "PoleDark")
    b.box((0.16, 0.01, 0.16), (-0.2, -0.26, 1.05), "Yellow", rot=rotY(45))
    b.box((0.3, 0.01, 0.08), (0.2, -0.26, 1.05), "White")
    front = (0, -0.25, 0)
    for x in (-0.22, 0.22):
        b.frame("-Y", front, 0.42, 1.1, 0.015, 0.005, "PoleGray", u=x, v=0.73)
        b.louver("-Y", front, 0.26, 0.12, 4, "PoleGray", depth=0.01, u=x, v=0.3)
    b.hinges("-Y", front, -0.44, (0.35, 1.1), "PoleDark", radius=0.01, length=0.07)
    b.hinges("-Y", front, 0.44, (0.35, 1.1), "PoleDark", radius=0.01, length=0.07)
    with b.noBevel():
        b.box((0.02, 0.012, 0.08), (-0.2, -0.267, 1.05), "Black", rot=rotY(-20))
    b.textBars("-Y", (0, -0.265, 0), 0.22, 0.014, (1.0, 0.6), "Black", u=0.2, v=1.06)
    b.bolts("+Z", (0, 0, 0.12), [(-0.45, -0.25), (0.45, -0.25), (-0.45, 0.25), (0.45, 0.25)], "Steel", radius=0.014)
    return b.build()


def makeBicycle():
    """자전거(1.7m, X 방향): 살 바퀴 2개, 마름모 뼈대(체인·시트 스테이), 포크, 안장·시트포스트, 핸들·손잡이, 체인링·크랭크·페달, 뒤 짐받이, 킥스탠드."""
    b = MeshBuilder("City_Bicycle", "City", bevel=0.0)
    for x in (-0.55, 0.55):
        spokedWheel(b, x, 0.34, 0.34, 0.04)
    rearHub, frontHub = (-0.55, 0, 0.34), (0.55, 0, 0.34)
    bottomBracket = (-0.05, 0, 0.3)
    seatTop = (-0.2, 0, 0.78)
    headTop, headBottom = (0.36, 0, 0.8), (0.41, 0, 0.62)
    b.tube(bottomBracket, seatTop, 0.018, "Blue")
    b.tube(seatTop, headTop, 0.018, "Blue")
    b.tube(bottomBracket, headBottom, 0.02, "Blue")
    b.tube(headBottom, headTop, 0.024, "Blue")
    for y in (-0.03, 0.03):
        b.tube((bottomBracket[0], y, bottomBracket[2]), (rearHub[0], y, rearHub[2]), 0.012, "Blue")
        b.tube((seatTop[0], y, seatTop[2] - 0.02), (rearHub[0], y, rearHub[2]), 0.012, "Blue")
        b.tube((headBottom[0], y, headBottom[2]), (frontHub[0], y, frontHub[2]), 0.013, "PoleDark")
    b.tube(seatTop, (-0.23, 0, 0.88), 0.013, "Steel")
    b.box((0.24, 0.1, 0.05), (-0.22, 0, 0.9), "Black")
    b.tube(headTop, (0.38, 0, 0.92), 0.015, "PoleDark")
    b.cylinder(0.015, 0.015, 0.5, (0.42, 0, 0.92), "PoleDark", segments=8, rot=rotX(90))
    b.tube((0.38, 0, 0.92), (0.42, 0, 0.92), 0.015, "PoleDark")
    for y in (-0.2, 0.2):
        b.cylinder(0.02, 0.02, 0.1, (0.42, y, 0.92), "Rubber", segments=8, rot=rotX(90))
    b.cylinder(0.1, 0.1, 0.008, (bottomBracket[0], 0.05, bottomBracket[2]), "PoleDark", segments=14, rot=rotX(90))
    for side, angle in ((1, 30), (-1, 210)):
        dx, dz = 0.14 * math.cos(math.radians(angle)), 0.14 * math.sin(math.radians(angle))
        end = (bottomBracket[0] + dx, side * 0.07, bottomBracket[2] + dz)
        b.tube((bottomBracket[0], side * 0.06, bottomBracket[2]), end, 0.01, "Steel")
        b.box((0.08, 0.06, 0.015), (end[0], side * 0.11, end[2]), "Black")
    b.box((0.36, 0.12, 0.015), (-0.6, 0, 0.72), "PoleDark")
    for y in (-0.05, 0.05):
        b.tube((-0.75, y, 0.72), (rearHub[0] - 0.05, y, rearHub[2] + 0.02), 0.008, "PoleDark")
    b.tube((-0.12, 0.05, 0.3), (-0.3, 0.14, 0.01), 0.008, "PoleDark")
    return b.build()


def makeScooter():
    """배달 오토바이(1.8m, X 방향): 바퀴 2개(허브), 차체·흙받이, 발판 고무, 안장, 앞 방패·조향축, 핸들(손잡이·거울), 전조등·후미등·번호판, 배기관, 뒤 배달 상자(걸쇠·로고)."""
    b = MeshBuilder("City_Scooter", "City", bevel=0.01)
    for x in (-0.62, 0.62):
        wheel(b, x, 0, 0.24, 0.24, 0.12, segments=18)
    b.box((0.6, 0.36, 0.08), (0.05, 0, 0.32), "PoleDark")
    b.box((0.7, 0.4, 0.3), (-0.4, 0, 0.55), "Red")
    b.box((0.6, 0.32, 0.08), (-0.35, 0, 0.74), "Black")
    b.box((0.2, 0.36, 0.55), (0.55, 0, 0.6), "Red", rot=rotY(-15))
    b.cylinder(0.02, 0.02, 0.6, (0.62, 0, 1.02), "PoleDark", segments=8, rot=rotX(90))
    b.box((0.06, 0.12, 0.1), (0.7, 0, 0.9), "GlowWhite")
    b.box((0.42, 0.42, 0.36), (-0.6, 0, 0.98), "White")
    b.box((0.43, 0.43, 0.06), (-0.6, 0, 1.08), "Red")
    with b.noBevel():
        b.plate("+Z", (0.05, 0, 0.36), 0.5, 0.3, 0.008, "Rubber")
        b.tube((0.6, 0, 0.82), (0.62, 0, 1.02), 0.025, "PoleDark")
        for y in (-0.06, 0.06):
            b.tube((0.62, y, 0.24), (0.6, y, 0.7), 0.018, "Steel")
        b.box((0.36, 0.16, 0.03), (0.62, 0, 0.51), "Red", rot=rotY(-5))
        b.box((0.3, 0.2, 0.03), (-0.62, 0, 0.48), "PoleDark", rot=rotY(10))
        for y in (-0.26, 0.26):
            b.cylinder(0.022, 0.022, 0.09, (0.62, y, 1.02), "Rubber", segments=8, rot=rotX(90))
            b.tube((0.6, y * 0.85, 1.03), (0.56, y * 1.05, 1.2), 0.008, "PoleDark")
            b.box((0.02, 0.1, 0.06), (0.56, y * 1.05, 1.22), "Black")
        b.box((0.04, 0.16, 0.06), (-0.76, 0, 0.62), "GlowRed")
        b.box((0.015, 0.18, 0.1), (-0.79, 0, 0.44), "White")
        b.tube((-0.05, 0.19, 0.28), (-0.55, 0.19, 0.36), 0.025, "Steel")
        b.cylinder(0.045, 0.045, 0.22, (-0.6, 0.19, 0.37), "Steel", segments=10, rot=rotY(80))
        b.box((0.04, 0.03, 0.08), (-0.6, -0.225, 0.98), "Steel")
    b.plate("-Y", (-0.6, -0.21, 0.92), 0.2, 0.12, 0.004, "Red")
    b.textBars("-Y", (-0.6, -0.214, 0.92), 0.14, 0.02, (1.0,), "White", v=0.0)
    return b.build()


def makeSedan():
    """승용차(4.4 x 1.8 x 1.45, X 방향): 하부 차체(문 이음선·손잡이·휠 아치), 위 유리 객실(B 기둥·아래 띠), 거울, 그릴·엠블럼, 번호판, 배기구, 바퀴 4개(너트), 앞(+X) 전조등·뒤 후미등."""
    b = MeshBuilder("City_Sedan", "City", bevel=0.03)
    b.box((4.4, 1.8, 0.62), (0, 0, 0.55), "CarPaint")
    b.box((2.3, 1.62, 0.5), (-0.2, 0, 1.1), "Glass")
    b.box((2.1, 1.64, 0.06), (-0.2, 0, 1.38), "CarPaint")
    for x in (-0.95, 0.55):
        b.box((0.08, 1.63, 0.46), (x, 0, 1.1), "CarPaint")
    for x in (-1.4, 1.4):
        for y in (-0.82, 0.82):
            wheel(b, x, y, 0.33, 0.33, 0.22, segments=18, lugs=5)
    b.box((4.46, 1.84, 0.12), (0, 0, 0.3), "PoleDark")
    for y in (-0.6, 0.6):
        b.box((0.04, 0.32, 0.12), (2.21, y, 0.7), "GlowWhite")
        b.box((0.04, 0.32, 0.12), (-2.21, y, 0.72), "GlowRed")
    b.box((0.04, 0.5, 0.12), (2.21, 0, 0.52), "Black")
    with b.noBevel():
        b.box((0.07, 1.63, 0.46), (-0.2, 0, 1.1), "CarPaint")
        b.box((2.32, 1.64, 0.03), (-0.2, 0, 0.865), "Black")
        for side in (-1, 1):
            facing = "-Y" if side < 0 else "+Y"
            mirror = 1 if side < 0 else -1
            surface = (0, side * 0.9, 0)
            for x in (0.8, -0.25, -1.25):
                b.plate(facing, surface, 0.008, 0.46, 0.003, "PoleDark", u=mirror * x, v=0.6)
            for x in (0.3, -0.75):
                b.plate(facing, surface, 0.14, 0.03, 0.012, "Steel", u=mirror * x, v=0.78)
            for x in (-1.4, 1.4):
                b.plate(facing, surface, 0.82, 0.07, 0.015, "PoleDark", u=mirror * x, v=0.69)
            b.box((0.14, 0.1, 0.1), (0.75, side * 0.94, 0.95), "CarPaint")
            b.box((0.02, 0.09, 0.08), (0.68, side * 0.945, 0.95), "Black")
            b.box((0.06, 0.06, 0.04), (2.21, side * 0.6, 0.76), "Steel")
        for z in (0.49, 0.55):
            b.box((0.02, 0.46, 0.015), (2.232, 0, z), "Steel")
        b.cylinder(0.04, 0.04, 0.01, (2.235, 0, 0.62), "Steel", segments=10, rot=rotY(90))
        b.box((0.015, 0.5, 0.11), (2.235, 0, 0.41), "White")
        b.box((0.015, 0.5, 0.11), (-2.235, 0, 0.5), "White")
        b.plate("+Z", (1.98, 0, 0.86), 0.008, 1.7, 0.002, "PoleDark")
        b.plate("+Z", (-1.85, 0, 0.86), 0.008, 1.7, 0.002, "PoleDark")
        b.cylinder(0.035, 0.035, 0.06, (-2.23, 0.5, 0.3), "Steel", segments=8, rot=rotY(90))
    return b.build()


# ---------------------------------------------------------------- 카페·가게 앞

def makeCafeTable():
    """둥근 카페 탁자(0.7m): 상판(아래 테) + 기둥(위아래 칼라) + 십자 받침(고무 발), 위 메뉴 꽂이·양념통."""
    b = MeshBuilder("City_CafeTable", "City", bevel=0.004)
    b.cylinder(0.35, 0.35, 0.03, (0, 0, 0.735), "WoodDark", segments=22)
    b.cylinder(0.035, 0.035, 0.7, (0, 0, 0.37), "PoleDark", segments=10)
    b.box((0.55, 0.06, 0.03), (0, 0, 0.015), "PoleDark")
    b.box((0.06, 0.55, 0.03), (0, 0, 0.015), "PoleDark")
    with b.noBevel():
        b.cylinder(0.33, 0.33, 0.02, (0, 0, 0.71), "PoleDark", segments=22)
        b.cylinder(0.05, 0.05, 0.05, (0, 0, 0.055), "PoleDark", segments=10)
        b.cylinder(0.08, 0.08, 0.02, (0, 0, 0.69), "PoleDark", segments=10)
        for x, y in ((0.27, 0), (-0.27, 0), (0, 0.27), (0, -0.27)):
            b.box((0.05, 0.05, 0.01), (x, y, 0.005), "Rubber")
        for side in (-1, 1):
            b.box((0.1, 0.004, 0.14), (0.08, side * 0.015, 0.815), "White", rot=rotX(side * 12))
        b.cylinder(0.018, 0.018, 0.07, (-0.08, -0.04, 0.785), "White", segments=8)
        b.cylinder(0.018, 0.018, 0.07, (-0.08, 0.02, 0.785), "Black", segments=8)
    return b.build()


def makeCafeChair():
    """카페 의자: 금속 다리 4개(가로대·고무 발), 나무 좌판(판 이음 줄), 등받이 판 2장(+Y)."""
    b = MeshBuilder("City_CafeChair", "City", bevel=0.004)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.cylinder(0.015, 0.015, 0.45, (sx * 0.18, sy * 0.18, 0.225), "PoleDark", segments=8)
    b.box((0.44, 0.44, 0.03), (0, 0, 0.46), "Wood")
    for sx in (-1, 1):
        b.cylinder(0.015, 0.015, 0.4, (sx * 0.18, 0.2, 0.66), "PoleDark", segments=8)
    b.box((0.42, 0.03, 0.14), (0, 0.21, 0.78), "Wood")
    with b.noBevel():
        b.box((0.42, 0.03, 0.08), (0, 0.21, 0.62), "Wood")
        for v in (-0.11, 0.0, 0.11):
            b.plate("+Z", (0, 0, 0.475), 0.44, 0.008, 0.002, "WoodDark", v=v)
        for sx in (-1, 1):
            b.tube((sx * 0.18, -0.18, 0.15), (sx * 0.18, 0.18, 0.15), 0.01, "PoleDark", segments=6)
            for sy in (-1, 1):
                b.cylinder(0.018, 0.018, 0.012, (sx * 0.18, sy * 0.18, 0.006), "Rubber", segments=6)
    return b.build()


def makeParasol():
    """파라솔(지름 2.2, 높이 2.4): 무거운 받침(볼트) + 기둥(높이 조절 손잡이) + 8각 천 덮개(살 8개·꼭대기 장식)."""
    b = MeshBuilder("City_Parasol", "City", bevel=0.0)
    b.cylinder(0.25, 0.25, 0.08, (0, 0, 0.04), "Concrete", segments=16)
    b.cylinder(0.025, 0.025, 2.35, (0, 0, 1.2), "White", segments=8)
    b.cylinder(1.1, 0.05, 0.4, (0, 0, 2.15), "Canvas", segments=8)
    b.cylinder(1.1, 1.1, 0.12, (0, 0, 1.92), "Canvas", segments=8)
    for k in range(8):
        a = 2 * math.pi * (k + 0.5) / 8
        b.tube((0, 0, 1.6), (1.0 * math.cos(a), 1.0 * math.sin(a), 1.88), 0.008, "White", segments=4)
    b.cylinder(0.04, 0.04, 0.1, (0, 0, 1.62), "White", segments=8)
    b.sphere(0.04, (0, 0, 2.38), "White", segments=8)
    b.cylinder(0.04, 0.04, 0.12, (0, 0, 0.12), "PoleGray", segments=8)
    b.box((0.12, 0.02, 0.02), (0.06, 0, 1.2), "PoleGray")
    b.bolts("+Z", (0, 0, 0.08), [(0.15, 0), (-0.15, 0), (0, 0.15), (0, -0.15)], "PoleGray", radius=0.02, depth=0.01)
    return b.build()


def makeSignBoard():
    """입간판(A형, 0.55 x 0.5 x 0.95): 검은 칠판 두 장(분필 메뉴 글씨) + 나무 테두리, 위 손잡이 구멍, 경첩."""
    b = MeshBuilder("City_SignBoard", "City", bevel=0.004)
    for side in (-1, 1):
        panelRot = rotX(side * 13)
        b.box((0.55, 0.03, 0.92), (0, side * 0.12, 0.46), "WoodDark", rot=panelRot)
        b.box((0.45, 0.035, 0.6), (0, side * 0.125, 0.52), "Black", rot=panelRot)
        b.box((0.3, 0.036, 0.04), (0, side * 0.127, 0.62), "White", rot=panelRot)
        outward = panelRot @ (rotZ(180) if side > 0 else rotX(0))
        board = surfaceOf((0, side * 0.125, 0.52), outward, 0.0175)
        b.textBars(outward, board, 0.34, 0.016, (0.8, 1.0, 0.6, 0.9, 0.7), "White", v=-0.04, gap=2.2)
        b.plate(outward, surfaceOf((0, side * 0.12, 0.46), outward, 0.015), 0.12, 0.04, 0.004, "Black", v=0.4)
    with b.noBevel():
        for x in (-0.2, 0.2):
            b.cylinder(0.01, 0.01, 0.06, (x, 0, 0.9), "Steel", segments=6, rot=rotY(90))
    return b.build()


def makePhoneBooth():
    """공중전화 부스(1.0 x 1.0 x 2.3): 틀 + 유리 3면(가운데 가로살), 앞(-Y)은 열림, 안에 전화기(키패드·수화기·선반), 위 표시판 글씨."""
    b = MeshBuilder("City_PhoneBooth", "City", bevel=0.008)
    b.box((1.0, 1.0, 0.08), (0, 0, 0.04), "PoleDark")
    b.box((1.0, 1.0, 0.12), (0, 0, 2.24), "Red")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.06, 0.06, 2.1), (sx * 0.47, sy * 0.47, 1.13), "Red")
    b.box((0.03, 0.88, 1.6), (-0.47, 0, 1.2), "Glass")
    b.box((0.03, 0.88, 1.6), (0.47, 0, 1.2), "Glass")
    b.box((0.88, 0.03, 1.6), (0, 0.47, 1.2), "Glass")
    b.box((0.3, 0.12, 0.42), (0, 0.4, 1.3), "Gray")
    b.box((0.06, 0.08, 0.2), (-0.1, 0.33, 1.38), "Black")
    b.box((0.7, 0.02, 0.1), (0, -0.505, 2.24), "GlowWhite")
    with b.noBevel():
        for x in (-0.47, 0.47):
            b.box((0.05, 0.88, 0.05), (x, 0, 1.0), "Red")
        b.box((0.88, 0.05, 0.05), (0, 0.47, 1.0), "Red")
        b.box((0.44, 0.2, 0.025), (0, 0.36, 0.98), "PoleDark")
        b.box((0.3, 0.16, 0.04), (0.05, 0.36, 1.01), "WoodDark")
        b.box((0.4, 0.04, 0.04), (0, 0.6, 2.18), "Red")
    for row in range(4):
        for col in range(3):
            b.plate("-Y", (0, 0.34, 0), 0.028, 0.022, 0.005, "Steel", u=0.03 + col * 0.04, v=1.38 - row * 0.035)
    b.plate("-Y", (0, 0.34, 0), 0.06, 0.012, 0.004, "Black", u=0.07, v=1.46)
    b.plate("-Y", (0, 0.34, 0), 0.16, 0.05, 0.003, "GlowWhite", u=0.0, v=1.17)
    b.textBars("-Y", (0, -0.515, 0), 0.4, 0.04, (1.0,), "Red", v=2.24)
    return b.build()


# ---------------------------------------------------------------- 공사장

def makeBarricade():
    """공사 차단기(1.6 x 0.5 x 1.05): 주황·흰 줄무늬 가로판 2장, A형 다리(위 연결 쇠·가로대·발판), 경고등(받침), 안내판."""
    b = MeshBuilder("City_Barricade", "City", bevel=0.004)
    for z in (0.55, 0.85):
        b.box((1.6, 0.04, 0.16), (0, -0.05, z), "White")
        for x in (-0.6, -0.2, 0.2, 0.6):
            b.box((0.18, 0.045, 0.16), (x, -0.05, z), "Orange")
    for x in (-0.7, 0.7):
        for side in (-1, 1):
            b.box((0.05, 0.05, 1.0), (x, side * 0.12, 0.48), "PoleDark", rot=rotX(side * 12))
        with b.noBevel():
            b.box((0.07, 0.1, 0.06), (x, 0.0, 0.96), "PoleDark")
            b.box((0.04, 0.4, 0.04), (x, 0.0, 0.2), "PoleDark")
            for side in (-1, 1):
                b.box((0.1, 0.12, 0.02), (x, side * 0.22, 0.01), "Rubber")
    with b.noBevel():
        b.box((0.08, 0.06, 0.06), (0.7, -0.05, 0.94), "PoleDark")
    b.cylinder(0.06, 0.06, 0.1, (0.7, -0.05, 1.0), "GlowAmber", segments=10)
    b.plate("-Y", (0, -0.0725, 0), 0.36, 0.22, 0.004, "Yellow", u=-0.35, v=0.7)
    b.textBars("-Y", (0, -0.0765, 0), 0.28, 0.025, (1.0, 0.6), "Black", u=-0.35, v=0.72)
    return b.build()


def makeWaterBarrier():
    """플라스틱 물통 차단벽(1.4 x 0.45 x 0.8): 사다리꼴 단면, 주황 몸통, 흰 반사띠, 주입구·배수 마개, 끝 연결 고리, 아래 지게차 홈."""
    b = MeshBuilder("City_WaterBarrier", "City", bevel=0.02)
    w, d0, d1, h = 0.7, 0.225, 0.1, 0.8
    pts = [(-w, -d0, 0), (w, -d0, 0), (w, d0, 0), (-w, d0, 0), (-w, -d1, h), (w, -d1, h), (w, d1, h), (-w, d1, h)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    b.polyhedron(pts, faces, "Orange")
    b.box((1.3, 0.02, 0.1), (0, -0.175, 0.45), "White", rot=rotX(-8))
    b.box((1.3, 0.02, 0.1), (0, 0.175, 0.45), "White", rot=rotX(8))
    b.cylinder(0.06, 0.06, 0.04, (0.4, 0, 0.81), "White", segments=10)
    with b.noBevel():
        b.cylinder(0.035, 0.035, 0.02, (0.4, 0, 0.835), "White", segments=6)
        b.cylinder(0.035, 0.035, 0.02, (-0.45, -0.218, 0.08), "Black", segments=8, rot=rotX(90))
        for side in (-1, 1):
            b.box((0.03, 0.08, 0.14), (side * 0.715, -0.04 * side, 0.65), "Orange")
            b.box((0.03, 0.08, 0.14), (side * 0.715, 0.06 * side, 0.2), "Orange")
        for x in (-0.35, 0.35):
            b.box((0.24, 0.452, 0.06), (x, 0, 0.03), "PoleDark")
        b.box((0.9, 0.02, 0.015), (0, -0.168, 0.65), "Orange", rot=rotX(-8))
        b.box((0.9, 0.02, 0.015), (0, -0.19, 0.28), "Orange", rot=rotX(-8))
    return b.build()


def makeConstructionFence():
    """공사장 가림막(2.0 x 0.5 x 2.0): 흰 강판(가로 골) + 위 녹색 띠(회사 글씨), 경고 표지, 뒤 지지대(볼트), 콘크리트 받침. (벽 취급, 변신 안 됨)"""
    b = MeshBuilder("City_ConstructionFence", "City", bevel=0.004)
    b.box((2.0, 0.04, 1.85), (0, 0, 1.03), "White")
    b.box((2.0, 0.045, 0.25), (0, 0, 1.8), "Green")
    for x in (-0.95, 0.95):
        b.box((0.06, 0.06, 2.0), (x, 0.03, 1.0), "PoleGray")
        b.box((0.05, 0.05, 1.0), (x, 0.3, 0.5), "PoleGray", rot=rotX(-30))
        b.box((0.3, 0.5, 0.12), (x, 0.18, 0.06), "Concrete")
        b.bolts("+Z", (x, 0.18, 0.12), [(0, -0.15), (0, 0.15)], "Steel", radius=0.016)
    front = (0, -0.02, 0)
    for v in (0.35, 0.7, 1.05, 1.4):
        b.plate("-Y", front, 1.98, 0.03, 0.006, "White", v=v)
    b.textBars("-Y", (0, -0.0225, 0), 1.1, 0.06, (1.0,), "White", v=1.8)
    b.plate("-Y", (0, -0.026, 0), 0.5, 0.36, 0.004, "Yellow", u=0.5, v=1.0)
    b.textBars("-Y", (0, -0.03, 0), 0.38, 0.04, (1.0, 0.7, 0.85), "Black", u=0.5, v=1.1)
    b.bolts("-Y", front, [(-0.95, 1.6), (0.95, 1.6), (-0.95, 0.4), (0.95, 0.4)], "PoleGray", radius=0.014)
    return b.build()


# ---------------------------------------------------------------- 가게 안

def makeFridgeCase():
    """음료 냉장고(1.2 x 0.75 x 2.0): 흰 몸통, 앞 유리문 2짝(문틀·세로 손잡이), 안 선반 칸, 아래 환기 그릴, 위 발광 간판(글씨)."""
    b = MeshBuilder("City_FridgeCase", "City", bevel=0.008)
    b.box((1.2, 0.75, 1.85), (0, 0, 0.925), "White")
    b.box((1.2, 0.75, 0.15), (0, 0, 1.925), "Blue")
    b.box((1.1, 0.02, 0.1), (0, -0.38, 1.925), "GlowWhite")
    b.box((1.08, 0.66, 1.6), (0, -0.02, 0.95), "Black")
    b.box((1.04, 0.02, 1.56), (0, 0.3, 0.95), "GlowWhite")
    for z in (0.45, 0.85, 1.25):
        b.box((1.08, 0.05, 0.03), (0, -0.37, z), "Gray")
        for k, x in enumerate((-0.4, -0.2, 0.0, 0.2, 0.4)):
            b.cylinder(0.04, 0.04, 0.2, (x, -0.33, z + 0.12), "Red" if k % 2 == 0 else "Green", segments=8)
    b.box((1.12, 0.02, 1.64), (0, -0.39, 0.95), "Glass")
    b.box((0.02, 0.03, 1.64), (0, -0.4, 0.95), "Steel")
    doorFront = (0, -0.4, 0)
    for x in (-0.28, 0.28):
        b.frame("-Y", doorFront, 0.56, 1.64, 0.03, 0.01, "Gray", u=x, v=0.95)
        b.handle("-Y", (0, -0.41, 0), 0.5, "Steel", u=x + (0.22 if x < 0 else -0.22), v=1.05, standoff=0.03, thickness=0.02)
    b.louver("-Y", (0, -0.375, 0), 1.0, 0.08, 3, "Black", depth=0.01, v=0.07)
    b.textBars("-Y", (0, -0.392, 0), 0.5, 0.04, (1.0,), "Blue", v=1.925)
    return b.build()


def makeShopShelf():
    """편의점 진열대(1.5 x 0.6 x 1.5): 금속 뼈대, 선반 4단(앞 가격표 띠·가격표), 상품 상자 줄, 아래 걸레받이, 위 머리 간판."""
    b = MeshBuilder("City_ShopShelf", "City", bevel=0.004)
    b.box((1.5, 0.04, 1.5), (0, 0, 0.75), "Gray")
    for x in (-0.73, 0.73):
        b.box((0.04, 0.6, 1.5), (x, 0, 0.75), "Gray")
    colors = ("Red", "Yellow", "Blue", "Green", "Orange")
    for i, z in enumerate((0.1, 0.45, 0.8, 1.15)):
        b.box((1.42, 0.58, 0.03), (0, 0, z), "White")
        for k, x in enumerate((-0.55, -0.27, 0.0, 0.27, 0.55)):
            for side in (-1, 1):
                b.box((0.22, 0.2, 0.22), (x, side * 0.15, z + 0.125), colors[(i + k) % len(colors)])
        for facing, y in (("-Y", -0.29), ("+Y", 0.29)):
            b.plate(facing, (0, y, 0), 1.42, 0.035, 0.006, "Gray", v=z - 0.01)
            mirror = 1 if facing == "-Y" else -1
            for k, x in enumerate((-0.55, 0.0, 0.55)):
                b.plate(facing, (0, y * 1.02, 0), 0.07, 0.03, 0.003, "Yellow" if (i + k) % 2 else "White", u=mirror * (x + 0.06), v=z - 0.01)
    b.box((1.5, 0.6, 0.03), (0, 0, 1.49), "White")
    with b.noBevel():
        b.box((1.42, 0.62, 0.06), (0, 0, 0.03), "PoleDark")
        b.box((1.0, 0.05, 0.12), (0, 0, 1.56), "Red")
    for facing, y in (("-Y", -0.025), ("+Y", 0.025)):
        b.textBars(facing, (0, y, 0), 0.6, 0.035, (1.0,), "White", v=1.56)
    return b.build()


def makeCounter():
    """계산대(1.8 x 0.7 x 1.0): 흰 몸통(아래 걸레받이), 나무 상판, 손님 쪽(-Y) 진열 띠(글씨)·작은 진열대, 금전등록기(키패드·영수증 프린터)·카드 단말(화면)."""
    b = MeshBuilder("City_Counter", "City", bevel=0.006)
    b.box((1.8, 0.66, 0.95), (0, 0, 0.475), "White")
    b.box((1.86, 0.72, 0.05), (0, 0, 0.975), "WoodDark")
    b.box((1.8, 0.02, 0.2), (0, -0.34, 0.75), "Blue")
    b.box((0.4, 0.35, 0.2), (0.5, 0.05, 1.1), "Black")
    b.box((0.32, 0.03, 0.18), (0.5, -0.08, 1.25), "GlowWhite", rot=rotX(-20))
    b.box((0.1, 0.16, 0.06), (0.05, -0.1, 1.03), "PoleDark")
    b.textBars("-Y", (0, -0.35, 0), 0.6, 0.04, (1.0,), "White", u=-0.3, v=0.75)
    with b.noBevel():
        b.box((1.8, 0.67, 0.06), (0, 0, 0.03), "PoleDark")
        b.box((0.36, 0.01, 0.08), (0.5, -0.126, 1.06), "PoleDark")
        b.box((0.025, 0.012, 0.02), (0.5, -0.132, 1.06), "Steel")
        for row in range(3):
            for col in range(4):
                b.box((0.035, 0.035, 0.008), (0.37 + col * 0.045, 0.1 + row * 0.045, 1.204), "Gray")
        b.box((0.12, 0.1, 0.08), (0.78, 0.1, 1.04), "Gray")
        b.box((0.08, 0.012, 0.03), (0.78, 0.045, 1.07), "White")
        b.box((0.06, 0.012, 0.035), (0.05, -0.18, 1.05), "GlowWhite")
        b.box((0.3, 0.14, 0.12), (-0.6, -0.22, 1.06), "WoodDark")
        for k, x in enumerate((-0.7, -0.6, -0.5)):
            b.box((0.08, 0.1, 0.06), (x, -0.22, 1.15), ("Red", "Yellow", "Green")[k])
    return b.build()


def makeCrateStack():
    """플라스틱 상자 더미(0.6 x 0.45 x 0.75): 파란 음료 상자 3단(옆 구멍 줄·모서리 기둥), 손잡이 구멍, 맨 위 병뚜껑."""
    b = MeshBuilder("City_CrateStack", "City", bevel=0.008)
    for i in range(3):
        z = 0.125 + i * 0.25
        ox, oy = 0.01 * (i % 2), 0.005 * i
        b.box((0.6, 0.42, 0.23), (ox, oy, z), "CrateBlue")
        for side in (-1, 1):
            b.box((0.12, 0.01, 0.05), (ox, oy + side * 0.215, z + 0.05), "Black")
            facing = "-Y" if side < 0 else "+Y"
            for u in (-0.2, -0.1, 0.1, 0.2):
                b.plate(facing, (ox, oy + side * 0.21, 0), 0.06, 0.07, 0.004, "Black", u=u, v=z - 0.03)
        for facing, x in (("-X", -0.3), ("+X", 0.3)):
            for u in (-0.08, 0.08):
                b.plate(facing, (ox + x, oy, 0), 0.08, 0.07, 0.004, "Black", u=u, v=z - 0.03)
    with b.noBevel():
        for x in (-0.2, 0.0, 0.2):
            for y in (-0.1, 0.1):
                b.cylinder(0.028, 0.028, 0.03, (0.02 + x, 0.01 + y, 0.755), "Green", segments=8)
                b.cylinder(0.018, 0.018, 0.012, (0.02 + x, 0.01 + y, 0.776), "Yellow", segments=6)
    return b.build()


# ---------------------------------------------------------------- 주차장

def makeParkingBooth():
    """주차 정산소(1.6 x 1.6 x 2.5): 아래 벽(패널 줄) + 사방 창(가운데 창살) + 지붕(위 실외기), 앞(-Y) 창구(돈 받는 홈), 뒤(+Y) 문(손잡이), 간판 글씨."""
    b = MeshBuilder("City_ParkingBooth", "City", bevel=0.01)
    b.box((1.6, 1.6, 1.0), (0, 0, 0.5), "White")
    b.box((1.5, 1.5, 0.95), (0, 0, 1.47), "Glass")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.08, 0.08, 0.95), (sx * 0.76, sy * 0.76, 1.47), "Blue")
    b.box((1.8, 1.8, 0.12), (0, 0, 2.0), "Blue")
    b.box((1.4, 1.4, 0.2), (0, 0, 2.15), "White")
    b.box((0.5, 0.2, 0.04), (0, -0.88, 1.02), "Gray")
    b.box((0.9, 0.02, 0.22), (0, -0.91, 2.15), "GlowWhite")
    with b.noBevel():
        for facing, center in (("-X", (-0.755, 0, 0)), ("+X", (0.755, 0, 0)), ("+Y", (0, 0.755, 0))):
            b.plate(facing, center, 0.05, 0.95, 0.03, "Blue", v=1.47)
        b.box((1.5, 0.03, 0.05), (0, -0.76, 1.25), "Blue")
        for facing, center in (("-Y", (0, -0.8, 0)), ("-X", (-0.8, 0, 0)), ("+X", (0.8, 0, 0))):
            for u in (-0.4, 0.0, 0.4):
                b.plate(facing, center, 0.008, 0.9, 0.003, "Gray", u=u, v=0.5)
        b.box((0.5, 0.4, 0.2), (0.3, 0.2, 2.35), "Gray")
        b.box((0.18, 0.06, 0.04), (0, -0.8, 1.04), "PoleDark")
    b.frame("+Y", (0, 0.8, 0), 0.7, 1.9, 0.04, 0.01, "Blue", v=0.95)
    b.handle("+Y", (0, 0.8, 0), 0.18, "Steel", u=0.25, v=1.0, standoff=0.03, thickness=0.02)
    b.textBars("-Y", (0, -0.92, 0), 0.6, 0.06, (1.0,), "Blue", v=2.15)
    return b.build()


def makeRoadSign():
    """도로 표지판(2.8m): 기둥(꼭대기 마개·받침판) + 둥근 빨간 금지 표지(뒤 고정 밴드) + 아래 파란 안내판(흰 테두리·화살표)."""
    b = MeshBuilder("City_RoadSign", "City", bevel=0.004)
    b.cylinder(0.04, 0.04, 2.8, (0, 0, 1.4), "PoleGray", segments=10)
    b.cylinder(0.32, 0.32, 0.03, (0, -0.06, 2.45), "Red", segments=24, rot=rotX(90))
    b.cylinder(0.25, 0.25, 0.035, (0, -0.06, 2.45), "White", segments=24, rot=rotX(90))
    b.box((0.36, 0.04, 0.08), (0, -0.08, 2.45), "Red")
    b.box((0.6, 0.03, 0.32), (0, -0.06, 1.9), "Blue")
    b.box((0.5, 0.035, 0.05), (0, -0.065, 1.9), "White")
    with b.noBevel():
        b.sphere(0.045, (0, 0, 2.8), "PoleGray", segments=8, scale=(1, 1, 0.6))
        for z in (2.35, 2.55, 1.85, 1.95):
            b.cylinder(0.05, 0.05, 0.03, (0, 0, z), "Steel", segments=10)
            b.box((0.04, 0.04, 0.03), (0, -0.035, z), "Steel")
        b.cylinder(0.09, 0.09, 0.02, (0, 0, 0.01), "PoleDark", segments=10)
    b.frame("-Y", (0, -0.075, 0), 0.58, 0.3, 0.02, 0.004, "White", v=1.9)
    with b.noBevel():
        b.box((0.08, 0.004, 0.08), (0.27, -0.0835, 1.9), "White", rot=rotY(45))
    b.bolts("+Z", (0, 0, 0.02), [(0.065, 0), (-0.065, 0), (0, 0.065), (0, -0.065)], "Steel", radius=0.01)
    return b.build()


def makeWheelStop():
    """주차 차막이(1.6 x 0.3 x 0.12): 콘크리트 턱 + 노란 반사띠 2개 + 바닥 고정 앵커 볼트."""
    b = MeshBuilder("City_WheelStop", "City", bevel=0.015)
    b.box((1.6, 0.3, 0.12), (0, 0, 0.06), "Concrete")
    for x in (-0.5, 0.5):
        b.box((0.25, 0.31, 0.06), (x, 0, 0.09), "Yellow")
    b.bolts("+Z", (0, 0, 0.12), [(-0.72, 0), (0.0, 0), (0.72, 0)], "Steel", radius=0.022, depth=0.012)
    return b.build()


builders = [
    makeStreetLamp, makeTrafficLight, makeFireHydrant, makeMailbox, makeStreetBin, makeParkBench,
    makeBusStop, makeNewsBox, makeBollard, makePlanterBox, makeDumpster, makeTrashBags,
    makeACUnit, makeUtilityBox, makeBicycle, makeScooter, makeSedan, makeCafeTable,
    makeCafeChair, makeParasol, makeSignBoard, makePhoneBooth, makeBarricade, makeWaterBarrier,
    makeFridgeCase, makeShopShelf, makeCounter, makeCrateStack, makeParkingBooth, makeRoadSign,
    makeConstructionFence, makeWheelStop,
]


def main():
    """도시 소품을 만들고 FBX/블렌드/미리보기를 저장한다."""
    resetScene()
    created = [fn() for fn in builders]
    for obj in created:
        exportFbx(obj, outDir)
        print("[CityProps] exported", obj.name)

    layoutGrid(created, (0, 0, 0), 6, 5.0)
    if previewDir:
        os.makedirs(previewDir, exist_ok=True)
        renderPreview(created, os.path.join(previewDir, "preview_city.png"))
        renderPreview(created, os.path.join(previewDir, "preview_city_back.png"), azimuth=210)
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=blendPath)
    print("[CityProps] done:", len(created), "objects")


main()
