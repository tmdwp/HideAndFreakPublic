"""
지하철역 소품(MapKit Subway) 생성 스크립트. 낡은 공포 에셋 대신 깨끗한 역 분위기(스테인리스·흰 플라스틱·노선 색)로 만든다.

실행:
  blender -b --python generate_subway_props.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 바닥 중앙, 앞면은 블렌더 -Y = Unity +Z)
발광 머티리얼(Screen, LightPanel, AdPanel)은 Unity 맵 빌더가 이름으로 찾아 발광 머티리얼로 바꿔 끼운다.
디테일(볼트·손잡이·라벨·슬릿)은 MeshBuilder의 면 디테일 도우미로 붙여 베벨을 받지 않게 한다(변신 사물로 많이 깔려 폴리곤을 아낀다).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import MeshBuilder, configure, exportFbx, layoutGrid, renderPreview, resetScene, rotX, rotY, rotZ, surfaceOf

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
    """스테인리스 3인용 벤치(1.8 x 0.55, 좌판 0.45, 등받이 0.85). 등받이는 +Y, 좌석 사이 팔걸이. 좌판·등받이에 타공 줄, 발판 고정 볼트."""
    b = MeshBuilder("Sub_Bench", "Subway", bevel=0.006)
    backRot = rotX(-8)
    for x in (-0.6, 0, 0.6):
        b.box((0.56, 0.42, 0.04), (x, -0.02, 0.45), "Steel")
        b.box((0.56, 0.04, 0.38), (x, 0.2, 0.66), "Steel", rot=backRot)
        for v in (-0.12, -0.04, 0.04, 0.12):
            b.plate("+Z", (x, -0.02, 0.47), 0.44, 0.012, 0.002, "SteelDark", v=v)
        backFront = surfaceOf((x, 0.2, 0.66), backRot, 0.02)
        for v in (-0.1, -0.03, 0.04, 0.11):
            b.plate(backRot, backFront, 0.44, 0.012, 0.002, "SteelDark", v=v)
    for x in (-0.9, -0.3, 0.3, 0.9):
        b.box((0.04, 0.4, 0.05), (x, 0.0, 0.62), "SteelDark")
        b.box((0.04, 0.04, 0.17), (x, -0.18, 0.54), "SteelDark")
    for x in (-0.7, 0.7):
        b.box((0.06, 0.06, 0.43), (x, -0.12, 0.215), "SteelDark")
        b.box((0.06, 0.06, 0.85), (x, 0.2, 0.425), "SteelDark")
        b.box((0.08, 0.45, 0.04), (x, 0.04, 0.42), "SteelDark")
        b.box((0.16, 0.5, 0.02), (x, 0.04, 0.01), "SteelDark")
        b.bolts("+Z", (x, 0.04, 0.02), [(-0.05, -0.2), (0.05, -0.2), (-0.05, 0.2), (0.05, 0.2)], "Steel", radius=0.01, depth=0.006)
    b.box((1.6, 0.04, 0.04), (0, 0.2, 0.3), "SteelDark")
    return b.build()


def makeRecycleBin():
    """분리수거함(1.0 x 0.45 x 1.0): 스테인리스 몸통, 위 투입구 3개(테두리), 앞면 색 띠·아이콘 판·글씨, 문 열쇠 구멍."""
    b = MeshBuilder("Sub_RecycleBin", "Subway", bevel=0.008)
    b.box((1.0, 0.45, 0.92), (0, 0, 0.46), "Steel")
    b.box((1.04, 0.49, 0.06), (0, 0, 0.95), "SteelDark")
    front = (0, -0.231, 0)
    for x, color in ((-0.33, "PanelGray"), (0, "LineBlue"), (0.33, "SafetyYellow")):
        b.box((0.22, 0.14, 0.02), (x, -0.06, 0.985), "Black")
        b.frame("+Z", (x, -0.06, 0.98), 0.27, 0.19, 0.025, 0.012, "Steel")
        b.box((0.3, 0.01, 0.16), (x, -0.226, 0.76), color)
        b.plate("-Y", front, 0.09, 0.09, 0.004, "PanelWhite", u=x, v=0.76)
        b.textBars("-Y", (0, -0.225, 0), 0.18, 0.018, (1.0, 0.6), "SteelDark", u=x, v=0.62)
        b.plate("-Y", (0, -0.225, 0), 0.12, 0.022, 0.006, "SteelDark", u=x, v=0.87)
        b.bolts("-Y", (0, -0.225, 0), [(x + 0.11, 0.5)], "Black", radius=0.012, depth=0.004)
        b.box((0.004, 0.01, 0.8), (x + 0.165, -0.226, 0.44), "SteelDark")
    b.box((1.0, 0.45, 0.04), (0, 0, 0.02), "Rubber")
    return b.build()


def makeTicketMachine():
    """교통카드 발매기(0.9 x 0.6 x 1.75): 흰 몸통, 기울어진 화면, 카드 패드·숫자 키패드·지폐구·받는 곳, 옆 환기구, 위 초록 머리판과 이름판."""
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
    body = (0, -0.3, 0)
    # 머리판 이름판과 위 표시등
    b.plate("-Y", (0, -0.31, 0), 0.5, 0.08, 0.004, "PanelWhite", v=1.675)
    b.textBars("-Y", (0, -0.314, 0), 0.36, 0.022, (1.0,), "Black", v=1.675)
    b.plate("+Z", (0, 0, 1.75), 0.6, 0.04, 0.01, "LightPanel", v=-0.26)
    # 받침대 위 카드 패드
    b.plate("+Z", (0.22, -0.33, 0.955), 0.14, 0.09, 0.006, "LineGreen")
    b.frame("+Z", (0.22, -0.33, 0.955), 0.16, 0.11, 0.012, 0.008, "Black")
    # 숫자 키패드(3 x 4)
    b.plate("-Y", body, 0.17, 0.22, 0.006, "PanelGray", u=-0.24, v=0.68)
    for row in range(4):
        for col in range(3):
            b.plate("-Y", (0, -0.306, 0), 0.035, 0.032, 0.008, "Steel", u=-0.29 + col * 0.05, v=0.755 - row * 0.05)
    # 지폐 투입구·동전 구멍
    b.frame("-Y", body, 0.2, 0.06, 0.015, 0.02, "SteelDark", u=0.2, v=0.7)
    b.plate("-Y", body, 0.17, 0.012, 0.004, "Black", u=0.2, v=0.7)
    b.plate("-Y", body, 0.05, 0.012, 0.012, "Black", u=0.2, v=0.62)
    # 옆 환기구
    for facing, cx in (("+X", 0.45), ("-X", -0.45)):
        b.louver(facing, (cx, 0, 0), 0.3, 0.2, 6, "PanelGray", u=0.1 if facing == "+X" else -0.1, v=0.35)
    return b.build()


def makeFireCabinet():
    """옥내 소화전함(0.8 x 0.3 x 1.5): 빨간 몸통, 흰 테두리 앞문 2짝(경첩·손잡이), 이름판, 위 표시등과 받침판, 아래 걸레받이."""
    b = MeshBuilder("Sub_FireCabinet", "Subway", bevel=0.008)
    b.box((0.8, 0.3, 1.5), (0, 0, 0.75), "FireRed")
    b.box((0.7, 0.02, 0.62), (0, -0.155, 1.05), "PanelWhite")
    b.box((0.62, 0.025, 0.54), (0, -0.16, 1.05), "FireRed")
    b.box((0.7, 0.02, 0.5), (0, -0.155, 0.4), "PanelWhite")
    b.box((0.62, 0.025, 0.42), (0, -0.16, 0.4), "FireRed")
    doorFront = (0, -0.1725, 0)
    b.handle("-Y", doorFront, 0.14, "Steel", u=0.27, v=1.05, standoff=0.025, thickness=0.018)
    b.handle("-Y", doorFront, 0.12, "Steel", u=0.27, v=0.4, standoff=0.025, thickness=0.018)
    b.hinges("-Y", (0, -0.165, 0), -0.355, (0.85, 1.25, 0.25, 0.55), "Steel", radius=0.01, length=0.06)
    # 위 문 이름판(흰 판에 빨간 글씨), 아래 문 흰 글씨
    b.plate("-Y", doorFront, 0.36, 0.1, 0.004, "PanelWhite", v=1.22)
    b.textBars("-Y", (0, -0.1765, 0), 0.26, 0.03, (1.0,), "FireRed", v=1.22)
    b.textBars("-Y", doorFront, 0.3, 0.025, (1.0, 0.7), "PanelWhite", v=0.46)
    b.plate("-Y", (0, -0.15, 0), 0.16, 0.16, 0.012, "SteelDark", v=1.42)
    b.cylinder(0.05, 0.05, 0.04, (0, -0.17, 1.42), "LightPanel", segments=14, rot=rotX(90))
    b.plate("-Y", (0, -0.15, 0), 0.8, 0.06, 0.008, "SteelDark", v=0.03)
    return b.build()


def makeInfoBoard():
    """노선도 안내판(1.2 x 0.25 x 2.0): 스테인리스 틀, 양면 발광판에 노선 색 띠·역 점·현위치, 머리판 글씨, 틀·받침 볼트."""
    b = MeshBuilder("Sub_InfoBoard", "Subway", bevel=0.006)
    b.box((1.2, 0.25, 0.08), (0, 0, 0.04), "SteelDark")
    for x in (-0.56, 0.56):
        b.box((0.08, 0.2, 1.95), (x, 0, 1.0), "Steel")
    b.box((1.2, 0.2, 0.08), (0, 0, 1.96), "Steel")
    b.box((1.04, 0.14, 1.5), (0, 0, 1.12), "LightPanel")
    diagonal = Vector((math.cos(math.radians(25)), -math.sin(math.radians(25))))
    stations = [(-0.4, 1.5), (0.1, 1.5), (0.35, 1.5), (-0.15, 1.3), (-0.15, 0.75)]
    stations += [(0.05 + t * diagonal.x, 0.9 + t * diagonal.y) for t in (-0.25, 0.25)]
    transfers = [(-0.15, 1.5), (-0.15, 1.0)]
    for side in (-1, 1):
        y = side * 0.072
        facing = "-Y" if side < 0 else "+Y"
        mirror = 1 if side < 0 else -1
        b.box((0.9, 0.006, 0.05), (0, y, 1.5), "LineGreen")
        b.box((0.05, 0.006, 0.9), (-0.15, y, 1.1), "LineBlue")
        b.box((0.7, 0.006, 0.05), (0.05, y, 0.9), "LineBlue", rot=rotY(25))
        b.box((0.9, 0.006, 0.08), (0, y, 1.78), "Black")
        surface = (0, side * 0.075, 0)
        with b.noBevel():
            ringRot = rotX(90) if side < 0 else rotX(-90)
            for x, z in stations:
                b.cylinder(0.022, 0.022, 0.004, (x, side * 0.077, z), "PanelWhite", segments=8, rot=ringRot)
            for x, z in transfers:
                b.cylinder(0.034, 0.034, 0.004, (x, side * 0.077, z), "Black", segments=10, rot=ringRot)
                b.cylinder(0.022, 0.022, 0.004, (x, side * 0.079, z), "PanelWhite", segments=8, rot=ringRot)
            b.cylinder(0.03, 0.03, 0.004, (0.1, side * 0.079, 1.5), "FireRed", segments=10, rot=ringRot)
        b.textBars(facing, surface, 0.5, 0.022, (1.0,), "PanelWhite", u=mirror * -0.15, v=1.79)
        b.textBars(facing, surface, 0.3, 0.018, (1.0, 0.7, 0.85), "SteelDark", u=mirror * 0.28, v=0.66)
        b.bolts(facing, (0, side * 0.1, 0), [(-0.56, 1.96), (0.56, 1.96), (-0.56, 0.3), (0.56, 0.3)], "SteelDark", radius=0.012)
    b.bolts("+Z", (0, 0, 0.08), [(-0.5, -0.08), (-0.5, 0.08), (0.5, -0.08), (0.5, 0.08)], "Steel", radius=0.014)
    return b.build()


def makeAdBoard():
    """광고 조명판(1.6 x 0.15 x 2.0): 두 다리 위 발광 광고면(양면 그림·글씨), 아래 가로대와 받침 볼트."""
    b = MeshBuilder("Sub_AdBoard", "Subway", bevel=0.006)
    for x in (-0.75, 0.75):
        b.box((0.06, 0.1, 2.0), (x, 0, 1.0), "SteelDark")
        b.box((0.2, 0.4, 0.04), (x, 0, 0.02), "SteelDark")
        b.bolts("+Z", (x, 0, 0.04), [(0, -0.14), (0, 0.14)], "Steel", radius=0.014)
    b.box((1.44, 0.04, 0.05), (0, 0, 0.3), "SteelDark")
    b.box((1.5, 0.12, 1.1), (0, 0, 1.35), "Steel")
    b.box((1.38, 0.13, 0.98), (0, 0, 1.35), "AdPanel")
    b.box((1.38, 0.132, 0.12), (0, 0, 1.0), "LineGreen")
    for side in (-1, 1):
        facing = "-Y" if side < 0 else "+Y"
        mirror = 1 if side < 0 else -1
        surface = (0, side * 0.065, 0)
        with b.noBevel():
            b.cylinder(0.17, 0.17, 0.006, (mirror * -0.38, side * 0.067, 1.45), "FireRed", segments=16,
                       rot=rotX(90) if side < 0 else rotX(-90))
        b.textBars(facing, surface, 0.6, 0.05, (1.0, 0.75), "Black", u=mirror * 0.3, v=1.66)
        b.textBars(facing, surface, 0.5, 0.025, (1.0, 0.8, 0.6), "SteelDark", u=mirror * 0.3, v=1.38)
        b.textBars(facing, (0, side * 0.066, 0), 0.4, 0.03, (1.0,), "PanelWhite", u=mirror * -0.3, v=1.0)
        b.cornerBolts(facing, (0, side * 0.06, 1.35), 1.5, 1.1, "SteelDark", inset=0.03, radius=0.01)
    return b.build()


def makeCoinLocker():
    """물품보관함(1.5 x 0.6 x 1.9): 3열 x 4단 문(번호판·열쇠 구멍·환기 슬릿), 가운데 아래 결제 화면·카드 투입구, 위 노선 색 머리판 글씨, 아래 환기구."""
    b = MeshBuilder("Sub_CoinLocker", "Subway", bevel=0.005)
    b.box((1.5, 0.6, 1.9), (0, 0, 0.95), "PanelGray")
    b.box((1.52, 0.62, 0.15), (0, 0, 1.835), "LineBlue")
    doorFront = (0, -0.315, 0)
    for col, x in enumerate((-0.5, 0, 0.5)):
        for row, z in enumerate((0.3, 0.68, 1.06, 1.44)):
            if col == 1 and row == 0:
                b.box((0.46, 0.02, 0.34), (x, -0.305, z), "PanelWhite")
                b.box((0.28, 0.02, 0.18), (x, -0.315, z + 0.03), "Screen")
                b.frame("-Y", doorFront, 0.32, 0.22, 0.02, 0.008, "Black", u=x, v=z + 0.03)
                b.plate("-Y", doorFront, 0.12, 0.012, 0.006, "Black", u=x - 0.07, v=z - 0.12)
                b.plate("-Y", doorFront, 0.07, 0.05, 0.006, "LineBlue", u=x + 0.1, v=z - 0.12)
                continue
            b.box((0.46, 0.02, 0.34), (x, -0.305, z), "PanelWhite")
            b.box((0.04, 0.03, 0.06), (x + 0.17, -0.32, z), "SteelDark")
            b.plate("-Y", doorFront, 0.07, 0.035, 0.003, "Black", u=x - 0.16, v=z + 0.12)
            b.bolts("-Y", doorFront, [(x + 0.17, z - 0.06)], "Black", radius=0.009, depth=0.003)
            for dv in (-0.04, 0.0, 0.04):
                b.plate("-Y", doorFront, 0.16, 0.012, 0.002, "PanelGray", u=x - 0.04, v=z - 0.08 + dv)
    b.textBars("-Y", (0, -0.31, 0), 0.6, 0.035, (1.0,), "PanelWhite", v=1.835)
    b.box((1.5, 0.6, 0.1), (0, 0, 0.05), "SteelDark")
    b.louver("-Y", (0, -0.3, 0), 1.2, 0.07, 3, "Black", depth=0.01, v=0.05)
    return b.build()


def makeEmergencyBox():
    """비상용품함(방독면함, 0.6 x 0.35 x 0.9): 짙은 녹색 몸통, 노란 띠 글씨, 흰 십자 창(스테인리스 틀), 손잡이·경첩."""
    b = MeshBuilder("Sub_EmergencyBox", "Subway", bevel=0.008)
    b.box((0.6, 0.35, 0.9), (0, 0, 0.45), "LineGreen")
    b.box((0.62, 0.36, 0.07), (0, 0, 0.875), "SafetyYellow")
    b.box((0.5, 0.02, 0.5), (0, -0.18, 0.45), "PanelWhite")
    b.box((0.08, 0.025, 0.28), (0, -0.19, 0.45), "FireRed")
    b.box((0.28, 0.025, 0.08), (0, -0.19, 0.45), "FireRed")
    front = (0, -0.175, 0)
    b.frame("-Y", (0, -0.19, 0), 0.52, 0.52, 0.025, 0.01, "Steel", v=0.45)
    b.textBars("-Y", (0, -0.18, 0), 0.4, 0.025, (1.0,), "Black", v=0.875)
    b.handle("-Y", front, 0.18, "Steel", vertical=False, standoff=0.025, thickness=0.016, v=0.12)
    b.hinges("-Y", front, -0.29, (0.3, 0.62), "SteelDark", radius=0.01, length=0.06)
    return b.build()


def makeStanchion():
    """줄서기 차단봉 한 쌍(폭 1.6, 높이 0.95): 둥근 받침 + 기둥 + 사이 띠. 왼쪽 띠 감개 통, 오른쪽 띠 고리, 기둥 밑 칼라."""
    b = MeshBuilder("Sub_Stanchion", "Subway", bevel=0.003)
    for x in (-0.8, 0.8):
        b.cylinder(0.17, 0.17, 0.04, (x, 0, 0.02), "SteelDark", segments=18)
        b.cylinder(0.03, 0.03, 0.9, (x, 0, 0.47), "Steel", segments=12)
        b.cylinder(0.04, 0.04, 0.06, (x, 0, 0.93), "Steel", segments=12)
        with b.noBevel():
            b.cylinder(0.05, 0.04, 0.05, (x, 0, 0.065), "Steel", segments=12)
            b.cylinder(0.175, 0.175, 0.008, (x, 0, 0.004), "Rubber", segments=18)
    with b.noBevel():
        b.cylinder(0.048, 0.048, 0.1, (-0.8, 0, 0.87), "SteelDark", segments=12)
        b.box((0.04, 0.03, 0.08), (0.765, 0, 0.88), "Black")
    b.box((1.56, 0.012, 0.06), (0, 0, 0.88), "LineBlue")
    return b.build()


def makeWetFloorSign():
    """'바닥 미끄럼 주의' A형 표지(0.3 x 0.32 x 0.62): 양면 그림판·글씨·윗 띠, 경첩 핀, 고무 발."""
    b = MeshBuilder("Sub_WetFloorSign", "Subway", bevel=0.004)
    for side in (-1, 1):
        panelRot = rotX(side * 14)
        b.box((0.3, 0.02, 0.62), (0, side * 0.08, 0.3), "SafetyYellow", rot=panelRot)
        b.box((0.16, 0.025, 0.16), (0, side * 0.085, 0.34), "Black", rot=panelRot)
        outward = panelRot @ (rotZ(180) if side > 0 else Matrix())
        surface = surfaceOf((0, side * 0.08, 0.3), outward, 0.01)
        b.textBars(outward, surface, 0.2, 0.022, (1.0, 0.65), "Black", v=-0.1)
        b.plate(outward, surface, 0.3, 0.03, 0.003, "Black", v=0.24)
        b.plate(outward, surface, 0.3, 0.025, 0.003, "Black", v=-0.27)
        with b.noBevel():
            b.box((0.05, 0.03, 0.02), (0, side * 0.155, 0.01), "Rubber")
    with b.noBevel():
        b.cylinder(0.008, 0.008, 0.34, (0, 0, 0.6), "SteelDark", segments=6, rot=rotY(90))
    b.box((0.08, 0.06, 0.04), (0, 0, 0.6), "SafetyYellow")
    return b.build()


def makePlanter():
    """사각 화분(0.6 x 0.6, 높이 1.2): 흰 화분(세로 홈·받침) + 흙 + 둥근 잎 덩어리 다섯."""
    b = MeshBuilder("Sub_Planter", "Subway", bevel=0.006)
    b.box((0.6, 0.6, 0.5), (0, 0, 0.25), "PanelWhite")
    b.box((0.62, 0.62, 0.04), (0, 0, 0.5), "PanelGray")
    b.box((0.52, 0.52, 0.02), (0, 0, 0.49), "Soil")
    b.box((0.62, 0.62, 0.04), (0, 0, 0.02), "PanelGray")
    for facing, center in (("-Y", (0, -0.3, 0)), ("+Y", (0, 0.3, 0)), ("-X", (-0.3, 0, 0)), ("+X", (0.3, 0, 0))):
        for u in (-0.15, 0.15):
            b.plate(facing, center, 0.012, 0.38, 0.004, "PanelGray", u=u, v=0.26)
    b.cylinder(0.03, 0.03, 0.3, (0, 0, 0.62), "Soil", segments=8)
    b.sphere(0.32, (0, 0, 0.88), "Plant", segments=14, scale=(1.0, 1.0, 0.95))
    b.sphere(0.2, (0.16, -0.1, 1.05), "Plant", segments=12)
    b.sphere(0.18, (-0.15, 0.12, 1.0), "Plant", segments=12)
    b.sphere(0.15, (-0.06, -0.2, 1.1), "Plant", segments=10)
    b.sphere(0.16, (0.2, 0.16, 0.84), "Plant", segments=10)
    return b.build()


def makeSuitcase():
    """여행 가방(0.45 x 0.28 x 0.7 + 손잡이): 파란 몸통, 모서리 띠, 지퍼 줄, 모서리 보호대, 옆 손잡이·이름표, 바퀴."""
    b = MeshBuilder("Sub_Suitcase", "Subway", bevel=0.02)
    b.box((0.45, 0.26, 0.62), (0, 0, 0.36), "Suitcase")
    for x in (-0.12, 0.12):
        b.box((0.03, 0.27, 0.6), (x, 0, 0.36), "SteelDark")
        b.cylinder(0.025, 0.025, 0.24, (x, 0.08, 0.79), "Steel", segments=8)
        b.cylinder(0.025, 0.025, 0.02, (x + (0.08 if x > 0 else -0.08), 0.1, 0.025), "Rubber", segments=10, rot=rotY(90))
    b.box((0.28, 0.04, 0.03), (0, 0.08, 0.92), "Black")
    with b.noBevel():
        # 지퍼: 몸통 옆 둘레를 도는 얇은 띠
        b.box((0.43, 0.012, 0.006), (0, 0.0, 0.672), "Black")
        for x in (-0.227, 0.227):
            b.box((0.006, 0.012, 0.58), (x, 0.0, 0.36), "Black")
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.box((0.05, 0.05, 0.05), (sx * 0.205, sy * 0.115, 0.075), "Rubber")
    b.handle("+X", (0.225, 0, 0), 0.14, "Black", vertical=False, standoff=0.025, thickness=0.018, v=0.45)
    b.plate("+X", (0.225, 0, 0), 0.04, 0.07, 0.004, "SafetyYellow", u=-0.04, v=0.38)
    return b.build()


def makeCleaningCart():
    """청소 카트(0.95 x 0.5 x 1.0): 노란 틀, 회색 수거 주머니(테두리·경고 표지), 양동이, 분무기, 고무 손잡이, 바퀴 허브."""
    b = MeshBuilder("Sub_CleaningCart", "Subway", bevel=0.006)
    b.box((0.95, 0.5, 0.06), (0, 0, 0.12), "SafetyYellow")
    b.box((0.95, 0.5, 0.04), (0, 0, 0.62), "SafetyYellow")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.04, 0.04, 0.6), (sx * 0.45, sy * 0.23, 0.38), "SafetyYellow")
            b.cylinder(0.04, 0.04, 0.03, (sx * 0.42, sy * 0.2, 0.04), "Rubber", segments=10, rot=rotX(90))
            with b.noBevel():
                b.cylinder(0.018, 0.018, 0.034, (sx * 0.42, sy * 0.2, 0.04), "Steel", segments=6, rot=rotX(90))
    b.box((0.4, 0.42, 0.5), (0.22, 0, 0.4), "PanelGray")
    b.frame("+Z", (0.22, 0, 0.65), 0.42, 0.44, 0.03, 0.02, "SteelDark")
    b.plate("-Y", (0, -0.21, 0), 0.16, 0.16, 0.004, "SafetyYellow", u=0.22, v=0.38, tilt=0)
    b.textBars("-Y", (0, -0.214, 0), 0.11, 0.018, (1.0, 0.7), "Black", u=0.22, v=0.41)
    b.hollowCylinder(0.14, 0.17, 0.28, 0.01, (-0.25, 0, 0.8), "LineBlue", floor=0.015, segments=16)
    b.cylinder(0.015, 0.015, 0.9, (-0.1, 0.1, 1.0), "Steel", segments=8, rot=rotX(-10))
    with b.noBevel():
        # 분무기: 몸통 + 목 + 방아쇠 머리
        b.cylinder(0.035, 0.035, 0.16, (0.0, -0.16, 0.72), "LineGreen", segments=10)
        b.cylinder(0.015, 0.015, 0.03, (0.0, -0.16, 0.815), "PanelWhite", segments=8)
        b.box((0.03, 0.07, 0.035), (0.0, -0.175, 0.845), "PanelWhite")
    b.box((0.04, 0.5, 0.04), (0.5, 0, 0.98), "SteelDark")
    with b.noBevel():
        b.box((0.05, 0.32, 0.05), (0.5, 0, 0.98), "Rubber")
    for sy in (-1, 1):
        b.box((0.04, 0.04, 0.36), (0.5, sy * 0.23, 0.8), "SteelDark")
    return b.build()


# ---------------------------------------------------------------- 역무실·화장실

def makeOfficeDesk():
    """사무 책상(1.4 x 0.7 x 0.74): 밝은 상판(모서리 띠·전선 구멍), 흰 옆판, 오른쪽 서랍(열쇠), 위 키보드·마우스·서류. 앉는 쪽은 +Y."""
    b = MeshBuilder("Sub_OfficeDesk", "Subway", bevel=0.004)
    b.box((1.4, 0.7, 0.03), (0, 0, 0.725), "Laminate")
    b.box((0.03, 0.66, 0.71), (-0.68, 0, 0.355), "PanelWhite")
    b.box((0.4, 0.62, 0.71), (0.48, 0, 0.355), "PanelWhite")
    for z in (0.17, 0.4, 0.6):
        b.box((0.36, 0.02, 0.18), (0.48, 0.32, z), "PanelGray")
        b.box((0.12, 0.025, 0.02), (0.48, 0.335, z + 0.05), "SteelDark")
    b.box((1.1, 0.02, 0.4), (-0.1, -0.3, 0.45), "PanelWhite")
    for facing, y in (("+Y", 0.35), ("-Y", -0.35)):
        b.plate(facing, (0, y, 0), 1.4, 0.03, 0.003, "PanelGray", v=0.725)
    b.bolts("+Y", (0, 0.33, 0), [(-0.6, 0.6)], "Steel", radius=0.008, depth=0.006)
    with b.noBevel():
        b.cylinder(0.03, 0.03, 0.004, (-0.5, -0.25, 0.742), "Black", segments=10)
        # 키보드(키 줄 두 개)·마우스·서류 더미
        b.box((0.44, 0.14, 0.02), (-0.1, 0.15, 0.75), "Black")
        for dy in (-0.03, 0.02):
            b.box((0.4, 0.035, 0.006), (-0.1, 0.15 + dy, 0.763), "SteelDark")
        b.box((0.06, 0.1, 0.03), (0.22, 0.15, 0.755), "Black")
        b.box((0.21, 0.3, 0.025), (-0.5, 0.02, 0.7525), "PanelWhite", rot=rotZ(8))
    return b.build()


def makeOfficeChair():
    """사무 의자: 별 모양 다리 + 가스 실린더(덮개) + 좌판 아래 기구 + 남색 좌판·등받이(+Y) + 팔걸이."""
    b = MeshBuilder("Sub_OfficeChair", "Subway", bevel=0.01)
    for i in range(5):
        a = i * 72
        b.box((0.3, 0.05, 0.04), (0.15 * math.cos(math.radians(a)), 0.15 * math.sin(math.radians(a)), 0.08), "Black", rot=rotZ(a))
        b.sphere(0.03, (0.29 * math.cos(math.radians(a)), 0.29 * math.sin(math.radians(a)), 0.03), "Black", segments=8)
    b.cylinder(0.03, 0.03, 0.35, (0, 0, 0.27), "Steel", segments=10)
    b.box((0.5, 0.48, 0.08), (0, 0, 0.48), "Fabric")
    b.box((0.46, 0.07, 0.5), (0, 0.24, 0.8), "Fabric", rot=rotX(-8))
    b.box((0.05, 0.05, 0.3), (0, 0.24, 0.55), "Black")
    with b.noBevel():
        b.cylinder(0.042, 0.042, 0.14, (0, 0, 0.17), "Black", segments=10)
        b.box((0.22, 0.22, 0.05), (0, 0, 0.415), "Black")
        for x in (-0.27, 0.27):
            b.box((0.035, 0.05, 0.2), (x, 0.02, 0.6), "Black")
            b.box((0.05, 0.24, 0.03), (x, 0.0, 0.715), "Black")
            b.box((0.035, 0.2, 0.025), (x * 0.95, 0.02, 0.49), "Black")
    return b.build()


def makeMonitor():
    """평면 모니터(0.55 x 0.2 x 0.45): 받침 + 목 + 얇은 화면(앞 -Y), 아래 턱 로고·전원 버튼, 뒤 볼록 덮개·환기구."""
    b = MeshBuilder("Sub_Monitor", "Subway", bevel=0.003)
    b.box((0.24, 0.18, 0.015), (0, 0, 0.008), "Black")
    b.box((0.04, 0.03, 0.12), (0, 0.04, 0.07), "Black")
    b.box((0.55, 0.035, 0.33), (0, 0.02, 0.28), "Black")
    b.box((0.51, 0.005, 0.29), (0, 0.0, 0.28), "Screen")
    front = (0, 0.0025, 0)
    b.plate("-Y", front, 0.04, 0.006, 0.002, "Steel", v=0.125)
    b.bolts("-Y", front, [(0.23, 0.125)], "Steel", radius=0.005, depth=0.002)
    with b.noBevel():
        b.box((0.3, 0.035, 0.2), (0, 0.055, 0.27), "Black")
    b.louver("+Y", (0, 0.0725, 0), 0.2, 0.06, 4, "SteelDark", depth=0.004, v=0.33)
    return b.build()


def makeToilet():
    """양변기(0.4 x 0.7 x 0.8): 흰 도기, 물탱크는 +Y. 속이 빈 그릇 위 고리 시트, 세운 뚜껑과 경첩, 탱크 위 물 내림 버튼, 바닥 볼트 덮개."""
    b = MeshBuilder("Sub_Toilet", "Subway", bevel=0.004)
    b.cylinder(0.12, 0.15, 0.3, (0, -0.05, 0.15), "Ceramic", segments=18)
    oval = Matrix.Diagonal((1.0, 1.2, 1.0, 1.0))
    b.hollowCylinder(0.13, 0.17, 0.16, 0.02, (0, -0.1, 0.34), "Ceramic", floor=0.03, segments=20, rot=oval)
    b.hollowCylinder(0.18, 0.18, 0.025, 0.06, (0, -0.1, 0.4325), "PanelWhite", segments=20, rot=oval)
    b.box((0.38, 0.18, 0.4), (0, 0.24, 0.6), "Ceramic")
    b.box((0.4, 0.2, 0.03), (0, 0.24, 0.815), "Ceramic")
    b.box((0.06, 0.02, 0.03), (0.12, 0.14, 0.76), "Steel")
    with b.noBevel():
        b.cylinder(0.15, 0.15, 0.02, (0, 0.13, 0.62), "PanelWhite", segments=18,
                   rot=rotX(80) @ Matrix.Diagonal((1.0, 1.2, 1.0, 1.0)))
        for x in (-0.09, 0.09):
            b.cylinder(0.012, 0.012, 0.05, (x, 0.1, 0.45), "Steel", segments=8, rot=rotY(90))
            b.sphere(0.02, (x * 1.5, -0.05, 0.005), "Ceramic", segments=8, scale=(1.0, 1.0, 0.7))
        b.cylinder(0.028, 0.028, 0.008, (0, 0.24, 0.834), "Steel", segments=12)
    return b.build()


def makeWashBasin():
    """세면대(1.2 x 0.55 x 0.85): 흰 상판 + 오목한 세면볼 2개(배수구) + 수도꼭지(레버), 비누 디스펜서, 아래 회색 가림판, 틀 두른 거울. 벽은 +Y."""
    b = MeshBuilder("Sub_WashBasin", "Subway", bevel=0.006)
    # 상판: 세면볼 두 개 자리(0.34 x 0.3)를 비운 틀
    b.box((1.2, 0.04, 0.78), (0, 0.255, 0.39), "PanelGray")
    b.box((1.16, 0.02, 0.2), (0, -0.265, 0.68), "PanelGray")
    b.box((1.2, 0.105, 0.06), (0, -0.2225, 0.82), "Ceramic")
    b.box((1.2, 0.145, 0.06), (0, 0.2025, 0.82), "Ceramic")
    for x0, x1 in ((-0.6, -0.47), (-0.13, 0.13), (0.47, 0.6)):
        b.box((x1 - x0, 0.3, 0.06), ((x0 + x1) / 2, -0.02, 0.82), "Ceramic")
    for x in (-0.3, 0.3):
        b.hollowBox((0.34, 0.3, 0.16), 0.015, (x, -0.02, 0.77), "PanelWhite")
        b.cylinder(0.02, 0.02, 0.12, (x, 0.18, 0.91), "Steel", segments=8)
        b.box((0.03, 0.12, 0.025), (x, 0.13, 0.96), "Steel")
        with b.noBevel():
            b.cylinder(0.025, 0.025, 0.004, (x, -0.02, 0.707), "Steel", segments=10)
            b.box((0.015, 0.07, 0.012), (x, 0.2, 0.976), "Steel", rot=rotX(-20))
    with b.noBevel():
        b.box((0.08, 0.06, 0.13), (0, 0.2, 0.915), "PanelWhite")
        b.box((0.02, 0.05, 0.015), (0, 0.16, 0.95), "Steel")
    b.box((1.2, 0.02, 0.7), (0, 0.27, 1.3), "Steel")
    b.frame("-Y", (0, 0.26, 0), 1.2, 0.7, 0.03, 0.012, "SteelDark", v=1.3)
    return b.build()


# ---------------------------------------------------------------- 설비

def makeElectricPanel():
    """배전반(1.2 x 0.5 x 2.0): 회색 함, 문 2짝(손잡이·열쇠·경첩), 위아래 환기구, 노란 경고 표시, 명판(볼트), 옆 환기구."""
    b = MeshBuilder("Sub_ElectricPanel", "Subway", bevel=0.006)
    b.box((1.2, 0.5, 1.95), (0, 0, 0.975), "PanelGray")
    b.box((1.24, 0.54, 0.06), (0, 0, 1.98), "SteelDark")
    b.box((1.24, 0.54, 0.08), (0, 0, 0.04), "SteelDark")
    door = (0, -0.2625, 0)
    for x in (-0.3, 0.3):
        inner = 0.22 if x < 0 else -0.22
        b.box((0.56, 0.015, 1.75), (x, -0.255, 1.0), "PanelGray")
        b.handle("-Y", door, 0.2, "SteelDark", u=x + inner, v=1.05, standoff=0.025, thickness=0.02)
        b.bolts("-Y", door, [(x + inner, 1.22)], "Black", radius=0.012, depth=0.006)
        b.hinges("-Y", (0, -0.255, 0), x - inner * 1.29, (0.4, 1.0, 1.6), "SteelDark", radius=0.012, length=0.08)
        b.louver("-Y", door, 0.34, 0.14, 4, "SteelDark", u=x, v=1.67)
        b.louver("-Y", door, 0.34, 0.14, 4, "SteelDark", u=x, v=0.3)
    with b.noBevel():
        b.box((0.2, 0.02, 0.18), (-0.3, -0.27, 1.35), "SafetyYellow", rot=rotY(45))
        b.box((0.025, 0.022, 0.1), (-0.3, -0.272, 1.35), "Black", rot=rotY(-20))
    b.plate("-Y", door, 0.28, 0.1, 0.004, "PanelWhite", u=0.3, v=1.38)
    b.textBars("-Y", (0, -0.2665, 0), 0.2, 0.014, (1.0, 0.6), "Black", u=0.3, v=1.4)
    b.cornerBolts("-Y", (0.3, -0.2665, 1.38), 0.28, 0.1, "Steel", inset=0.018, radius=0.006, depth=0.004)
    b.louver("+X", (0.6, 0, 0), 0.3, 0.3, 6, "SteelDark", v=1.6)
    return b.build()


def makeAirHandler():
    """공조기(1.6 x 1.0 x 1.6): 회색 상자, 앞면 원형 팬 그릴(링·볼트), 점검판(볼트·손잡이·명판), 옆 환기구·배관, 위 덕트 플랜지."""
    b = MeshBuilder("Sub_AirHandler", "Subway", bevel=0.01)
    b.box((1.6, 1.0, 1.5), (0, 0, 0.8), "PanelGray")
    b.box((1.64, 1.04, 0.1), (0, 0, 0.05), "SteelDark")
    b.cylinder(0.5, 0.5, 0.04, (0.2, -0.51, 0.85), "SteelDark", segments=28, rot=rotX(90))
    b.cylinder(0.44, 0.44, 0.05, (0.2, -0.515, 0.85), "Black", segments=28, rot=rotX(90))
    for i in range(4):
        b.box((0.82, 0.02, 0.03), (0.2, -0.54, 0.85), "Steel", rot=rotY(i * 45))
    b.cylinder(0.08, 0.08, 0.06, (0.2, -0.545, 0.85), "Steel", segments=12, rot=rotX(90))
    with b.noBevel():
        b.hollowCylinder(0.28, 0.28, 0.015, 0.015, (0.2, -0.545, 0.85), "Steel", segments=24, rot=rotX(90))
    fanFront = (0.2, -0.53, 0.85)
    b.bolts("-Y", fanFront, [(0.47 * math.cos(math.radians(a)), 0.47 * math.sin(math.radians(a))) for a in range(0, 360, 45)],
            "Steel", radius=0.014)
    for z in (0.4, 0.55, 0.7, 0.85, 1.0, 1.15):
        b.box((0.03, 0.6, 0.04), (0.81, 0, z), "SteelDark")
    b.box((0.4, 0.02, 0.6), (-0.55, -0.51, 0.8), "PanelWhite")
    panel = (-0.55, -0.52, 0.8)
    b.cornerBolts("-Y", panel, 0.4, 0.6, "SteelDark", inset=0.03, radius=0.012)
    b.handle("-Y", panel, 0.16, "SteelDark", vertical=False, v=-0.18, standoff=0.025, thickness=0.02)
    b.plate("-Y", panel, 0.24, 0.1, 0.003, "SafetyYellow", v=0.16)
    b.textBars("-Y", (-0.55, -0.523, 0.8), 0.18, 0.016, (1.0, 0.6), "Black", v=0.17)
    b.plate("-Y", (0, -0.5, 0), 0.012, 1.4, 0.004, "SteelDark", u=-0.3, v=0.8)
    with b.noBevel():
        for y in (-0.2, 0.2):
            b.cylinder(0.05, 0.05, 0.1, (-0.85, y, 0.45), "Steel", segments=10, rot=rotY(90))
            b.cylinder(0.075, 0.075, 0.025, (-0.81, y, 0.45), "SteelDark", segments=10, rot=rotY(90))
    b.box((0.6, 0.6, 0.3), (-0.2, 0.1, 1.7), "Steel")
    b.plate("+Z", (-0.2, 0.1, 1.55), 0.72, 0.72, 0.03, "SteelDark")
    b.cornerBolts("+Z", (-0.2, 0.1, 1.58), 0.72, 0.72, "Steel", inset=0.035, radius=0.014)
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
