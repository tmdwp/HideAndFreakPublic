"""
지하철역 소품(MapKit Subway) 생성 스크립트. 낡은 공포 에셋 대신 깨끗한 역 분위기(스테인리스·흰 플라스틱·노선 색)로 만든다.

실행:
  blender -b --python generate_subway_props.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 바닥 중앙, 앞면은 블렌더 -Y = Unity +Z)
발광 머티리얼(Screen, LightPanel, AdPanel)은 Unity 맵 빌더가 이름으로 찾아 발광 머티리얼로 바꿔 끼운다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import MeshBuilder, configure, exportFbx, layoutGrid, renderPreview, resetScene, rotX, rotY, rotZ

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models", "Subway")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "Steel":        ((0.72, 0.74, 0.76), 0.85, 0.3, 0.0),
    "SteelDark":    ((0.30, 0.31, 0.33), 0.7, 0.45, 0.0),
    "PanelWhite":   ((0.90, 0.91, 0.92), 0.0, 0.4, 0.0),
    "PanelGray":    ((0.62, 0.64, 0.66), 0.1, 0.5, 0.0),
    "Black":        ((0.06, 0.06, 0.07), 0.0, 0.6, 0.0),
    "LineGreen":    ((0.10, 0.62, 0.28), 0.0, 0.45, 0.0),
    "LineBlue":     ((0.08, 0.28, 0.72), 0.0, 0.45, 0.0),
    "SafetyYellow": ((0.95, 0.76, 0.08), 0.0, 0.5, 0.0),
    "FireRed":      ((0.78, 0.08, 0.06), 0.2, 0.45, 0.0),
    "Ceramic":      ((0.96, 0.96, 0.95), 0.0, 0.15, 0.0),
    "Laminate":     ((0.80, 0.76, 0.68), 0.0, 0.5, 0.0),
    "Fabric":       ((0.16, 0.22, 0.34), 0.0, 0.95, 0.0),
    "Plant":        ((0.20, 0.45, 0.18), 0.0, 0.85, 0.0),
    "Soil":         ((0.22, 0.15, 0.10), 0.0, 1.0, 0.0),
    "Suitcase":     ((0.15, 0.38, 0.60), 0.1, 0.4, 0.0),
    "Rubber":       ((0.05, 0.05, 0.05), 0.0, 0.9, 0.0),
    "Screen":       ((0.10, 0.35, 0.60), 0.0, 0.2, 2.0),
    "LightPanel":   ((0.95, 0.96, 0.98), 0.0, 0.3, 2.0),
    "AdPanel":      ((0.95, 0.78, 0.55), 0.0, 0.3, 2.0),
}
configure(palette, "M_Sub_")


# ---------------------------------------------------------------- 승강장·대합실

def makeBench():
    """스테인리스 3인용 벤치(1.8 x 0.55, 좌판 0.45, 등받이 0.85). 등받이는 +Y, 좌석 사이 팔걸이."""
    b = MeshBuilder("Sub_Bench", "Subway", bevel=0.006)
    for x in (-0.6, 0, 0.6):
        b.box((0.56, 0.42, 0.04), (x, -0.02, 0.45), "Steel")
        b.box((0.56, 0.04, 0.38), (x, 0.2, 0.66), "Steel", rot=rotX(-8))
    for x in (-0.9, -0.3, 0.3, 0.9):
        b.box((0.04, 0.4, 0.05), (x, 0.0, 0.62), "SteelDark")
        b.box((0.04, 0.04, 0.17), (x, -0.18, 0.54), "SteelDark")
    for x in (-0.7, 0.7):
        b.box((0.06, 0.06, 0.43), (x, -0.12, 0.215), "SteelDark")
        b.box((0.06, 0.06, 0.85), (x, 0.2, 0.425), "SteelDark")
        b.box((0.08, 0.45, 0.04), (x, 0.04, 0.42), "SteelDark")
        b.box((0.16, 0.5, 0.02), (x, 0.04, 0.01), "SteelDark")
    b.box((1.6, 0.04, 0.04), (0, 0.2, 0.3), "SteelDark")
    return b.build()


def makeRecycleBin():
    """분리수거함(1.0 x 0.45 x 1.0): 스테인리스 몸통, 위 투입구 3개, 앞면 색 띠(일반·플라스틱·캔)."""
    b = MeshBuilder("Sub_RecycleBin", "Subway", bevel=0.008)
    b.box((1.0, 0.45, 0.92), (0, 0, 0.46), "Steel")
    b.box((1.04, 0.49, 0.06), (0, 0, 0.95), "SteelDark")
    for x, color in ((-0.33, "PanelGray"), (0, "LineBlue"), (0.33, "SafetyYellow")):
        b.box((0.22, 0.14, 0.02), (x, -0.06, 0.985), "Black")
        b.box((0.3, 0.01, 0.16), (x, -0.226, 0.76), color)
        b.box((0.004, 0.01, 0.8), (x + 0.165, -0.226, 0.44), "SteelDark")
    b.box((1.0, 0.45, 0.04), (0, 0, 0.02), "Rubber")
    return b.build()


def makeTicketMachine():
    """교통카드 발매기(0.9 x 0.6 x 1.75): 흰 몸통, 기울어진 화면, 카드 투입구·지폐구·받는 곳, 위 초록 머리판."""
    b = MeshBuilder("Sub_TicketMachine", "Subway", bevel=0.01)
    b.box((0.9, 0.6, 1.6), (0, 0, 0.8), "PanelWhite")
    b.box((0.92, 0.62, 0.15), (0, 0, 1.675), "LineGreen")
    b.box((0.94, 0.64, 0.06), (0, 0, 0.03), "SteelDark")
    b.box((0.62, 0.06, 0.42), (0, -0.31, 1.2), "Black", rot=rotX(12))
    b.box((0.56, 0.02, 0.36), (0, -0.345, 1.2), "Screen", rot=rotX(12))
    b.box((0.7, 0.12, 0.05), (0, -0.33, 0.93), "PanelGray")
    for x in (-0.22, 0.0, 0.22):
        b.box((0.12, 0.02, 0.04), (x, -0.31, 0.82), "Black")
    b.box((0.4, 0.08, 0.14), (0, -0.32, 0.5), "SteelDark")
    b.box((0.36, 0.02, 0.1), (0, -0.36, 0.5), "Black")
    return b.build()


def makeFireCabinet():
    """옥내 소화전함(0.8 x 0.3 x 1.5): 빨간 몸통, 흰 테두리 앞문, 손잡이, 위 표시등."""
    b = MeshBuilder("Sub_FireCabinet", "Subway", bevel=0.008)
    b.box((0.8, 0.3, 1.5), (0, 0, 0.75), "FireRed")
    b.box((0.7, 0.02, 0.62), (0, -0.155, 1.05), "PanelWhite")
    b.box((0.62, 0.025, 0.54), (0, -0.16, 1.05), "FireRed")
    b.box((0.7, 0.02, 0.5), (0, -0.155, 0.4), "PanelWhite")
    b.box((0.62, 0.025, 0.42), (0, -0.16, 0.4), "FireRed")
    b.box((0.03, 0.04, 0.16), (0.26, -0.18, 0.75), "Steel")
    b.cylinder(0.05, 0.05, 0.04, (0, -0.17, 1.42), "LightPanel", segments=14, rot=rotX(90))
    return b.build()


def makeInfoBoard():
    """노선도 안내판(1.2 x 0.25 x 2.0): 스테인리스 틀, 양면 발광판에 노선 색 띠."""
    b = MeshBuilder("Sub_InfoBoard", "Subway", bevel=0.006)
    b.box((1.2, 0.25, 0.08), (0, 0, 0.04), "SteelDark")
    for x in (-0.56, 0.56):
        b.box((0.08, 0.2, 1.95), (x, 0, 1.0), "Steel")
    b.box((1.2, 0.2, 0.08), (0, 0, 1.96), "Steel")
    b.box((1.04, 0.14, 1.5), (0, 0, 1.12), "LightPanel")
    for side in (-1, 1):
        y = side * 0.072
        b.box((0.9, 0.006, 0.05), (0, y, 1.5), "LineGreen")
        b.box((0.05, 0.006, 0.9), (-0.15, y, 1.1), "LineBlue")
        b.box((0.7, 0.006, 0.05), (0.05, y, 0.9), "LineBlue", rot=rotY(25))
        b.box((0.9, 0.006, 0.08), (0, y, 1.78), "Black")
    return b.build()


def makeAdBoard():
    """광고 조명판(1.6 x 0.15 x 2.0): 두 다리 위 발광 광고면, 아래 받침."""
    b = MeshBuilder("Sub_AdBoard", "Subway", bevel=0.006)
    for x in (-0.75, 0.75):
        b.box((0.06, 0.1, 2.0), (x, 0, 1.0), "SteelDark")
        b.box((0.2, 0.4, 0.04), (x, 0, 0.02), "SteelDark")
    b.box((1.5, 0.12, 1.1), (0, 0, 1.35), "Steel")
    b.box((1.38, 0.13, 0.98), (0, 0, 1.35), "AdPanel")
    b.box((1.38, 0.132, 0.12), (0, 0, 1.0), "LineGreen")
    return b.build()


def makeCoinLocker():
    """물품보관함(1.5 x 0.6 x 1.9): 3열 x 4단 문, 가운데 아래 결제 화면, 위 노선 색 머리판."""
    b = MeshBuilder("Sub_CoinLocker", "Subway", bevel=0.005)
    b.box((1.5, 0.6, 1.9), (0, 0, 0.95), "PanelGray")
    b.box((1.52, 0.62, 0.15), (0, 0, 1.835), "LineBlue")
    for col, x in enumerate((-0.5, 0, 0.5)):
        for row, z in enumerate((0.3, 0.68, 1.06, 1.44)):
            if col == 1 and row == 0:
                b.box((0.46, 0.02, 0.34), (x, -0.305, z), "PanelWhite")
                b.box((0.28, 0.02, 0.18), (x, -0.315, z + 0.03), "Screen")
                continue
            b.box((0.46, 0.02, 0.34), (x, -0.305, z), "PanelWhite")
            b.box((0.04, 0.03, 0.06), (x + 0.17, -0.32, z), "SteelDark")
    b.box((1.5, 0.6, 0.1), (0, 0, 0.05), "SteelDark")
    return b.build()


def makeEmergencyBox():
    """비상용품함(방독면함, 0.6 x 0.35 x 0.9): 짙은 녹색 몸통, 노란 띠, 흰 십자, 투명창 대신 밝은 판."""
    b = MeshBuilder("Sub_EmergencyBox", "Subway", bevel=0.008)
    b.box((0.6, 0.35, 0.9), (0, 0, 0.45), "LineGreen")
    b.box((0.62, 0.36, 0.07), (0, 0, 0.875), "SafetyYellow")
    b.box((0.5, 0.02, 0.5), (0, -0.18, 0.45), "PanelWhite")
    b.box((0.08, 0.025, 0.28), (0, -0.19, 0.45), "FireRed")
    b.box((0.28, 0.025, 0.08), (0, -0.19, 0.45), "FireRed")
    return b.build()


def makeStanchion():
    """줄서기 차단봉 한 쌍(폭 1.6, 높이 0.95): 둥근 받침 + 기둥 + 사이 띠."""
    b = MeshBuilder("Sub_Stanchion", "Subway", bevel=0.003)
    for x in (-0.8, 0.8):
        b.cylinder(0.17, 0.17, 0.04, (x, 0, 0.02), "SteelDark", segments=18)
        b.cylinder(0.03, 0.03, 0.9, (x, 0, 0.47), "Steel", segments=12)
        b.cylinder(0.04, 0.04, 0.06, (x, 0, 0.93), "Steel", segments=12)
    b.box((1.56, 0.012, 0.06), (0, 0, 0.88), "LineBlue")
    return b.build()


def makeWetFloorSign():
    """'바닥 미끄럼 주의' A형 표지(0.3 x 0.32 x 0.62)."""
    b = MeshBuilder("Sub_WetFloorSign", "Subway", bevel=0.004)
    for side in (-1, 1):
        b.box((0.3, 0.02, 0.62), (0, side * 0.08, 0.3), "SafetyYellow", rot=rotX(side * 14))
        b.box((0.16, 0.025, 0.16), (0, side * 0.085, 0.34), "Black", rot=rotX(side * 14))
    b.box((0.08, 0.06, 0.04), (0, 0, 0.6), "SafetyYellow")
    return b.build()


def makePlanter():
    """사각 화분(0.6 x 0.6, 높이 1.2): 흰 화분 + 흙 + 둥근 잎 덩어리."""
    b = MeshBuilder("Sub_Planter", "Subway", bevel=0.006)
    b.box((0.6, 0.6, 0.5), (0, 0, 0.25), "PanelWhite")
    b.box((0.62, 0.62, 0.04), (0, 0, 0.5), "PanelGray")
    b.box((0.52, 0.52, 0.02), (0, 0, 0.49), "Soil")
    b.cylinder(0.03, 0.03, 0.3, (0, 0, 0.62), "Soil", segments=8)
    b.sphere(0.32, (0, 0, 0.88), "Plant", segments=14, scale=(1.0, 1.0, 0.95))
    b.sphere(0.2, (0.16, -0.1, 1.05), "Plant", segments=12)
    b.sphere(0.18, (-0.15, 0.12, 1.0), "Plant", segments=12)
    return b.build()


def makeSuitcase():
    """여행 가방(0.45 x 0.28 x 0.7 + 손잡이): 파란 몸통, 모서리 띠, 바퀴."""
    b = MeshBuilder("Sub_Suitcase", "Subway", bevel=0.02)
    b.box((0.45, 0.26, 0.62), (0, 0, 0.36), "Suitcase")
    for x in (-0.12, 0.12):
        b.box((0.03, 0.27, 0.6), (x, 0, 0.36), "SteelDark")
        b.cylinder(0.025, 0.025, 0.24, (x, 0.08, 0.79), "Steel", segments=8)
        b.cylinder(0.025, 0.025, 0.02, (x + (0.08 if x > 0 else -0.08), 0.1, 0.025), "Rubber", segments=10, rot=rotY(90))
    b.box((0.28, 0.04, 0.03), (0, 0.08, 0.92), "Black")
    return b.build()


def makeCleaningCart():
    """청소 카트(0.95 x 0.5 x 1.0): 노란 틀, 회색 수거 주머니, 양동이, 손잡이."""
    b = MeshBuilder("Sub_CleaningCart", "Subway", bevel=0.006)
    b.box((0.95, 0.5, 0.06), (0, 0, 0.12), "SafetyYellow")
    b.box((0.95, 0.5, 0.04), (0, 0, 0.62), "SafetyYellow")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.04, 0.04, 0.6), (sx * 0.45, sy * 0.23, 0.38), "SafetyYellow")
            b.cylinder(0.04, 0.04, 0.03, (sx * 0.42, sy * 0.2, 0.04), "Rubber", segments=10, rot=rotX(90))
    b.box((0.4, 0.42, 0.5), (0.22, 0, 0.4), "PanelGray")
    b.cylinder(0.14, 0.17, 0.28, (-0.25, 0, 0.8), "LineBlue", segments=16)
    b.cylinder(0.015, 0.015, 0.9, (-0.1, 0.1, 1.0), "Steel", segments=8, rot=rotX(-10))
    b.box((0.04, 0.5, 0.04), (0.5, 0, 0.98), "SteelDark")
    for sy in (-1, 1):
        b.box((0.04, 0.04, 0.36), (0.5, sy * 0.23, 0.8), "SteelDark")
    return b.build()


# ---------------------------------------------------------------- 역무실·화장실

def makeOfficeDesk():
    """사무 책상(1.4 x 0.7 x 0.74): 밝은 상판, 흰 옆판, 오른쪽 서랍. 앉는 쪽은 +Y."""
    b = MeshBuilder("Sub_OfficeDesk", "Subway", bevel=0.004)
    b.box((1.4, 0.7, 0.03), (0, 0, 0.725), "Laminate")
    b.box((0.03, 0.66, 0.71), (-0.68, 0, 0.355), "PanelWhite")
    b.box((0.4, 0.62, 0.71), (0.48, 0, 0.355), "PanelWhite")
    for z in (0.17, 0.4, 0.6):
        b.box((0.36, 0.02, 0.18), (0.48, 0.32, z), "PanelGray")
        b.box((0.12, 0.025, 0.02), (0.48, 0.335, z + 0.05), "SteelDark")
    b.box((1.1, 0.02, 0.4), (-0.1, -0.3, 0.45), "PanelWhite")
    return b.build()


def makeOfficeChair():
    """사무 의자: 별 모양 다리 + 기둥 + 남색 좌판·등받이(+Y)."""
    b = MeshBuilder("Sub_OfficeChair", "Subway", bevel=0.01)
    for i in range(5):
        a = i * 72
        b.box((0.3, 0.05, 0.04), (0.15 * math.cos(math.radians(a)), 0.15 * math.sin(math.radians(a)), 0.08), "Black", rot=rotZ(a))
        b.sphere(0.03, (0.29 * math.cos(math.radians(a)), 0.29 * math.sin(math.radians(a)), 0.03), "Black", segments=8)
    b.cylinder(0.03, 0.03, 0.35, (0, 0, 0.27), "Steel", segments=10)
    b.box((0.5, 0.48, 0.08), (0, 0, 0.48), "Fabric")
    b.box((0.46, 0.07, 0.5), (0, 0.24, 0.8), "Fabric", rot=rotX(-8))
    b.box((0.05, 0.05, 0.3), (0, 0.24, 0.55), "Black")
    return b.build()


def makeMonitor():
    """평면 모니터(0.55 x 0.2 x 0.45): 받침 + 목 + 얇은 화면(앞 -Y)."""
    b = MeshBuilder("Sub_Monitor", "Subway", bevel=0.003)
    b.box((0.24, 0.18, 0.015), (0, 0, 0.008), "Black")
    b.box((0.04, 0.03, 0.12), (0, 0.04, 0.07), "Black")
    b.box((0.55, 0.035, 0.33), (0, 0.02, 0.28), "Black")
    b.box((0.51, 0.005, 0.29), (0, 0.0, 0.28), "Screen")
    return b.build()


def makeToilet():
    """양변기(0.4 x 0.7 x 0.8): 흰 도기, 물탱크는 +Y."""
    b = MeshBuilder("Sub_Toilet", "Subway", bevel=0.01)
    b.cylinder(0.12, 0.15, 0.36, (0, -0.05, 0.18), "Ceramic", segments=18)
    b.sphere(0.2, (0, -0.1, 0.38), "Ceramic", segments=18, scale=(0.95, 1.2, 0.35))
    b.box((0.38, 0.4, 0.03), (0, -0.08, 0.43), "PanelWhite")
    b.box((0.38, 0.18, 0.4), (0, 0.24, 0.6), "Ceramic")
    b.box((0.4, 0.2, 0.03), (0, 0.24, 0.815), "Ceramic")
    b.box((0.06, 0.02, 0.03), (0.12, 0.14, 0.76), "Steel")
    return b.build()


def makeWashBasin():
    """세면대(1.2 x 0.55 x 0.85): 흰 상판 + 오목한 세면볼 2개 + 수도꼭지, 아래 회색 가림판. 벽은 +Y."""
    b = MeshBuilder("Sub_WashBasin", "Subway", bevel=0.006)
    b.box((1.2, 0.55, 0.06), (0, 0, 0.82), "Ceramic")
    b.box((1.2, 0.04, 0.78), (0, 0.255, 0.39), "PanelGray")
    b.box((1.16, 0.02, 0.2), (0, -0.265, 0.68), "PanelGray")
    for x in (-0.3, 0.3):
        b.cylinder(0.17, 0.12, 0.12, (x, -0.02, 0.77), "PanelWhite", segments=18)
        b.cylinder(0.02, 0.02, 0.12, (x, 0.18, 0.91), "Steel", segments=8)
        b.box((0.03, 0.12, 0.025), (x, 0.13, 0.96), "Steel")
    b.box((1.2, 0.02, 0.7), (0, 0.27, 1.3), "Steel")
    return b.build()


# ---------------------------------------------------------------- 설비

def makeElectricPanel():
    """배전반(1.2 x 0.5 x 2.0): 회색 함, 문 2짝, 환기구, 노란 경고 표시."""
    b = MeshBuilder("Sub_ElectricPanel", "Subway", bevel=0.006)
    b.box((1.2, 0.5, 1.95), (0, 0, 0.975), "PanelGray")
    b.box((1.24, 0.54, 0.06), (0, 0, 1.98), "SteelDark")
    b.box((1.24, 0.54, 0.08), (0, 0, 0.04), "SteelDark")
    for x in (-0.3, 0.3):
        b.box((0.56, 0.015, 1.75), (x, -0.255, 1.0), "PanelGray")
        b.box((0.03, 0.03, 0.2), (x + (0.22 if x < 0 else -0.22), -0.27, 1.05), "SteelDark")
        for z in (1.6, 1.66, 1.72):
            b.box((0.34, 0.02, 0.025), (x, -0.265, z), "SteelDark")
    b.box((0.2, 0.02, 0.18), (-0.3, -0.27, 1.35), "SafetyYellow", rot=rotY(45))
    b.box((0.28, 0.02, 0.07), (0.3, -0.27, 1.38), "PanelWhite")
    return b.build()


def makeAirHandler():
    """공조기(1.6 x 1.0 x 1.6): 회색 상자, 앞면 원형 팬 그릴, 옆 환기구, 위 덕트 연결부."""
    b = MeshBuilder("Sub_AirHandler", "Subway", bevel=0.01)
    b.box((1.6, 1.0, 1.5), (0, 0, 0.8), "PanelGray")
    b.box((1.64, 1.04, 0.1), (0, 0, 0.05), "SteelDark")
    b.cylinder(0.5, 0.5, 0.04, (0.2, -0.51, 0.85), "SteelDark", segments=28, rot=rotX(90))
    b.cylinder(0.44, 0.44, 0.05, (0.2, -0.515, 0.85), "Black", segments=28, rot=rotX(90))
    for i in range(4):
        b.box((0.82, 0.02, 0.03), (0.2, -0.54, 0.85), "Steel", rot=rotY(i * 45))
    b.cylinder(0.08, 0.08, 0.06, (0.2, -0.545, 0.85), "Steel", segments=12, rot=rotX(90))
    for z in (0.4, 0.55, 0.7, 0.85, 1.0, 1.15):
        b.box((0.03, 0.6, 0.04), (0.81, 0, z), "SteelDark")
    b.box((0.4, 0.02, 0.6), (-0.55, -0.51, 0.8), "PanelWhite")
    b.box((0.6, 0.6, 0.3), (-0.2, 0.1, 1.7), "Steel")
    return b.build()


builders = [
    makeBench, makeRecycleBin, makeTicketMachine, makeFireCabinet, makeInfoBoard, makeAdBoard,
    makeCoinLocker, makeEmergencyBox, makeStanchion, makeWetFloorSign, makePlanter, makeSuitcase,
    makeCleaningCart, makeOfficeDesk, makeOfficeChair, makeMonitor, makeToilet, makeWashBasin,
    makeElectricPanel, makeAirHandler,
]


def main():
    """지하철 소품을 만들고 FBX/블렌드/미리보기를 저장한다."""
    resetScene()
    created = [fn() for fn in builders]
    for obj in created:
        exportFbx(obj, outDir)
        print("[SubwayProps] exported", obj.name)

    layoutGrid(created, (0, 0, 0), 5, 2.8)
    if previewDir:
        os.makedirs(previewDir, exist_ok=True)
        renderPreview(created, os.path.join(previewDir, "preview_subway.png"))
        renderPreview(created, os.path.join(previewDir, "preview_subway_back.png"), azimuth=210)
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=blendPath)
    print("[SubwayProps] done:", len(created), "objects")


main()
