"""
거대한 방 맵(Map_GiantRoom) 소품 생성 스크립트. 플레이어가 손가락만 해진 것처럼 보이는 50m 방에 쓰는 가구와 작은 물건.
목록과 크기는 Docs/GiantRoomMap.md "오브젝트 모델 목록"과 같다.

모든 모델은 **실제 크기**(미터)로 만든다. Unity 맵 빌더가 10배로 키워 놓는다. (연필 18cm → 1.8m)
- 가구(SM_GR_*): 침대·책상·책장·소파 등. 변신 대상이 아니다.
- 작은 물건(SM_GR_Small_*): 연필·지우개 등. 10배로 키워도 변신 크기 제한(3m) 안이라 변신 사물로 쓴다.

실행:
  blender -b --factory-startup --python generate_giantroom_props.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (피벗은 바닥 중앙, 앞면은 블렌더 -Y = Unity +Z)
발광 부분은 머티리얼 M_GR_Glow / M_GR_Screen 이름으로 찾아 Unity 발광 머티리얼로 바꿔 끼운다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import MeshBuilder, configure, exportFbx, layoutGrid, renderPreview, resetScene, rotX, rotY, rotZ

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models", "GiantRoom")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "Oak":          ((0.62, 0.44, 0.27), 0.0, 0.55, 0.0),
    "OakDark":      ((0.36, 0.24, 0.14), 0.0, 0.6, 0.0),
    "White":        ((0.90, 0.90, 0.88), 0.0, 0.45, 0.0),
    "Cream":        ((0.88, 0.82, 0.70), 0.0, 0.7, 0.0),
    "Gray":         ((0.50, 0.51, 0.53), 0.1, 0.55, 0.0),
    "Charcoal":     ((0.16, 0.16, 0.18), 0.0, 0.6, 0.0),
    "Metal":        ((0.70, 0.71, 0.73), 0.85, 0.3, 0.0),
    "Blanket":      ((0.24, 0.36, 0.58), 0.0, 0.95, 0.0),
    "Pillow":       ((0.94, 0.92, 0.86), 0.0, 0.95, 0.0),
    "Fabric":       ((0.45, 0.20, 0.18), 0.0, 0.95, 0.0),
    "Rug":          ((0.55, 0.42, 0.30), 0.0, 1.0, 0.0),
    "RugTrim":      ((0.30, 0.20, 0.32), 0.0, 1.0, 0.0),
    "Red":          ((0.80, 0.12, 0.10), 0.0, 0.45, 0.0),
    "Blue":         ((0.12, 0.32, 0.75), 0.0, 0.45, 0.0),
    "Green":        ((0.15, 0.55, 0.25), 0.0, 0.5, 0.0),
    "Yellow":       ((0.95, 0.78, 0.12), 0.0, 0.45, 0.0),
    "Orange":       ((0.92, 0.45, 0.10), 0.0, 0.5, 0.0),
    "Pink":         ((0.92, 0.55, 0.62), 0.0, 0.7, 0.0),
    "Purple":       ((0.45, 0.25, 0.65), 0.0, 0.5, 0.0),
    "Graphite":     ((0.10, 0.10, 0.11), 0.4, 0.4, 0.0),
    "Paper":        ((0.95, 0.94, 0.90), 0.0, 0.8, 0.0),
    "Cardboard":    ((0.62, 0.47, 0.30), 0.0, 0.9, 0.0),
    "Ceramic":      ((0.92, 0.93, 0.95), 0.0, 0.2, 0.0),
    "Black":        ((0.05, 0.05, 0.06), 0.0, 0.5, 0.0),
    "Glass":        ((0.20, 0.25, 0.30), 0.3, 0.1, 0.0),
    "Copper":       ((0.72, 0.45, 0.25), 0.9, 0.35, 0.0),
    "AppleRed":     ((0.70, 0.08, 0.08), 0.0, 0.35, 0.0),
    "Glow":         ((1.00, 0.88, 0.65), 0.0, 0.3, 3.0),
    "Screen":       ((0.20, 0.45, 0.85), 0.0, 0.2, 2.0),
}
configure(palette, "M_GR_")


# ---------------------------------------------------------------- 가구 (실제 크기, 변신 안 됨)

def makeBed():
    """싱글 침대(2.0 x 1.4, 매트리스 윗면 0.5, 머리판 1.0): 나무 틀, 매트리스, 이불, 베개 2개. 머리판은 +Y(뒤)."""
    b = MeshBuilder("GR_Bed", "GiantRoom", bevel=0.01)
    b.box((1.4, 2.0, 0.2), (0, 0, 0.2), "Oak")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.07, 0.07, 0.12), (sx * 0.64, sy * 0.94, 0.06), "OakDark")
    b.box((1.36, 1.96, 0.2), (0, 0, 0.4), "White")
    b.box((1.42, 1.4, 0.06), (0, -0.28, 0.52), "Blanket")
    b.box((1.42, 0.04, 0.22), (0, -0.98, 0.42), "Blanket")
    for sx in (-1, 1):
        b.box((0.04, 1.4, 0.22), (sx * 0.71, -0.28, 0.42), "Blanket")
    for x in (-0.33, 0.33):
        b.box((0.55, 0.32, 0.12), (x, 0.78, 0.56), "Pillow")
    b.box((1.46, 0.06, 1.0), (0, 1.0, 0.5), "OakDark")
    b.box((1.46, 0.08, 0.05), (0, 1.0, 1.0), "Oak")
    return b.build()


def makeDrawer(name, centerX, frontY, bottomZ, width, depth, height, frontColor, bodyColor):
    """
    서랍 하나(따로 된 오브젝트): 앞판 + 손잡이 + 위가 열린 상자(바닥·옆판·뒤판). 앞은 -Y.
    가구 몸체의 자식으로 붙여 같은 FBX로 내보내며, Unity에서 앞(+Z)으로 끌어내면 빠진다. 피벗은 원점(몸체와 같음).
    """
    b = MeshBuilder(name, "GiantRoom", bevel=0.003)
    back = frontY + depth
    mid = (frontY + back) / 2
    wall = 0.012
    b.box((width + 0.01, 0.015, height + 0.012), (centerX, frontY - 0.0075, bottomZ + height / 2), frontColor)
    b.box((min(0.1, width * 0.3), 0.02, 0.015), (centerX, frontY - 0.025, bottomZ + height * 0.62), "Metal")
    b.box((width, depth, wall), (centerX, mid, bottomZ + wall / 2), bodyColor)
    for sx in (-1, 1):
        b.box((wall, depth, height * 0.8), (centerX + sx * (width / 2 - wall / 2), mid, bottomZ + height * 0.4), bodyColor)
    b.box((width, wall, height * 0.8), (centerX, back - wall / 2, bottomZ + height * 0.4), bodyColor)
    return b.build()


def attachDrawers(body, drawers):
    """서랍들을 몸체의 자식으로 붙인다. (모두 원점에 있으므로 위치는 그대로)"""
    for drawer in drawers:
        drawer.parent = body
    body["drawers"] = len(drawers)
    return body


def makeNightstand():
    """
    협탁(0.47 x 0.42 x 0.58): 속이 빈 몸체(옆판·뒤판·바닥·칸막이·윗판) + 따로 된 서랍 2개(SM_GR_Nightstand_Drawer1·2, 아래부터).
    서랍 칸: 높이 0.09~0.30, 0.32~0.53. 앞은 -Y.
    """
    b = MeshBuilder("GR_Nightstand", "GiantRoom", bevel=0.006)
    for sx in (-1, 1):
        b.box((0.02, 0.4, 0.5), (sx * 0.215, 0, 0.3), "Oak")
    b.box((0.41, 0.02, 0.5), (0, 0.19, 0.3), "Oak")
    b.box((0.41, 0.4, 0.04), (0, 0, 0.07), "Oak")
    b.box((0.41, 0.38, 0.02), (0, 0.01, 0.31), "Oak")
    b.box((0.41, 0.4, 0.02), (0, 0, 0.54), "Oak")
    b.box((0.47, 0.42, 0.03), (0, 0, 0.565), "OakDark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.03, 0.03, 0.05), (sx * 0.2, sy * 0.17, 0.025), "OakDark")
    body = b.build()
    drawers = [
        makeDrawer("GR_Nightstand_Drawer1", 0.0, -0.2, 0.095, 0.4, 0.37, 0.2, "OakDark", "Oak"),
        makeDrawer("GR_Nightstand_Drawer2", 0.0, -0.2, 0.325, 0.4, 0.37, 0.2, "OakDark", "Oak"),
    ]
    return attachDrawers(body, drawers)


def makeDesk():
    """
    책상(1.2 x 0.6 x 0.75): 상판, 왼쪽 서랍장(속이 빈 몸체 + 따로 된 서랍 3개 SM_GR_Desk_Drawer1~3, 아래부터), 오른쪽 다리, 뒤 가림판.
    서랍 칸: 높이 0.05~0.25, 0.28~0.48, 0.50~0.70. 앉는 쪽·서랍 앞은 -Y.
    """
    b = MeshBuilder("GR_Desk", "GiantRoom", bevel=0.005)
    b.box((1.2, 0.6, 0.03), (0, 0, 0.735), "Oak")
    for x in (-0.58, -0.2):
        b.box((0.02, 0.56, 0.72), (x, 0.0, 0.36), "White")
    b.box((0.36, 0.02, 0.72), (-0.39, 0.27, 0.36), "White")
    b.box((0.36, 0.54, 0.05), (-0.39, 0.0, 0.025), "White")
    for z0, z1 in ((0.25, 0.28), (0.48, 0.5), (0.7, 0.72)):
        b.box((0.36, 0.54, z1 - z0), (-0.39, 0.0, (z0 + z1) / 2), "White")
    for sy in (-1, 1):
        b.box((0.04, 0.04, 0.72), (0.56, sy * 0.26, 0.36), "White")
    b.box((0.78, 0.02, 0.3), (0.18, 0.28, 0.55), "White")
    body = b.build()
    drawers = [
        makeDrawer(f"GR_Desk_Drawer{i + 1}", -0.39, -0.275, z, 0.355, 0.52, 0.19, "Gray", "White")
        for i, z in enumerate((0.055, 0.285, 0.505))
    ]
    return attachDrawers(body, drawers)


def exportWithChildren(obj, folder):
    """자식(서랍)이 있으면 함께 골라 FBX 하나로 내보낸다. 자식은 Unity에서 따로 된 자식 오브젝트가 된다."""
    if not obj.children:
        return exportFbx(obj, folder)
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for child in obj.children:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(folder, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=True,
        add_leaf_bones=False, bake_anim=False, path_mode="AUTO", embed_textures=False,
    )
    return path

def makeDeskChair():
    """책상 의자: 다리 4개, 좌판(0.45), 등받이(+Y)."""
    b = MeshBuilder("GR_DeskChair", "GiantRoom", bevel=0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.035, 0.035, 0.44), (sx * 0.19, sy * 0.19, 0.22), "OakDark")
    b.box((0.44, 0.44, 0.04), (0, 0, 0.45), "Oak")
    b.box((0.42, 0.03, 0.38), (0, 0.205, 0.68), "Oak")
    for sx in (-1, 1):
        b.box((0.035, 0.035, 0.42), (sx * 0.19, 0.2, 0.68), "OakDark")
    return b.build()


def makeBookshelf():
    """책장(0.8 x 0.3 x 1.8): 옆판·뒤판, 선반 6단(0.35 간격, 판 두께 0.03). 앞(-Y)이 열려 있다. 책은 빌더가 따로 꽂는다."""
    b = MeshBuilder("GR_Bookshelf", "GiantRoom", bevel=0.004)
    for sx in (-1, 1):
        b.box((0.03, 0.3, 1.8), (sx * 0.385, 0, 0.9), "OakDark")
    b.box((0.8, 0.015, 1.8), (0, 0.143, 0.9), "OakDark")
    for i in range(6):
        b.box((0.74, 0.29, 0.03), (0, 0, 0.015 + i * 0.35), "Oak")
    b.box((0.8, 0.3, 0.03), (0, 0, 1.785), "OakDark")
    return b.build()


def makeBook():
    """책(0.25 x 0.18 x 0.04, 눕힌 상태): 표지 + 종이 면. 책장에는 세워 꽂고, 쌓으면 계단이 된다."""
    b = MeshBuilder("GR_Book", "GiantRoom", bevel=0.003)
    b.box((0.25, 0.18, 0.006), (0, 0, 0.003), "Blue")
    b.box((0.25, 0.18, 0.006), (0, 0, 0.037), "Blue")
    b.box((0.01, 0.18, 0.04), (-0.12, 0, 0.02), "Blue")
    b.box((0.238, 0.172, 0.028), (0.004, 0, 0.02), "Paper")
    return b.build()


def makeWardrobe():
    """옷장(1.0 x 0.6 x 2.0): 문 2짝, 손잡이, 받침."""
    b = MeshBuilder("GR_Wardrobe", "GiantRoom", bevel=0.008)
    b.box((1.0, 0.6, 1.92), (0, 0, 1.04), "White")
    b.box((1.02, 0.62, 0.08), (0, 0, 0.04), "OakDark")
    b.box((1.04, 0.64, 0.04), (0, 0, 2.02), "OakDark")
    b.box((0.004, 0.01, 1.8), (0, -0.3, 1.04), "Gray")
    for x in (-0.05, 0.05):
        b.box((0.015, 0.03, 0.22), (x, -0.31, 1.1), "Metal")
    return b.build()


def makeSofa():
    """2인용 소파(1.8 x 0.8 x 0.8): 좌판(0.42), 쿠션 2개, 등받이(+Y), 팔걸이(0.6)."""
    b = MeshBuilder("GR_Sofa", "GiantRoom", bevel=0.03)
    b.box((1.8, 0.8, 0.28), (0, 0, 0.2), "Fabric")
    for x in (-0.42, 0.42):
        b.box((0.82, 0.62, 0.14), (x, -0.06, 0.41), "Fabric")
    b.box((1.8, 0.2, 0.5), (0, 0.3, 0.55), "Fabric")
    for sx in (-1, 1):
        b.box((0.16, 0.8, 0.26), (sx * 0.82, 0, 0.47), "Fabric")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.05, 0.05, 0.06), (sx * 0.82, sy * 0.34, 0.03), "OakDark")
    return b.build()


def makeCoffeeTable():
    """낮은 탁자(1.0 x 0.5 x 0.45): 상판, 아래 선반, 다리 4개."""
    b = MeshBuilder("GR_CoffeeTable", "GiantRoom", bevel=0.006)
    b.box((1.0, 0.5, 0.04), (0, 0, 0.43), "Oak")
    b.box((0.9, 0.42, 0.02), (0, 0, 0.12), "OakDark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.04, 0.04, 0.41), (sx * 0.46, sy * 0.21, 0.205), "OakDark")
    return b.build()


def makeRug():
    """러그(2.0 x 1.4 x 0.01): 갈색 바탕 + 보라 테두리."""
    b = MeshBuilder("GR_Rug", "GiantRoom", bevel=0.0)
    b.box((2.0, 1.4, 0.006), (0, 0, 0.003), "RugTrim")
    b.box((1.8, 1.2, 0.008), (0, 0, 0.004), "Rug")
    return b.build()


def makeFloorLamp():
    """스탠드 조명(1.6m): 둥근 받침, 기둥, 원뿔 갓, 갓 안 발광 전구."""
    b = MeshBuilder("GR_FloorLamp", "GiantRoom", bevel=0.0)
    b.cylinder(0.15, 0.15, 0.03, (0, 0, 0.015), "Charcoal", segments=20)
    b.cylinder(0.012, 0.012, 1.4, (0, 0, 0.72), "Metal", segments=10)
    b.hollowCylinder(0.2, 0.12, 0.25, 0.006, (0, 0, 1.47), "Cream", segments=20)
    b.sphere(0.05, (0, 0, 1.42), "Glow", segments=12)
    return b.build()


def makeDeskLamp():
    """탁상 조명(0.45m): 받침, 꺾인 팔 2개, 갓(-Y 쪽 아래로), 발광 전구."""
    b = MeshBuilder("GR_DeskLamp", "GiantRoom", bevel=0.0)
    b.cylinder(0.08, 0.08, 0.02, (0, 0.05, 0.01), "Charcoal", segments=18)
    b.box((0.015, 0.015, 0.25), (0, 0.04, 0.13), "Metal", rot=rotX(10))
    b.box((0.015, 0.015, 0.22), (0, -0.03, 0.3), "Metal", rot=rotX(60))
    b.hollowCylinder(0.07, 0.03, 0.09, 0.004, (0, -0.13, 0.36), "Red", segments=16, rot=rotX(-30))
    b.sphere(0.025, (0, -0.15, 0.33), "Glow", segments=10)
    return b.build()


def makeCeilingLamp():
    """천장 조명(지름 0.5, 높이 0.15): 납작한 원판 갓 + 발광 아랫면. 피벗은 윗면(천장에 붙음) 기준 바닥 중앙이 아니라 위."""
    b = MeshBuilder("GR_CeilingLamp", "GiantRoom", bevel=0.0)
    b.cylinder(0.05, 0.05, 0.05, (0, 0, -0.025), "White", segments=16)
    b.cylinder(0.25, 0.22, 0.08, (0, 0, -0.09), "White", segments=28)
    b.cylinder(0.21, 0.21, 0.01, (0, 0, -0.135), "Glow", segments=28)
    return b.build()


def makeToyBox():
    """장난감 상자(0.6 x 0.45 x 0.45): 나무 상자, 뚜껑, 앞면 색 줄무늬."""
    b = MeshBuilder("GR_ToyBox", "GiantRoom", bevel=0.008)
    b.box((0.6, 0.45, 0.42), (0, 0, 0.21), "Oak")
    b.box((0.62, 0.47, 0.04), (0, 0, 0.44), "OakDark")
    for i, color in enumerate(("Red", "Yellow", "Blue")):
        b.box((0.56, 0.01, 0.05), (0, -0.226, 0.1 + i * 0.1), color)
    return b.build()


def makeTrashCan():
    """휴지통(지름 0.3, 높이 0.35): 속이 빈 원통 + 테두리 고리, 바닥에 구겨진 종이 2개."""
    b = MeshBuilder("GR_TrashCan", "GiantRoom", bevel=0.0)
    b.hollowCylinder(0.13, 0.15, 0.34, 0.005, (0, 0, 0.17), "Gray", floor=0.01, segments=24)
    b.hollowCylinder(0.155, 0.155, 0.02, 0.01, (0, 0, 0.34), "Charcoal", segments=24)
    b.sphere(0.05, (0.03, 0.02, 0.06), "Paper", segments=8)
    b.sphere(0.04, (-0.04, -0.02, 0.05), "Paper", segments=8)
    return b.build()


def makeDoor():
    """방문(1.0 x 0.05 x 2.1): 문짝, 테두리 판 2개, 손잡이(-Y 쪽)."""
    b = MeshBuilder("GR_Door", "GiantRoom", bevel=0.005)
    b.box((0.9, 0.04, 2.05), (0, 0, 1.025), "White")
    b.box((1.0, 0.06, 0.06), (0, 0, 2.08), "Cream")
    for sx in (-1, 1):
        b.box((0.05, 0.06, 2.1), (sx * 0.475, 0, 1.05), "Cream")
    for z in (0.5, 1.5):
        b.box((0.7, 0.01, 0.7), (0, -0.025, z), "Cream")
    b.cylinder(0.03, 0.03, 0.05, (0.35, -0.05, 1.0), "Metal", segments=12, rot=rotX(90))
    b.box((0.12, 0.02, 0.02), (0.31, -0.08, 1.0), "Metal")
    return b.build()


# ---------------------------------------------------------------- 작은 물건 (실제 크기, 10배로 키워 변신 사물)

def makePencil():
    """연필(0.18 x 0.008): 육각 노란 몸통(X 방향), 깎은 나무 끝 + 흑연, 분홍 지우개 + 금속 띠."""
    b = MeshBuilder("GR_Small_Pencil", "GiantRoom", bevel=0.0)
    b.cylinder(0.004, 0.004, 0.15, (0, 0, 0.004), "Yellow", segments=6, rot=rotY(90))
    b.cylinder(0.004, 0.001, 0.018, (0.084, 0, 0.004), "Oak", segments=6, rot=rotY(90))
    b.cylinder(0.0012, 0.0002, 0.005, (0.095, 0, 0.004), "Graphite", segments=6, rot=rotY(90))
    b.cylinder(0.0042, 0.0042, 0.008, (-0.079, 0, 0.004), "Metal", segments=10, rot=rotY(90))
    b.cylinder(0.004, 0.004, 0.008, (-0.087, 0, 0.004), "Pink", segments=10, rot=rotY(90))
    return b.build()


def makeEraser():
    """지우개(0.05 x 0.02 x 0.012): 흰 몸통 + 파란 종이 띠."""
    b = MeshBuilder("GR_Small_Eraser", "GiantRoom", bevel=0.0015)
    b.box((0.05, 0.02, 0.012), (0, 0, 0.006), "White")
    b.box((0.03, 0.0205, 0.0125), (0.006, 0, 0.006), "Blue")
    return b.build()


def makeRuler():
    """자(0.25 x 0.03 x 0.002): 투명 대신 연한 노랑, 눈금."""
    b = MeshBuilder("GR_Small_Ruler", "GiantRoom", bevel=0.0)
    b.box((0.25, 0.03, 0.002), (0, 0, 0.001), "Cream")
    for i in range(25):
        length = 0.008 if i % 5 else 0.014
        b.box((0.0008, length, 0.0004), (-0.12 + i * 0.01, 0.015 - length / 2, 0.0021), "Black")
    return b.build()


def makeSharpener():
    """연필깎이(0.03 x 0.02 x 0.015): 빨간 몸통, 구멍, 금속 날."""
    b = MeshBuilder("GR_Small_Sharpener", "GiantRoom", bevel=0.001)
    b.box((0.03, 0.02, 0.015), (0, 0, 0.0075), "Red")
    b.cylinder(0.0045, 0.0045, 0.003, (0, -0.0105, 0.0075), "Black", segments=12, rot=rotX(90))
    b.box((0.016, 0.001, 0.008), (0, 0.0005, 0.0155), "Metal")
    return b.build()


def makeCrayon():
    """크레용(0.09 x 0.009): 초록 몸통 + 종이 띠 + 뾰족한 끝 (X 방향)."""
    b = MeshBuilder("GR_Small_Crayon", "GiantRoom", bevel=0.0)
    b.cylinder(0.0045, 0.0045, 0.08, (0, 0, 0.0045), "Green", segments=10, rot=rotY(90))
    b.cylinder(0.0047, 0.0047, 0.06, (-0.006, 0, 0.0045), "Paper", segments=10, rot=rotY(90))
    b.cylinder(0.0045, 0.0012, 0.01, (0.045, 0, 0.0045), "Green", segments=10, rot=rotY(90))
    return b.build()


def makeGlueStick():
    """딱풀(지름 0.025, 높이 0.1): 몸통 + 뚜껑, 세워 둔다."""
    b = MeshBuilder("GR_Small_GlueStick", "GiantRoom", bevel=0.0)
    b.cylinder(0.0125, 0.0125, 0.075, (0, 0, 0.0375), "White", segments=16)
    b.cylinder(0.013, 0.013, 0.004, (0, 0, 0.002), "Purple", segments=16)
    b.cylinder(0.0132, 0.0132, 0.025, (0, 0, 0.0875), "Purple", segments=16)
    b.box((0.012, 0.001, 0.04), (0, -0.0126, 0.04), "Yellow")
    return b.build()


def makeScissors():
    """가위(0.17 x 0.07, 눕힘): 금속 날 2개 + 플라스틱 손잡이 고리 2개."""
    b = MeshBuilder("GR_Small_Scissors", "GiantRoom", bevel=0.0)
    for side in (-1, 1):
        b.box((0.09, 0.008, 0.002), (0.045, side * 0.004, 0.0015), "Metal", rot=rotZ(side * 4))
        b.cylinder(0.018, 0.018, 0.004, (-0.03, side * 0.017, 0.002), "Orange", segments=14)
        b.cylinder(0.011, 0.011, 0.0045, (-0.03, side * 0.017, 0.002), "Black", segments=14)
    b.cylinder(0.003, 0.003, 0.006, (0.0, 0, 0.003), "Metal", segments=8)
    return b.build()


def makeCoin():
    """동전(지름 0.025, 두께 0.002)."""
    b = MeshBuilder("GR_Small_Coin", "GiantRoom", bevel=0.0)
    b.cylinder(0.0125, 0.0125, 0.002, (0, 0, 0.001), "Copper", segments=24)
    b.cylinder(0.009, 0.009, 0.0022, (0, 0, 0.001), "Metal", segments=24)
    return b.build()


def makeDice():
    """주사위(0.03): 흰 정육면체 + 검은 눈(윗면 5, 앞면 2)."""
    b = MeshBuilder("GR_Small_Dice", "GiantRoom", bevel=0.003)
    b.box((0.03, 0.03, 0.03), (0, 0, 0.015), "White")
    for x, y in ((-0.008, -0.008), (0.008, 0.008), (0, 0), (-0.008, 0.008), (0.008, -0.008)):
        b.cylinder(0.0025, 0.0025, 0.001, (x, y, 0.0302), "Black", segments=8)
    for x in (-0.007, 0.007):
        b.cylinder(0.0025, 0.0025, 0.001, (x, -0.0152, 0.015 + x), "Black", segments=8, rot=rotX(90))
    return b.build()


def makeToyBlock():
    """나무 장난감 블록(0.04): 파란 정육면체, 면마다 노란 글자 판."""
    b = MeshBuilder("GR_Small_ToyBlock", "GiantRoom", bevel=0.002)
    b.box((0.04, 0.04, 0.04), (0, 0, 0.02), "Blue")
    b.box((0.026, 0.0005, 0.026), (0, -0.0202, 0.02), "Yellow")
    b.box((0.0005, 0.026, 0.026), (0.0202, 0, 0.02), "Red")
    b.box((0.026, 0.026, 0.0005), (0, 0, 0.0402), "Green")
    return b.build()


def makeMug():
    """머그잔(지름 0.08, 높이 0.095): 속이 빈 흰 몸통(바닥 6mm), 손잡이(+X)."""
    b = MeshBuilder("GR_Small_Mug", "GiantRoom", bevel=0.0)
    b.hollowCylinder(0.04, 0.04, 0.095, 0.004, (0, 0, 0.0475), "Ceramic", floor=0.006, segments=24)
    # 손잡이: ㄷ자 막대 세 개 (+X 쪽)
    b.box((0.006, 0.012, 0.05), (0.058, 0, 0.05), "Ceramic")
    for z in (0.028, 0.072):
        b.box((0.02, 0.012, 0.006), (0.048, 0, z), "Ceramic")
    return b.build()

def makeRubberDuck():
    """고무 오리(높이 0.09): 노란 몸통·머리, 주황 부리, 검은 눈. 앞은 -Y."""
    b = MeshBuilder("GR_Small_RubberDuck", "GiantRoom", bevel=0.0)
    b.sphere(0.045, (0, 0.005, 0.035), "Yellow", segments=16, scale=(0.9, 1.15, 0.75))
    b.sphere(0.026, (0, -0.025, 0.075), "Yellow", segments=14)
    b.sphere(0.012, (0, -0.05, 0.07), "Orange", segments=10, scale=(1.2, 1.0, 0.5))
    for x in (-0.012, 0.012):
        b.sphere(0.004, (x, -0.044, 0.082), "Black", segments=8)
    b.sphere(0.012, (0, 0.05, 0.05), "Yellow", segments=10, scale=(0.8, 1.0, 0.8))
    return b.build()


def makeBattery():
    """AA 건전지(0.05 x 0.014, 눕힘, X 방향): 검은 몸통 + 금색 띠 + 양극 꼭지."""
    b = MeshBuilder("GR_Small_Battery", "GiantRoom", bevel=0.0)
    b.cylinder(0.007, 0.007, 0.048, (0, 0, 0.007), "Black", segments=14, rot=rotY(90))
    b.cylinder(0.0071, 0.0071, 0.014, (0.016, 0, 0.007), "Copper", segments=14, rot=rotY(90))
    b.cylinder(0.0025, 0.0025, 0.002, (0.025, 0, 0.007), "Metal", segments=10, rot=rotY(90))
    return b.build()


def makeStickyNotes():
    """메모지 묶음(0.075 x 0.075 x 0.012): 노란 종이 묶음 + 맨 위 한 장 살짝 들림."""
    b = MeshBuilder("GR_Small_StickyNotes", "GiantRoom", bevel=0.0005)
    b.box((0.075, 0.075, 0.011), (0, 0, 0.0055), "Yellow")
    b.box((0.075, 0.075, 0.0006), (0, -0.002, 0.0118), "Yellow", rot=rotX(4))
    return b.build()


def makeSmartphone():
    """스마트폰(0.15 x 0.07 x 0.008, 눕힘): 검은 몸통, 발광 화면, 카메라."""
    b = MeshBuilder("GR_Small_Smartphone", "GiantRoom", bevel=0.002)
    b.box((0.07, 0.15, 0.008), (0, 0, 0.004), "Black")
    b.box((0.064, 0.138, 0.0005), (0, 0, 0.0082), "Screen")
    b.cylinder(0.004, 0.004, 0.001, (0.02, 0.06, 0.0002), "Glass", segments=10)
    return b.build()


def makeToyCar():
    """장난감 자동차(0.08 x 0.04 x 0.035, X 방향): 빨간 차체, 창, 바퀴 4개."""
    b = MeshBuilder("GR_Small_ToyCar", "GiantRoom", bevel=0.003)
    b.box((0.08, 0.036, 0.016), (0, 0, 0.014), "Red")
    b.box((0.042, 0.032, 0.014), (-0.004, 0, 0.029), "Glass")
    b.box((0.038, 0.033, 0.002), (-0.004, 0, 0.0365), "Red")
    for x in (-0.026, 0.026):
        for y in (-0.019, 0.019):
            b.cylinder(0.007, 0.007, 0.006, (x, y, 0.007), "Black", segments=12, rot=rotX(90))
    return b.build()


def makePaperClip():
    """클립(0.03 x 0.008): 가는 금속 고리 두 겹."""
    b = MeshBuilder("GR_Small_PaperClip", "GiantRoom", bevel=0.0)
    for x, w in ((0.0, 0.03), (0.002, 0.022)):
        for y in (-0.003, 0.003):
            b.box((w, 0.0008, 0.0008), (x, y * (1 if w > 0.025 else 0.6), 0.0004), "Metal")
    for x in (-0.015, 0.015):
        b.box((0.0008, 0.0068, 0.0008), (x, 0, 0.0004), "Metal")
    return b.build()


def makeBottleCap():
    """병뚜껑(지름 0.03, 높이 0.007): 빨간 톱니 테두리."""
    b = MeshBuilder("GR_Small_BottleCap", "GiantRoom", bevel=0.0)
    b.cylinder(0.015, 0.015, 0.006, (0, 0, 0.003), "Red", segments=21)
    b.cylinder(0.0135, 0.0135, 0.0012, (0, 0, 0.0065), "Metal", segments=21)
    return b.build()


def makeLegoBrick():
    """조립 블록 2x4(0.032 x 0.016 x 0.0096) + 돌기 8개."""
    b = MeshBuilder("GR_Small_LegoBrick", "GiantRoom", bevel=0.0005)
    b.box((0.032, 0.016, 0.0096), (0, 0, 0.0048), "Red")
    for i in range(4):
        for j in range(2):
            b.cylinder(0.0024, 0.0024, 0.0017, (-0.012 + i * 0.008, -0.004 + j * 0.008, 0.0104), "Red", segments=10)
    return b.build()


def makeTapeRoll():
    """테이프(지름 0.08, 폭 0.02, 세움): 연노랑 테이프 고리 + 흰 심지 고리, 가운데가 뚫려 있다."""
    b = MeshBuilder("GR_Small_TapeRoll", "GiantRoom", bevel=0.0)
    b.hollowCylinder(0.04, 0.04, 0.02, 0.015, (0, 0, 0.04), "Cream", segments=24, rot=rotX(90))
    b.hollowCylinder(0.025, 0.025, 0.021, 0.004, (0, 0, 0.04), "White", segments=24, rot=rotX(90))
    return b.build()


def makeApple():
    """사과(지름 0.08): 빨간 몸통, 꼭지, 잎."""
    b = MeshBuilder("GR_Small_Apple", "GiantRoom", bevel=0.0)
    b.sphere(0.04, (0, 0, 0.037), "AppleRed", segments=16, scale=(1.0, 1.0, 0.92))
    b.cylinder(0.0025, 0.002, 0.015, (0, 0, 0.078), "OakDark", segments=6)
    b.sphere(0.008, (0.008, 0, 0.082), "Green", segments=8, scale=(1.6, 0.6, 0.3))
    return b.build()


def makeAlarmClock():
    """자명종(0.1 x 0.05 x 0.11): 둥근 몸통(앞 -Y), 흰 문자판, 바늘, 종 2개, 다리."""
    b = MeshBuilder("GR_Small_AlarmClock", "GiantRoom", bevel=0.0)
    b.cylinder(0.045, 0.045, 0.04, (0, 0, 0.055), "Blue", segments=24, rot=rotX(90))
    b.cylinder(0.038, 0.038, 0.002, (0, -0.021, 0.055), "White", segments=24, rot=rotX(90))
    b.box((0.002, 0.001, 0.028), (0, -0.0225, 0.065), "Black")
    b.box((0.02, 0.001, 0.002), (0.008, -0.0225, 0.055), "Black")
    for x in (-0.025, 0.025):
        b.sphere(0.016, (x, 0, 0.1), "Metal", segments=12, scale=(1, 1, 0.6))
        b.box((0.004, 0.004, 0.012), (x * 1.2, 0, 0.006), "Metal")
    return b.build()


def makeTissueBox():
    """휴지 상자(0.24 x 0.12 x 0.09): 무늬 상자 + 위로 나온 휴지."""
    b = MeshBuilder("GR_Small_TissueBox", "GiantRoom", bevel=0.002)
    b.box((0.24, 0.12, 0.09), (0, 0, 0.045), "Pink")
    b.box((0.12, 0.03, 0.001), (0, 0, 0.0905), "White")
    b.box((0.06, 0.002, 0.04), (0, 0, 0.11), "Paper", rot=rotY(15))
    return b.build()


def makeBlanketDrape():
    """
    침대에서 바닥까지 흘러내린 이불 자락(폭 0.7, 높이 0.55, 길이 약 1.0). 위끝은 매트리스 위에 걸치고(+Y 쪽),
    앞(-Y)으로 처지며 내려간다: 위는 가파르고(42°) 아래로 갈수록 완만해져(20°) 바닥에 구겨져 고인다.
    가장 가파른 곳도 이동 경사 한계(45°) 안이라 맵에서 걸어 오르는 비탈이 된다. 주름은 비탈을 따라 둥근 골로 낸다.
    """
    b = MeshBuilder("GR_BlanketDrape", "GiantRoom", bevel=0.004)
    width, thick, top = 0.7, 0.016, 0.55
    b.box((width, 0.09, thick), (0, 0.045, top + thick / 2), "Blanket")

    # 처지는 곡선: (각도, 수평 거리) 구간을 위에서부터 이어 붙인다.
    segments = ((42, 0.18), (38, 0.2), (32, 0.25), (20, 0.209))
    y, z = 0.0, top
    for index, (angle, run) in enumerate(segments):
        a = math.radians(angle)
        drop = run * math.tan(a)
        length = math.hypot(run, drop)
        cy, cz = y - run / 2, z - drop / 2 + thick / 2
        b.box((width, length, thick), (0, cy, cz), "Blanket", rot=rotX(angle))
        b.cylinder(thick * 0.6, thick * 0.6, width, (0, y, z + thick / 2), "Blanket", segments=8, rot=rotY(90))
        normal = (0.0, -math.sin(a), math.cos(a))
        for x, r in ((-0.22, 0.016), (-0.02, 0.012), (0.2, 0.018)):
            wobble = 0.01 * ((index + int(x * 10)) % 3 - 1)
            b.cylinder(r, r * 0.8, length * 0.95, (x + wobble, cy + normal[1] * thick * 0.4, cz + normal[2] * thick * 0.4),
                       "Blanket", segments=8, rot=rotX(90 + angle))
        y, z = y - run, z - drop

    # 바닥에 고인 자락: 겹쳐 구겨진 두 장 + 둥근 접힘
    b.box((width + 0.03, 0.17, thick), (0, y - 0.08, thick / 2), "Blanket")
    b.box((width * 0.8, 0.12, thick), (0.04, y - 0.12, thick * 1.5), "Blanket", rot=rotZ(6))
    b.cylinder(0.02, 0.02, width * 0.7, (-0.05, y - 0.17, 0.02), "Blanket", segments=10, rot=rotY(90))
    return b.build()

furniture = [    makeBed, makeNightstand, makeDesk, makeDeskChair, makeBookshelf, makeBook, makeWardrobe, makeSofa,
    makeCoffeeTable, makeRug, makeFloorLamp, makeDeskLamp, makeCeilingLamp, makeToyBox, makeTrashCan, makeDoor,
    makeBlanketDrape,
]
smallItems = [
    makePencil, makeEraser, makeRuler, makeSharpener, makeCrayon, makeGlueStick, makeScissors, makeCoin,
    makeDice, makeToyBlock, makeMug, makeRubberDuck, makeBattery, makeStickyNotes, makeSmartphone, makeToyCar,
    makePaperClip, makeBottleCap, makeLegoBrick, makeTapeRoll, makeApple, makeAlarmClock, makeTissueBox,
]


def main():
    """가구와 작은 물건을 만들고 FBX/블렌드/미리보기를 저장한다. (미리보기는 종류별로 따로, 작은 물건은 크게 보이게)"""
    resetScene()
    bigOnes = [fn() for fn in furniture]
    smallOnes = [fn() for fn in smallItems]
    for obj in bigOnes + smallOnes:
        exportWithChildren(obj, outDir)
        print("[GiantRoomProps] exported", obj.name)

    if previewDir:
        os.makedirs(previewDir, exist_ok=True)
        layoutGrid(bigOnes, (0, 0, 0), 4, 3.0)
        renderPreview(bigOnes, os.path.join(previewDir, "preview_giantroom_furniture.png"))
        for obj in bigOnes:
            obj.hide_render = True
        for label in [o for o in bpy.data.objects if o.name.startswith("Label_")]:
            label.hide_render = True
        layoutGrid(smallOnes, (100, 0, 0), 6, 0.3)
        renderPreview(smallOnes, os.path.join(previewDir, "preview_giantroom_small.png"))
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=blendPath)
    print("[GiantRoomProps] done:", len(bigOnes) + len(smallOnes), "objects")


main()
