"""
도시 길거리 소품(MapKit City) 생성 스크립트. 밤의 도심 사거리(Map_CityStreet)에 쓰는 거리·가게·주차장·공사장 소품을 만든다.
목록과 크기는 Docs/CityStreetMap.md "오브젝트 모델 목록"과 같다.

실행:
  blender -b --factory-startup --python generate_city_props.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 바닥 중앙, 앞면은 블렌더 -Y = Unity +Z)
발광 머티리얼(M_City_Glow*)은 Unity 맵 빌더가 이름으로 찾아 발광 머티리얼로 바꿔 끼운다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import MeshBuilder, configure, exportFbx, layoutGrid, renderPreview, resetScene, rotX, rotY, rotZ

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


def wheel(b, x, y, z, radius, width, tire="Rubber", hub="Steel", segments=16):
    """옆에서 보는 바퀴(축이 블렌더 Y): 타이어 + 휠."""
    b.cylinder(radius, radius, width, (x, y, z), tire, segments=segments, rot=rotX(90))
    b.cylinder(radius * 0.55, radius * 0.55, width + 0.01, (x, y, z), hub, segments=segments, rot=rotX(90))


# ---------------------------------------------------------------- 도로 시설

def makeStreetLamp():
    """가로등(높이 5.2m): 받침 + 기둥 + 앞(-Y)으로 뻗은 팔 + 등갓 아래 발광판. 빛은 Unity 맵 조명이 낸다."""
    b = MeshBuilder("City_StreetLamp", "City", bevel=0.01)
    b.cylinder(0.2, 0.2, 0.3, (0, 0, 0.15), "PoleDark", segments=12)
    b.cylinder(0.08, 0.06, 4.9, (0, 0, 2.75), "PoleGray", segments=12)
    b.box((0.08, 1.2, 0.08), (0, -0.6, 5.1), "PoleGray")
    b.box((0.36, 0.6, 0.14), (0, -1.15, 5.05), "PoleDark")
    b.box((0.28, 0.5, 0.02), (0, -1.15, 4.97), "GlowWarm")
    return b.build()


def makeTrafficLight():
    """신호등(높이 4.4m): 기둥 + 앞(-Y)으로 뻗은 팔 + 3색 신호(빨강만 켜짐) + 기둥의 보행 신호."""
    b = MeshBuilder("City_TrafficLight", "City", bevel=0.01)
    b.cylinder(0.12, 0.12, 0.2, (0, 0, 0.1), "PoleDark", segments=12)
    b.cylinder(0.07, 0.07, 4.3, (0, 0, 2.25), "Yellow", segments=12)
    b.box((0.08, 2.4, 0.08), (0, -1.2, 4.2), "Yellow")
    b.box((1.0, 0.3, 0.34), (0, -2.2, 4.0), "PoleDark")
    for x, glow in ((-0.32, "GlowRed"), (0.0, "Black"), (0.32, "Black")):
        b.cylinder(0.11, 0.11, 0.04, (x, -2.37, 4.0), glow, segments=14, rot=rotX(90))
    b.box((0.3, 0.25, 0.55), (0, -0.2, 2.6), "PoleDark")
    b.box((0.22, 0.02, 0.2), (0, -0.33, 2.73), "GlowRed")
    b.box((0.22, 0.02, 0.2), (0, -0.33, 2.47), "Black")
    return b.build()


def makeFireHydrant():
    """지상식 소화전(0.8m): 빨간 몸통, 둥근 머리, 양옆 배출구, 받침."""
    b = MeshBuilder("City_FireHydrant", "City", bevel=0.006)
    b.cylinder(0.2, 0.2, 0.06, (0, 0, 0.03), "Red", segments=16)
    b.cylinder(0.14, 0.14, 0.58, (0, 0, 0.35), "Red", segments=16)
    b.cylinder(0.17, 0.17, 0.05, (0, 0, 0.64), "Red", segments=16)
    b.sphere(0.14, (0, 0, 0.68), "Red", segments=14, scale=(1, 1, 0.8))
    b.cylinder(0.03, 0.03, 0.06, (0, 0, 0.8), "Steel", segments=8)
    for side in (-1, 1):
        b.cylinder(0.06, 0.06, 0.12, (side * 0.18, 0, 0.45), "Red", segments=12, rot=rotY(90))
        b.cylinder(0.065, 0.065, 0.03, (side * 0.245, 0, 0.45), "Steel", segments=12, rot=rotY(90))
    b.cylinder(0.07, 0.07, 0.12, (0, -0.18, 0.4), "Red", segments=12, rot=rotX(90))
    return b.build()


def makeMailbox():
    """우체통(1.3m): 빨간 몸통, 둥근 지붕, 앞 투입구, 짧은 다리."""
    b = MeshBuilder("City_Mailbox", "City", bevel=0.01)
    b.box((0.5, 0.42, 0.9), (0, 0, 0.62), "Red")
    b.cylinder(0.25, 0.25, 0.42, (0, 0, 1.07), "Red", segments=18, rot=rotX(90))
    b.box((0.3, 0.02, 0.05), (0, -0.215, 0.95), "Black")
    b.box((0.22, 0.015, 0.14), (0, -0.215, 0.7), "White")
    for x in (-0.18, 0.18):
        b.box((0.06, 0.32, 0.17), (x, 0, 0.085), "PoleDark")
    return b.build()


def makeStreetBin():
    """거리 휴지통(1.0m): 짙은 녹색 원통, 위 덮개와 투입구 2개."""
    b = MeshBuilder("City_StreetBin", "City", bevel=0.006)
    b.cylinder(0.26, 0.24, 0.85, (0, 0, 0.45), "Green", segments=18)
    b.cylinder(0.28, 0.28, 0.08, (0, 0, 0.9), "PoleDark", segments=18)
    b.sphere(0.27, (0, 0, 0.94), "PoleDark", segments=16, scale=(1, 1, 0.25))
    for y in (-0.22, 0.22):
        b.box((0.22, 0.06, 0.1), (0, y, 0.82), "Black")
    b.cylinder(0.27, 0.27, 0.04, (0, 0, 0.02), "PoleDark", segments=18)
    return b.build()


def makeParkBench():
    """공원 벤치(1.6m): 나무 좌판 3장 + 등받이 2장(+Y), 주철 다리."""
    b = MeshBuilder("City_ParkBench", "City", bevel=0.006)
    for y in (-0.16, 0.0, 0.16):
        b.box((1.6, 0.13, 0.04), (0, y, 0.45), "Wood")
    for z in (0.62, 0.78):
        b.box((1.6, 0.03, 0.11), (0, 0.27, z), "Wood", rot=rotX(-10))
    for x in (-0.68, 0.68):
        b.box((0.05, 0.5, 0.05), (x, 0.02, 0.42), "PoleDark")
        b.box((0.05, 0.05, 0.42), (x, -0.2, 0.21), "PoleDark")
        b.box((0.05, 0.05, 0.85), (x, 0.24, 0.425), "PoleDark")
        b.box((0.05, 0.4, 0.04), (x, 0.0, 0.65), "PoleDark")
    return b.build()


def makeBusStop():
    """버스 정류장(3.6 x 1.5 x 2.6): 뒤(+Y) 유리벽, 옆 광고판, 지붕, 긴 의자, 노선 안내판."""
    b = MeshBuilder("City_BusStop", "City", bevel=0.008)
    for x in (-1.75, 1.75):
        b.box((0.08, 0.08, 2.5), (x, 0.65, 1.25), "PoleGray")
        b.box((0.08, 0.08, 2.5), (x, -0.6, 1.25), "PoleGray")
    b.box((3.6, 1.5, 0.08), (0, 0.05, 2.56), "PoleDark")
    b.box((3.4, 0.03, 1.8), (0, 0.68, 1.3), "Glass")
    b.box((0.04, 1.2, 1.9), (1.75, 0.03, 1.25), "PoleDark")
    b.box((0.05, 1.1, 1.6), (1.75, 0.03, 1.25), "GlowWhite")
    b.box((2.4, 0.35, 0.05), (-0.4, 0.45, 0.45), "Steel")
    for x in (-1.4, 0.6):
        b.box((0.05, 0.3, 0.43), (x, 0.45, 0.215), "PoleDark")
    b.box((1.0, 0.04, 0.4), (-1.0, 0.65, 2.1), "Blue")
    return b.build()


def makeNewsBox():
    """신문 가판함(1.1m): 파란 상자, 앞 유리창, 위 비스듬한 지붕, 다리."""
    b = MeshBuilder("City_NewsBox", "City", bevel=0.008)
    b.box((0.5, 0.42, 0.75), (0, 0, 0.62), "Blue")
    b.box((0.4, 0.02, 0.32), (0, -0.215, 0.75), "Glass")
    b.box((0.42, 0.02, 0.05), (0, -0.215, 0.5), "Steel")
    b.box((0.54, 0.46, 0.04), (0, 0, 1.02), "Blue", rot=rotX(-6))
    b.box((0.4, 0.4, 0.25), (0, 0, 0.125), "PoleDark")
    return b.build()


def makeBollard():
    """볼라드(0.9m): 회색 기둥 + 노란 반사띠 + 둥근 머리."""
    b = MeshBuilder("City_Bollard", "City", bevel=0.004)
    b.cylinder(0.1, 0.09, 0.8, (0, 0, 0.4), "PoleGray", segments=14)
    b.cylinder(0.102, 0.102, 0.06, (0, 0, 0.66), "Yellow", segments=14)
    b.sphere(0.09, (0, 0, 0.8), "PoleGray", segments=12, scale=(1, 1, 0.8))
    return b.build()


def makePlanterBox():
    """화단 상자(1.2 x 0.6, 높이 1.1): 콘크리트 상자 + 흙 + 낮은 관목 덩어리."""
    b = MeshBuilder("City_PlanterBox", "City", bevel=0.01)
    b.box((1.2, 0.6, 0.5), (0, 0, 0.25), "Concrete")
    b.box((1.1, 0.5, 0.02), (0, 0, 0.49), "Soil")
    for x, r in ((-0.35, 0.3), (0.05, 0.34), (0.38, 0.27)):
        b.sphere(r, (x, 0, 0.5 + r * 0.9), "Plant", segments=12, scale=(1, 0.85, 1))
    return b.build()


# ---------------------------------------------------------------- 뒷골목·설비

def makeDumpster():
    """대형 쓰레기 수거함(1.9 x 1.1 x 1.3): 녹색 철통, 비스듬한 뚜껑 2장, 옆 손잡이, 바퀴."""
    b = MeshBuilder("City_Dumpster", "City", bevel=0.01)
    b.box((1.8, 1.0, 1.05), (0, 0, 0.66), "Green")
    b.box((1.86, 1.06, 0.06), (0, 0, 1.18), "PoleDark")
    for x in (-0.46, 0.46):
        b.box((0.9, 1.08, 0.04), (x, 0, 1.24), "Black", rot=rotX(-4))
    for side in (-1, 1):
        b.box((0.06, 0.3, 0.06), (side * 0.93, 0, 0.95), "PoleDark")
    for sx in (-0.75, 0.75):
        for sy in (-0.38, 0.38):
            b.cylinder(0.07, 0.07, 0.05, (sx, sy, 0.07), "Rubber", segments=10, rot=rotX(90))
    b.box((1.6, 0.02, 0.14), (0, -0.505, 0.85), "Yellow")
    return b.build()


def makeTrashBags():
    """검은 쓰레기봉투 더미(1.0 x 0.8 x 0.6): 크기가 다른 봉투 4개, 묶은 매듭."""
    b = MeshBuilder("City_TrashBags", "City", bevel=0.0)
    for x, y, r, h in ((-0.25, 0.05, 0.28, 0.85), (0.22, -0.1, 0.3, 0.8), (0.05, 0.22, 0.24, 0.9), (0.0, -0.05, 0.22, 1.25)):
        z = r * h if h < 1.0 else 0.42
        b.sphere(r, (x, y, z), "TrashBag", segments=12, scale=(1, 0.9, h))
        b.cylinder(0.04, 0.02, 0.1, (x, y, z + r * h + 0.03), "TrashBag", segments=8)
    return b.build()


def makeACUnit():
    """에어컨 실외기(0.85 x 0.35 x 0.65): 흰 상자, 앞 원형 팬, 받침 다리."""
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
    return b.build()


def makeUtilityBox():
    """한전 변압기함(0.9 x 0.5 x 1.4): 회녹색 함, 앞문 2짝, 경고 표지, 콘크리트 받침."""
    b = MeshBuilder("City_UtilityBox", "City", bevel=0.008)
    b.box((1.0, 0.6, 0.12), (0, 0, 0.06), "Concrete")
    b.box((0.9, 0.5, 1.22), (0, 0, 0.73), "Gray")
    b.box((0.94, 0.54, 0.05), (0, 0, 1.365), "PoleGray")
    b.box((0.002, 0.01, 1.1), (0, -0.255, 0.73), "PoleDark")
    for x in (-0.2, 0.2):
        b.box((0.03, 0.03, 0.12), (x + (0.15 if x < 0 else -0.15), -0.265, 0.75), "PoleDark")
    b.box((0.16, 0.01, 0.16), (-0.2, -0.26, 1.05), "Yellow", rot=rotY(45))
    b.box((0.3, 0.01, 0.08), (0.2, -0.26, 1.05), "White")
    return b.build()


def makeBicycle():
    """자전거(1.7m, X 방향): 바퀴 2개, 삼각 차체, 안장, 핸들."""
    b = MeshBuilder("City_Bicycle", "City", bevel=0.0)
    for x in (-0.55, 0.55):
        wheel(b, x, 0, 0.34, 0.34, 0.04, segments=20)
    b.cylinder(0.018, 0.018, 0.6, (-0.2, 0, 0.58), "Blue", segments=8, rot=rotY(60))
    b.cylinder(0.018, 0.018, 0.62, (0.17, 0, 0.6), "Blue", segments=8, rot=rotY(90))
    b.cylinder(0.018, 0.018, 0.55, (0.25, 0, 0.5), "Blue", segments=8, rot=rotY(-50))
    b.cylinder(0.018, 0.018, 0.5, (-0.4, 0, 0.5), "Blue", segments=8, rot=rotY(-40))
    b.cylinder(0.018, 0.018, 0.5, (0.5, 0, 0.62), "PoleDark", segments=8, rot=rotY(15))
    b.box((0.24, 0.1, 0.05), (-0.18, 0, 0.9), "Black")
    b.cylinder(0.015, 0.015, 0.5, (0.44, 0, 0.92), "PoleDark", segments=8, rot=rotX(90))
    return b.build()


def makeScooter():
    """배달 오토바이(1.8m, X 방향): 바퀴 2개, 차체, 발판, 안장, 뒤 배달 상자, 핸들·전조등."""
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
    return b.build()


def makeSedan():
    """승용차(4.4 x 1.8 x 1.45, X 방향): 하부 차체, 위 유리 객실, 바퀴 4개, 앞(+X) 전조등·뒤 후미등."""
    b = MeshBuilder("City_Sedan", "City", bevel=0.03)
    b.box((4.4, 1.8, 0.62), (0, 0, 0.55), "CarPaint")
    b.box((2.3, 1.62, 0.5), (-0.2, 0, 1.1), "Glass")
    b.box((2.1, 1.64, 0.06), (-0.2, 0, 1.38), "CarPaint")
    for x in (-0.95, 0.55):
        b.box((0.08, 1.63, 0.46), (x, 0, 1.1), "CarPaint")
    for x in (-1.4, 1.4):
        for y in (-0.82, 0.82):
            wheel(b, x, y, 0.33, 0.33, 0.22, segments=18)
    b.box((4.46, 1.84, 0.12), (0, 0, 0.3), "PoleDark")
    for y in (-0.6, 0.6):
        b.box((0.04, 0.32, 0.12), (2.21, y, 0.7), "GlowWhite")
        b.box((0.04, 0.32, 0.12), (-2.21, y, 0.72), "GlowRed")
    b.box((0.04, 0.5, 0.12), (2.21, 0, 0.52), "Black")
    return b.build()


# ---------------------------------------------------------------- 카페·가게 앞

def makeCafeTable():
    """둥근 카페 탁자(0.7m): 상판 + 기둥 + 십자 받침."""
    b = MeshBuilder("City_CafeTable", "City", bevel=0.004)
    b.cylinder(0.35, 0.35, 0.03, (0, 0, 0.735), "WoodDark", segments=22)
    b.cylinder(0.035, 0.035, 0.7, (0, 0, 0.37), "PoleDark", segments=10)
    b.box((0.55, 0.06, 0.03), (0, 0, 0.015), "PoleDark")
    b.box((0.06, 0.55, 0.03), (0, 0, 0.015), "PoleDark")
    return b.build()


def makeCafeChair():
    """카페 의자: 금속 다리 4개, 나무 좌판, 등받이(+Y)."""
    b = MeshBuilder("City_CafeChair", "City", bevel=0.004)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.cylinder(0.015, 0.015, 0.45, (sx * 0.18, sy * 0.18, 0.225), "PoleDark", segments=8)
    b.box((0.44, 0.44, 0.03), (0, 0, 0.46), "Wood")
    for sx in (-1, 1):
        b.cylinder(0.015, 0.015, 0.4, (sx * 0.18, 0.2, 0.66), "PoleDark", segments=8)
    b.box((0.42, 0.03, 0.14), (0, 0.21, 0.78), "Wood")
    return b.build()


def makeParasol():
    """파라솔(지름 2.2, 높이 2.4): 무거운 받침 + 기둥 + 8각 천 덮개."""
    b = MeshBuilder("City_Parasol", "City", bevel=0.0)
    b.cylinder(0.25, 0.25, 0.08, (0, 0, 0.04), "Concrete", segments=16)
    b.cylinder(0.025, 0.025, 2.35, (0, 0, 1.2), "White", segments=8)
    b.cylinder(1.1, 0.05, 0.4, (0, 0, 2.15), "Canvas", segments=8)
    b.cylinder(1.1, 1.1, 0.12, (0, 0, 1.92), "Canvas", segments=8)
    return b.build()


def makeSignBoard():
    """입간판(A형, 0.55 x 0.5 x 0.95): 검은 칠판 두 장 + 나무 테두리."""
    b = MeshBuilder("City_SignBoard", "City", bevel=0.004)
    for side in (-1, 1):
        b.box((0.55, 0.03, 0.92), (0, side * 0.12, 0.46), "WoodDark", rot=rotX(side * 13))
        b.box((0.45, 0.035, 0.6), (0, side * 0.125, 0.52), "Black", rot=rotX(side * 13))
        b.box((0.3, 0.036, 0.04), (0, side * 0.127, 0.62), "White", rot=rotX(side * 13))
    return b.build()


def makePhoneBooth():
    """공중전화 부스(1.0 x 1.0 x 2.3): 틀 + 유리 3면, 앞(-Y)은 열림, 안에 전화기, 위 표시판."""
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
    return b.build()


# ---------------------------------------------------------------- 공사장

def makeBarricade():
    """공사 차단기(1.6 x 0.5 x 1.05): 주황·흰 줄무늬 가로판 2장, A형 다리, 경고등."""
    b = MeshBuilder("City_Barricade", "City", bevel=0.004)
    for z in (0.55, 0.85):
        b.box((1.6, 0.04, 0.16), (0, -0.05, z), "White")
        for x in (-0.6, -0.2, 0.2, 0.6):
            b.box((0.18, 0.045, 0.16), (x, -0.05, z), "Orange")
    for x in (-0.7, 0.7):
        for side in (-1, 1):
            b.box((0.05, 0.05, 1.0), (x, side * 0.12, 0.48), "PoleDark", rot=rotX(side * 12))
    b.cylinder(0.06, 0.06, 0.1, (0.7, -0.05, 1.0), "GlowAmber", segments=10)
    return b.build()


def makeWaterBarrier():
    """플라스틱 물통 차단벽(1.4 x 0.45 x 0.8): 사다리꼴 단면, 주황 몸통, 흰 반사띠."""
    b = MeshBuilder("City_WaterBarrier", "City", bevel=0.02)
    w, d0, d1, h = 0.7, 0.225, 0.1, 0.8
    pts = [(-w, -d0, 0), (w, -d0, 0), (w, d0, 0), (-w, d0, 0), (-w, -d1, h), (w, -d1, h), (w, d1, h), (-w, d1, h)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    b.polyhedron(pts, faces, "Orange")
    b.box((1.3, 0.02, 0.1), (0, -0.175, 0.45), "White", rot=rotX(-8))
    b.box((1.3, 0.02, 0.1), (0, 0.175, 0.45), "White", rot=rotX(8))
    b.cylinder(0.06, 0.06, 0.04, (0.4, 0, 0.81), "White", segments=10)
    return b.build()


def makeConstructionFence():
    """공사장 가림막(2.0 x 0.5 x 2.0): 흰 강판 + 위 녹색 띠, 뒤 지지대, 콘크리트 받침. (벽 취급, 변신 안 됨)"""
    b = MeshBuilder("City_ConstructionFence", "City", bevel=0.004)
    b.box((2.0, 0.04, 1.85), (0, 0, 1.03), "White")
    b.box((2.0, 0.045, 0.25), (0, 0, 1.8), "Green")
    for x in (-0.95, 0.95):
        b.box((0.06, 0.06, 2.0), (x, 0.03, 1.0), "PoleGray")
        b.box((0.05, 0.05, 1.0), (x, 0.3, 0.5), "PoleGray", rot=rotX(-30))
        b.box((0.3, 0.5, 0.12), (x, 0.18, 0.06), "Concrete")
    return b.build()


# ---------------------------------------------------------------- 가게 안

def makeFridgeCase():
    """음료 냉장고(1.2 x 0.75 x 2.0): 흰 몸통, 앞 유리문 2짝, 안 선반 칸, 위 발광 간판."""
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
    return b.build()


def makeShopShelf():
    """편의점 진열대(1.5 x 0.6 x 1.5): 금속 뼈대, 선반 4단, 상품 상자 줄."""
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
    b.box((1.5, 0.6, 0.03), (0, 0, 1.49), "White")
    return b.build()


def makeCounter():
    """계산대(1.8 x 0.7 x 1.0): 흰 몸통, 나무 상판, 손님 쪽(-Y) 진열 띠, 금전등록기·카드 단말."""
    b = MeshBuilder("City_Counter", "City", bevel=0.006)
    b.box((1.8, 0.66, 0.95), (0, 0, 0.475), "White")
    b.box((1.86, 0.72, 0.05), (0, 0, 0.975), "WoodDark")
    b.box((1.8, 0.02, 0.2), (0, -0.34, 0.75), "Blue")
    b.box((0.4, 0.35, 0.2), (0.5, 0.05, 1.1), "Black")
    b.box((0.32, 0.03, 0.18), (0.5, -0.08, 1.25), "GlowWhite", rot=rotX(-20))
    b.box((0.1, 0.16, 0.06), (0.05, -0.1, 1.03), "PoleDark")
    return b.build()


def makeCrateStack():
    """플라스틱 상자 더미(0.6 x 0.45 x 0.75): 파란 음료 상자 3단, 손잡이 구멍."""
    b = MeshBuilder("City_CrateStack", "City", bevel=0.008)
    for i in range(3):
        z = 0.125 + i * 0.25
        b.box((0.6, 0.42, 0.23), (0.01 * (i % 2), 0.005 * i, z), "CrateBlue")
        for side in (-1, 1):
            b.box((0.12, 0.01, 0.05), (0, side * 0.215, z + 0.05), "Black")
    return b.build()


# ---------------------------------------------------------------- 주차장

def makeParkingBooth():
    """주차 정산소(1.6 x 1.6 x 2.5): 아래 벽 + 사방 창 + 지붕, 앞(-Y) 창구."""
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
    return b.build()


def makeRoadSign():
    """도로 표지판(2.8m): 기둥 + 둥근 빨간 금지 표지 + 아래 파란 안내판."""
    b = MeshBuilder("City_RoadSign", "City", bevel=0.004)
    b.cylinder(0.04, 0.04, 2.8, (0, 0, 1.4), "PoleGray", segments=10)
    b.cylinder(0.32, 0.32, 0.03, (0, -0.06, 2.45), "Red", segments=24, rot=rotX(90))
    b.cylinder(0.25, 0.25, 0.035, (0, -0.06, 2.45), "White", segments=24, rot=rotX(90))
    b.box((0.36, 0.04, 0.08), (0, -0.08, 2.45), "Red")
    b.box((0.6, 0.03, 0.32), (0, -0.06, 1.9), "Blue")
    b.box((0.5, 0.035, 0.05), (0, -0.065, 1.9), "White")
    return b.build()


def makeWheelStop():
    """주차 차막이(1.6 x 0.3 x 0.12): 콘크리트 턱 + 노란 반사띠 2개."""
    b = MeshBuilder("City_WheelStop", "City", bevel=0.015)
    b.box((1.6, 0.3, 0.12), (0, 0, 0.06), "Concrete")
    for x in (-0.5, 0.5):
        b.box((0.25, 0.31, 0.06), (x, 0, 0.09), "Yellow")
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
