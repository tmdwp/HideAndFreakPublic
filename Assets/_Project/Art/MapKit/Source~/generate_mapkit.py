"""
맵 제작용 기본 오브젝트(MapKit) 생성 스크립트.

실행:
  blender -b --python generate_mapkit.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

- 단위는 미터, 피벗은 바닥 중앙(천장 조명은 윗면, 배관은 축 중앙).
- 앞면은 블렌더 -Y 쪽. FBX는 Unity 축(-Z forward, Y up)에 맞춰 회전/스케일을 굽는다.
- UV는 월드 스케일 박스 투영(1 UV = 1m)이라 격자/타일 텍스처를 바로 써도 크기가 맞는다.
"""
import bpy
import math
import os
import sys
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import (MeshBuilder, configure, exportFbx, layoutGrid, renderPreview, resetScene,
                           rotX, rotY, rotZ)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "Concrete":     ((0.55, 0.55, 0.52), 0.0, 0.9, 0.0),
    "ConcreteDark": ((0.36, 0.36, 0.35), 0.0, 0.9, 0.0),
    "Plaster":      ((0.74, 0.72, 0.66), 0.0, 0.85, 0.0),
    "Wood":         ((0.50, 0.34, 0.20), 0.0, 0.7, 0.0),
    "WoodDark":     ((0.30, 0.19, 0.11), 0.0, 0.7, 0.0),
    "Metal":        ((0.62, 0.63, 0.65), 0.8, 0.4, 0.0),
    "MetalDark":    ((0.25, 0.26, 0.28), 0.7, 0.5, 0.0),
    "Rust":         ((0.45, 0.22, 0.10), 0.4, 0.8, 0.0),
    "PaintGreen":   ((0.28, 0.38, 0.32), 0.3, 0.6, 0.0),
    "PaintBlue":    ((0.16, 0.28, 0.48), 0.3, 0.5, 0.0),
    "ContainerRed": ((0.50, 0.12, 0.08), 0.4, 0.6, 0.0),
    "Cardboard":    ((0.66, 0.50, 0.32), 0.0, 0.9, 0.0),
    "Tape":         ((0.82, 0.76, 0.60), 0.0, 0.5, 0.0),
    "Fabric":       ((0.86, 0.87, 0.89), 0.0, 0.95, 0.0),
    "FabricBlue":   ((0.40, 0.52, 0.62), 0.0, 0.95, 0.0),
    "White":        ((0.93, 0.93, 0.92), 0.0, 0.5, 0.0),
    "Rubber":       ((0.05, 0.05, 0.05), 0.0, 0.9, 0.0),
    "Orange":       ((0.92, 0.38, 0.05), 0.0, 0.6, 0.0),
    "Yellow":       ((0.90, 0.72, 0.10), 0.0, 0.6, 0.0),
    "Light":        ((1.00, 0.93, 0.78), 0.0, 0.3, 4.0),
}
configure(palette, "M_Kit_")


# ---------------------------------------------------------------- 구조물

def makeWall(width):
    """두께 0.2, 높이 3m 벽 + 양면 걸레받이."""
    b = MeshBuilder(f"Wall_{width:g}m", "Structure")
    b.box((width, 0.2, 3.0), (0, 0, 1.5), "Plaster")
    b.box((width, 0.24, 0.12), (0, 0, 0.06), "WoodDark")
    return b.build()


def makeWallDoor():
    """4m 벽, 가운데 1.2 x 2.2m 문 구멍."""
    b = MeshBuilder("Wall_Door_4m", "Structure")
    for side in (-1, 1):
        b.box((1.4, 0.2, 3.0), (side * 1.3, 0, 1.5), "Plaster")
        b.box((1.4, 0.24, 0.12), (side * 1.3, 0, 0.06), "WoodDark")
        b.box((0.08, 0.26, 2.24), (side * 0.64, 0, 1.12), "WoodDark")
    b.box((1.2, 0.2, 0.8), (0, 0, 2.6), "Plaster")
    b.box((1.36, 0.26, 0.08), (0, 0, 2.24), "WoodDark")
    return b.build()


def makeWallWindow():
    """4m 벽, 가운데 1.6 x 1.2m 창 구멍(바닥에서 1m)."""
    b = MeshBuilder("Wall_Window_4m", "Structure")
    b.box((4.0, 0.2, 1.0), (0, 0, 0.5), "Plaster")
    b.box((4.0, 0.2, 0.8), (0, 0, 2.6), "Plaster")
    b.box((4.0, 0.24, 0.12), (0, 0, 0.06), "WoodDark")
    for side in (-1, 1):
        b.box((1.2, 0.2, 1.2), (side * 1.4, 0, 1.6), "Plaster")
        b.box((0.08, 0.26, 1.28), (side * 0.84, 0, 1.6), "WoodDark")
    b.box((1.76, 0.26, 0.08), (0, 0, 2.24), "WoodDark")
    b.box((1.8, 0.34, 0.06), (0, 0, 0.97), "WoodDark")
    return b.build()


def makeWallHalf():
    """허리 높이(1.1m) 엄폐용 낮은 벽 2m."""
    b = MeshBuilder("Wall_Low_2m", "Structure")
    b.box((2.0, 0.2, 1.1), (0, 0, 0.55), "Concrete")
    b.box((2.04, 0.26, 0.06), (0, 0, 1.1), "ConcreteDark")
    return b.build()


def makeFloor(size):
    """두께 0.2 바닥판. 윗면이 피벗(z=0)."""
    b = MeshBuilder(f"Floor_{size}x{size}", "Structure", bevel=0.005)
    b.box((size, size, 0.2), (0, 0, -0.1), "Concrete")
    return b.build()


def makePillarSquare():
    """3m 사각 기둥 + 받침/머리."""
    b = MeshBuilder("Pillar_Square", "Structure")
    b.box((0.4, 0.4, 3.0), (0, 0, 1.5), "Concrete")
    b.box((0.52, 0.52, 0.15), (0, 0, 0.075), "ConcreteDark")
    b.box((0.52, 0.52, 0.15), (0, 0, 2.925), "ConcreteDark")
    return b.build()


def makePillarRound():
    """3m 원기둥 + 사각 받침/머리."""
    b = MeshBuilder("Pillar_Round", "Structure")
    b.cylinder(0.22, 0.22, 3.0, (0, 0, 1.5), "Concrete", segments=20)
    b.box((0.55, 0.55, 0.15), (0, 0, 0.075), "ConcreteDark")
    b.box((0.55, 0.55, 0.15), (0, 0, 2.925), "ConcreteDark")
    return b.build()


def makeStairs():
    """단높이 0.2 x 10단 = 2m 계단, +Y 쪽으로 올라간다(길이 3m)."""
    b = MeshBuilder("Stairs_2m", "Structure", bevel=0.008)
    for i in range(10):
        height = (i + 1) * 0.2
        b.box((1.2, 0.3, height), (0, -1.5 + i * 0.3 + 0.15, height / 2), "Concrete")
    return b.build()


def makeRamp():
    """폭 2, 길이 4, 높이 1m 경사로, +Y 쪽이 높다."""
    b = MeshBuilder("Ramp_4x1", "Structure")
    points = [(-1, -2, 0), (1, -2, 0), (1, 2, 0), (-1, 2, 0), (-1, 2, 1), (1, 2, 1)]
    faces = [(0, 3, 2, 1), (3, 4, 5, 2), (0, 1, 5, 4), (0, 4, 3), (1, 2, 5)]
    b.polyhedron(points, faces, "ConcreteDark")
    return b.build()


def makePlatform():
    """2 x 2 x 1m 단상/엄폐물."""
    b = MeshBuilder("Platform_2x2x1", "Structure")
    b.box((2.0, 2.0, 1.0), (0, 0, 0.5), "ConcreteDark")
    return b.build()


def makeRailing():
    """2m 난간(높이 1m)."""
    b = MeshBuilder("Railing_2m", "Structure", bevel=0.004)
    for x in (-0.975, 0, 0.975):
        b.box((0.05, 0.05, 1.0), (x, 0, 0.5), "MetalDark")
    b.box((2.0, 0.06, 0.06), (0, 0, 1.0), "Metal")
    b.box((2.0, 0.04, 0.04), (0, 0, 0.5), "Metal")
    return b.build()


def makeDoor():
    """Wall_Door_4m 구멍에 맞는 문짝. 피벗은 경첩(왼쪽 아래), 문짝은 +X 쪽으로 뻗는다."""
    b = MeshBuilder("Door_Single", "Structure", bevel=0.005)
    b.box((1.18, 0.05, 2.18), (0.59, 0, 1.09), "Wood")
    for side in (-1, 1):
        b.box((0.03, 0.04, 0.03), (1.02, side * 0.045, 1.0), "Metal")
        b.box((0.13, 0.03, 0.03), (0.97, side * 0.07, 1.0), "Metal")
    return b.build()


def makeBeam():
    """4m H빔(천장/구조용)."""
    b = MeshBuilder("Beam_4m", "Structure", bevel=0.004)
    b.box((4.0, 0.2, 0.03), (0, 0, 0.015), "Rust")
    b.box((4.0, 0.2, 0.03), (0, 0, 0.285), "Rust")
    b.box((4.0, 0.03, 0.24), (0, 0, 0.15), "Rust")
    return b.build()


def makeContainer():
    """6 x 2.4 x 2.6m 화물 컨테이너(큰 엄폐물)."""
    b = MeshBuilder("Container_6m", "Structure", bevel=0.02)
    b.box((5.9, 2.36, 2.5), (0, 0, 1.3), "ContainerRed")
    steps = 18
    for i in range(steps):
        x = -2.7 + i * (5.4 / (steps - 1))
        b.box((0.12, 2.42, 2.4), (x, 0, 1.3), "ContainerRed")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.16, 0.16, 2.6), (sx * 2.92, sy * 1.12, 1.3), "MetalDark")
        b.box((6.0, 0.14, 0.14), (0, sx * 1.13, 2.53), "MetalDark")
        b.box((6.0, 0.14, 0.14), (0, sx * 1.13, 0.07), "MetalDark")
    for y in (-0.6, -0.35, 0.35, 0.6):
        b.box((0.04, 0.05, 2.3), (2.97, y, 1.3), "Metal")
    return b.build()


# ---------------------------------------------------------------- 소품

def makeCrate(size, name):
    """나무 상자: 몸통 + 모서리 각목 12개."""
    b = MeshBuilder(name, "Prop", bevel=0.006 * size)
    t = 0.08 * size
    h = size / 2 - t / 2
    b.box((size - 0.06 * size,) * 3, (0, 0, size / 2), "Wood")
    for s1 in (-1, 1):
        for s2 in (-1, 1):
            b.box((size, t, t), (0, s1 * h, size / 2 + s2 * h), "WoodDark")
            b.box((t, size, t), (s1 * h, 0, size / 2 + s2 * h), "WoodDark")
            b.box((t, t, size), (s1 * h, s2 * h, size / 2), "WoodDark")
    return b.build()


def makeBarrel():
    """드럼통(지름 0.6, 높이 0.9)."""
    b = MeshBuilder("Barrel", "Prop", bevel=0.005)
    b.cylinder(0.3, 0.3, 0.9, (0, 0, 0.45), "PaintBlue", segments=20)
    for z in (0.3, 0.6):
        b.cylinder(0.31, 0.31, 0.03, (0, 0, z), "PaintBlue", segments=20)
    for z in (0.012, 0.888):
        b.cylinder(0.305, 0.305, 0.024, (0, 0, z), "MetalDark", segments=20)
    return b.build()


def makePallet():
    """1.2 x 1.0 x 0.15m 나무 팔레트."""
    b = MeshBuilder("Pallet", "Prop", bevel=0.003)
    for i in range(5):
        y = -0.43 + i * 0.215
        b.box((1.2, 0.14, 0.02), (0, y, 0.14), "Wood")
    for y in (-0.44, 0, 0.44):
        b.box((1.2, 0.12, 0.02), (0, y, 0.12), "WoodDark")
        b.box((1.2, 0.12, 0.02), (0, y, 0.01), "Wood")
        for x in (-0.54, 0, 0.54):
            b.box((0.12, 0.12, 0.09), (x, y, 0.065), "WoodDark")
    return b.build()


def addCardboardBox(b, size, center, angle=0.0):
    """테이프 붙은 골판지 상자 하나를 빌더에 추가."""
    sx, sy, sz = size
    cx, cy, cz = center
    rot = rotZ(angle)
    offset = Matrix.Translation((cx, cy, cz))
    parts = [
        ((sx, sy, sz), (0, 0, sz / 2), "Cardboard"),
        ((sx + 0.01, 0.07, 0.006), (0, 0, sz), "Tape"),
        ((0.006, 0.07, 0.1), (sx / 2, 0, sz - 0.05), "Tape"),
        ((0.006, 0.07, 0.1), (-sx / 2, 0, sz - 0.05), "Tape"),
    ]
    for partSize, partCenter, mat in parts:
        world = offset @ rot @ Vector(partCenter)
        b.box(partSize, world, mat, rot=rot)


def makeCardboardBox():
    """골판지 상자 1개(0.5 x 0.4 x 0.35)."""
    b = MeshBuilder("Box_Cardboard", "Prop", bevel=0.004)
    addCardboardBox(b, (0.5, 0.4, 0.35), (0, 0, 0))
    return b.build()


def makeCardboardStack():
    """살짝 비뚤게 쌓인 골판지 상자 3개."""
    b = MeshBuilder("Box_Cardboard_Stack", "Prop", bevel=0.004)
    addCardboardBox(b, (0.5, 0.4, 0.35), (-0.28, 0, 0), 4)
    addCardboardBox(b, (0.5, 0.4, 0.35), (0.27, 0.02, 0), -6)
    addCardboardBox(b, (0.45, 0.38, 0.3), (0, 0.01, 0.35), 12)
    return b.build()


def makeShelf():
    """1.2 x 0.45 x 1.9m 철제 선반(5단)."""
    b = MeshBuilder("Shelf_Metal", "Prop", bevel=0.003)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.04, 0.04, 1.9), (sx * 0.58, sy * 0.205, 0.95), "MetalDark")
    for z in (0.1, 0.55, 1.0, 1.45, 1.88):
        b.box((1.2, 0.45, 0.025), (0, 0, z), "Metal")
    return b.build()


def makeTable():
    """1.6 x 0.8m 탁자(높이 0.75)."""
    b = MeshBuilder("Table", "Prop", bevel=0.004)
    b.box((1.6, 0.8, 0.04), (0, 0, 0.73), "Wood")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.06, 0.06, 0.71), (sx * 0.74, sy * 0.34, 0.355), "WoodDark")
    return b.build()


def makeChair():
    """나무 의자. 등받이는 +Y 쪽."""
    b = MeshBuilder("Chair", "Prop", bevel=0.003)
    b.box((0.45, 0.45, 0.04), (0, 0, 0.45), "Wood")
    for sx in (-1, 1):
        b.box((0.035, 0.035, 0.43), (sx * 0.2, -0.2, 0.215), "WoodDark")
        b.box((0.035, 0.035, 0.9), (sx * 0.2, 0.2, 0.45), "WoodDark")
    b.box((0.44, 0.03, 0.22), (0, 0.2, 0.76), "Wood")
    return b.build()


def makeStool():
    """둥근 철제 스툴."""
    b = MeshBuilder("Stool", "Prop", bevel=0.003)
    b.cylinder(0.18, 0.18, 0.04, (0, 0, 0.62), "Wood", segments=16)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.03, 0.03, 0.6), (sx * 0.11, sy * 0.11, 0.3), "MetalDark")
    b.box((0.25, 0.02, 0.02), (0, -0.11, 0.25), "MetalDark")
    b.box((0.25, 0.02, 0.02), (0, 0.11, 0.25), "MetalDark")
    return b.build()


def addLocker(b, x):
    """사물함 1칸(0.5 x 0.5 x 1.9)을 빌더에 추가."""
    b.box((0.5, 0.5, 0.08), (x, 0, 0.04), "MetalDark")
    b.box((0.5, 0.5, 1.82), (x, 0, 0.99), "PaintGreen")
    b.box((0.44, 0.012, 1.7), (x, -0.256, 0.99), "PaintGreen")
    for z in (1.62, 1.67, 1.72):
        b.box((0.3, 0.012, 0.02), (x, -0.264, z), "MetalDark")
    b.box((0.03, 0.03, 0.16), (x + 0.17, -0.27, 1.0), "Metal")


def makeLocker():
    """사물함 1칸."""
    b = MeshBuilder("Locker", "Prop", bevel=0.004)
    addLocker(b, 0)
    return b.build()


def makeLockerRow():
    """사물함 3칸 묶음(폭 1.5m)."""
    b = MeshBuilder("Locker_Row3", "Prop", bevel=0.004)
    for x in (-0.5, 0, 0.5):
        addLocker(b, x)
    return b.build()


def makeHospitalBed():
    """병원 침대(0.95 x 2.05m). 머리는 +Y 쪽."""
    b = MeshBuilder("Bed_Hospital", "Prop", bevel=0.004)
    b.box((0.95, 2.05, 0.06), (0, 0, 0.5), "Metal")
    b.box((0.9, 2.0, 0.16), (0, 0, 0.61), "Fabric")
    b.box((0.6, 0.35, 0.1), (0, 0.75, 0.74), "White")
    b.box((0.92, 1.3, 0.04), (0, -0.33, 0.7), "FabricBlue")
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.cylinder(0.025, 0.025, 0.42, (sx * 0.44, sy * 0.98, 0.29), "Metal", segments=10)
            b.cylinder(0.045, 0.045, 0.03, (sx * 0.44, sy * 0.98, 0.045), "Rubber", segments=12, rot=rotY(90))
        b.cylinder(0.025, 0.025, 1.05, (sx * 0.45, 1.03, 0.525), "Metal", segments=10)
        b.cylinder(0.025, 0.025, 0.85, (sx * 0.45, -1.03, 0.425), "Metal", segments=10)
    b.box((0.86, 0.03, 0.35), (0, 1.03, 0.85), "White")
    b.box((0.86, 0.03, 0.22), (0, -1.03, 0.72), "White")
    return b.build()


def makeFileCabinet():
    """3단 서랍장(0.8 x 0.5 x 1.0)."""
    b = MeshBuilder("Cabinet_Drawers", "Prop", bevel=0.004)
    b.box((0.8, 0.5, 1.0), (0, 0, 0.5), "Metal")
    for z in (0.19, 0.51, 0.83):
        b.box((0.74, 0.02, 0.29), (0, -0.255, z), "Metal")
        b.box((0.22, 0.03, 0.025), (0, -0.275, z + 0.08), "MetalDark")
    return b.build()


def makeTrashBin():
    """철제 휴지통."""
    b = MeshBuilder("TrashBin", "Prop", bevel=0.003)
    b.cylinder(0.21, 0.24, 0.65, (0, 0, 0.325), "MetalDark", segments=18)
    b.cylinder(0.255, 0.255, 0.03, (0, 0, 0.645), "Metal", segments=18)
    return b.build()


def makeTrafficCone():
    """라바콘(높이 0.6)."""
    b = MeshBuilder("TrafficCone", "Prop", bevel=0.003)
    b.box((0.4, 0.4, 0.03), (0, 0, 0.015), "Orange")
    bottom, top, height = 0.15, 0.02, 0.55

    def radiusAt(z):
        return bottom - (bottom - top) * (z - 0.03) / height

    b.cylinder(bottom, top, height, (0, 0, 0.03 + height / 2), "Orange", segments=18)
    for z0, z1 in ((0.2, 0.27), (0.36, 0.42)):
        b.cylinder(radiusAt(z0) + 0.004, radiusAt(z1) + 0.004, z1 - z0, (0, 0, (z0 + z1) / 2), "White", segments=18)
    return b.build()


def makeBucket():
    """플라스틱 양동이."""
    b = MeshBuilder("Bucket", "Prop", bevel=0.003)
    b.cylinder(0.12, 0.15, 0.3, (0, 0, 0.15), "Yellow", segments=16)
    b.cylinder(0.155, 0.155, 0.02, (0, 0, 0.295), "Yellow", segments=16)
    return b.build()


def makePipeStraight():
    """2m 직선 배관(지름 0.2), X축 방향, 피벗은 축 중앙."""
    b = MeshBuilder("Pipe_Straight_2m", "Prop", bevel=0.003)
    b.cylinder(0.1, 0.1, 2.0, (0, 0, 0), "PaintGreen", segments=16, rot=rotY(90))
    for x in (-0.98, 0.98):
        b.cylinder(0.14, 0.14, 0.04, (x, 0, 0), "MetalDark", segments=16, rot=rotY(90))
    return b.build()


def makePipeElbow():
    """90도 엘보 배관. 피벗은 굽힘 중심, 끝단은 (0.3,0,0)과 (0,0.3,0)."""
    b = MeshBuilder("Pipe_Elbow", "Prop", bevel=0.003)
    b.pipeBend(0.3, 0.1, "PaintGreen")
    b.cylinder(0.14, 0.14, 0.04, (0.3, 0.02, 0), "MetalDark", segments=16, rot=rotX(90))
    b.cylinder(0.14, 0.14, 0.04, (0.02, 0.3, 0), "MetalDark", segments=16, rot=rotY(90))
    return b.build()


def makeDuct():
    """2m 사각 환기 덕트, X축 방향, 피벗은 축 중앙."""
    b = MeshBuilder("Duct_Straight_2m", "Prop", bevel=0.004)
    b.box((2.0, 0.5, 0.4), (0, 0, 0), "Metal")
    for x in (-0.97, 0.97):
        b.box((0.04, 0.56, 0.46), (x, 0, 0), "MetalDark")
    return b.build()


def makeCeilingLamp():
    """천장 매달림 조명. 피벗은 천장 부착점(윗면), 아래로 0.8m."""
    b = MeshBuilder("Lamp_Ceiling", "Prop", bevel=0.002)
    b.cylinder(0.07, 0.07, 0.03, (0, 0, -0.015), "MetalDark", segments=14)
    b.cylinder(0.006, 0.006, 0.6, (0, 0, -0.33), "Rubber", segments=6)
    b.cylinder(0.25, 0.06, 0.2, (0, 0, -0.7), "MetalDark", segments=20)
    b.sphere(0.06, (0, 0, -0.79), "Light")
    return b.build()


def makeSandbags():
    """모래주머니 엄폐물(폭 1.8, 높이 0.75)."""
    b = MeshBuilder("Sandbag_Wall", "Prop", bevel=0.03)
    bagWidth = 0.45
    for rowIndex, count in enumerate((4, 3, 2)):
        z = 0.12 + rowIndex * 0.24
        for i in range(count):
            x = (i - (count - 1) / 2) * bagWidth
            b.box((bagWidth - 0.02, 0.5, 0.24), (x, 0, z), "Cardboard", rot=rotZ(3 if (i + rowIndex) % 2 else -3))
    return b.build()


builders = [
    lambda: makeFloor(4), lambda: makeFloor(2),
    lambda: makeWall(4), lambda: makeWall(2), lambda: makeWall(1),
    makeWallDoor, makeWallWindow, makeWallHalf, makeDoor,
    makePillarSquare, makePillarRound, makeStairs, makeRamp, makePlatform,
    makeRailing, makeBeam, makeContainer,
    lambda: makeCrate(1.0, "Crate_1m"), lambda: makeCrate(0.6, "Crate_06m"),
    makeBarrel, makePallet, makeCardboardBox, makeCardboardStack,
    makeShelf, makeTable, makeChair, makeStool, makeLocker, makeLockerRow,
    makeHospitalBed, makeFileCabinet, makeTrashBin, makeTrafficCone, makeBucket,
    makePipeStraight, makePipeElbow, makeDuct, makeCeilingLamp, makeSandbags,
]


def main():
    """전체 오브젝트를 만들고 FBX/블렌드/미리보기를 저장한다."""
    resetScene()
    created = [fn() for fn in builders]
    for obj in created:
        exportFbx(obj, os.path.join(outDir, obj["mapkitCategory"]))
        print("[MapKit] exported", obj.name)

    structures = [o for o in created if o["mapkitCategory"] == "Structure"]
    props = [o for o in created if o["mapkitCategory"] == "Prop"]
    layoutGrid(structures, (0, 0, 0), 6, 7.0)
    layoutGrid(props, (0, -40, 0), 6, 2.6)

    if previewDir:
        os.makedirs(previewDir, exist_ok=True)
        renderPreview(structures, os.path.join(previewDir, "preview_structures.png"))
        renderPreview(props, os.path.join(previewDir, "preview_props.png"))
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=blendPath)
    print("[MapKit] done:", len(created), "objects")


main()
