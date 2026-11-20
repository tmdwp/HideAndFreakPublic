"""
숲 맵 Terrain 나무용 로우폴리 나무(MapKit Nature) 생성 스크립트. 침엽수·활엽수·자작나무를 각진 면(플랫 셰이딩)으로 만든다.

실행:
  blender -b --python generate_trees.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 밑동 중앙, 메시 하나)
Terrain 나무는 오브젝트 회전 없이 심기므로 메시에 회전을 굽지 않고, 정점 흔들기는 고정 시드로 매번 같은 모양을 만든다.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import configure, exportFbx, layoutGrid, renderPreview, resetScene, rotX, rotY
from nature_common import NatureBuilder as TreeBuilder

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models", "Nature")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "Bark":       ((0.30, 0.19, 0.11), 0.0, 0.95, 0.0),
    "BarkBirch":  ((0.86, 0.85, 0.80), 0.0, 0.85, 0.0),
    "BarkMark":   ((0.12, 0.11, 0.10), 0.0, 0.9, 0.0),
    "LeafPine":   ((0.10, 0.28, 0.14), 0.0, 0.9, 0.0),
    "LeafPineDark": ((0.07, 0.21, 0.12), 0.0, 0.9, 0.0),
    "Leaf":       ((0.24, 0.45, 0.15), 0.0, 0.9, 0.0),
    "LeafLight":  ((0.45, 0.60, 0.20), 0.0, 0.9, 0.0),
}
configure(palette, "M_Nature_")


# ---------------------------------------------------------------- 침엽수

def makePine(name, seed, height, layers, radius, matName):
    """원뿔을 층층이 쌓은 침엽수. height는 전체 높이, layers는 잎 층 수, radius는 맨 아래 층 반지름."""
    b = TreeBuilder(name, seed)
    trunkHeight = height * 0.35
    b.trunk(radius * 0.14, radius * 0.09, trunkHeight + height * 0.2, "Bark", jitter=0.01)
    foliageBase = trunkHeight * 0.7
    foliageHeight = height - foliageBase
    step = foliageHeight / (layers + 1)
    for i in range(layers):
        t = i / max(1, layers - 1)
        layerRadius = radius * (1.0 - 0.55 * t)
        layerHeight = step * 2.0 if i < layers - 1 else height - (foliageBase + step * i)
        b.cone(layerRadius, layerHeight, foliageBase + step * i, matName, segments=10,
               jitter=layerRadius * 0.06, twist=b.rng.uniform(0, 45), serrate=0.55)
    # 뿌리는 마지막에 붙인다: 앞의 무작위 값 순서를 바꾸지 않아 잎 모양이 그대로 남는다
    b.roots(4, radius * 0.14, radius * 0.35, "Bark")
    return b.build()


# ---------------------------------------------------------------- 활엽수

def makeRoundTree(name, seed, height, crownRadius, clusters):
    """줄기 위에 흔든 다면체 잎 덩어리를 얹은 활엽수. clusters는 큰 덩어리 둘레의 작은 덩어리 수."""
    b = TreeBuilder(name, seed)
    trunkHeight = height * 0.4
    b.trunk(0.2, 0.12, trunkHeight + crownRadius * 0.4, "Bark", jitter=0.015)
    crownZ = trunkHeight + crownRadius * 0.7
    b.blob(crownRadius, (0, 0, crownZ), "Leaf", scale=(1.0, 1.0, 0.85), jitter=crownRadius * 0.1)
    for i in range(clusters):
        a = math.tau * i / clusters + b.rng.uniform(-0.3, 0.3)
        r = crownRadius * b.rng.uniform(0.55, 0.75)
        z = crownZ + crownRadius * b.rng.uniform(-0.35, 0.3)
        # 가지: 줄기 위쪽에서 작은 덩어리 쪽으로 기울인다(rotX는 -Y로 기울고, Z 회전으로 방향을 맞춘다).
        branchBase = trunkHeight * 0.85
        tilt = math.degrees(math.atan2(r, z - branchBase))
        branchLength = math.hypot(r, z - branchBase)
        b.trunk(0.07, 0.04, branchLength * 0.8, "Bark", segments=5, base=(0, 0, branchBase),
                rot=Matrix.Rotation(a + math.pi / 2, 4, "Z") @ rotX(tilt))
        color = "LeafLight" if i % 2 == 0 else "Leaf"
        b.blob(crownRadius * b.rng.uniform(0.5, 0.65), (r * math.cos(a), r * math.sin(a), z), color,
               scale=(1.0, 1.0, 0.8), jitter=crownRadius * 0.06)
    # 수관 겉에 작은 잎 뭉치를 더해 큰 다면체 면이 그대로 드러나지 않게 한다
    for i in range(3):
        a = math.tau * (i + 0.5) / 3 + b.rng.uniform(-0.4, 0.4)
        r = crownRadius * 0.85
        b.blob(crownRadius * 0.28, (r * math.cos(a), r * math.sin(a), crownZ + crownRadius * b.rng.uniform(0.2, 0.55)),
               "LeafLight" if i % 2 else "Leaf", scale=(1.0, 1.0, 0.8), jitter=crownRadius * 0.03)
    # 줄기 중간에서 갈라진 굵은 가지 하나: 수관 아래로 줄기만 곧게 보이지 않게
    forkAngle = b.rng.uniform(0, math.tau)
    b.trunk(0.08, 0.05, crownRadius * 0.9, "Bark", segments=5, base=(0, 0, trunkHeight * 0.6),
            rot=Matrix.Rotation(forkAngle, 4, "Z") @ rotY(35))
    b.roots(4, 0.2, 0.45, "Bark")
    return b.build()


def makeBirch(name, seed, height):
    """가늘고 흰 줄기에 길쭉한 잎 덩어리 두세 개를 얹은 자작나무."""
    b = TreeBuilder(name, seed)
    b.trunk(0.13, 0.06, height * 0.92, "BarkBirch", jitter=0.008)
    b.blob(height * 0.16, (0.05, 0.0, height * 0.72), "LeafLight", scale=(1.0, 1.0, 1.5), jitter=height * 0.015)
    b.blob(height * 0.12, (-0.25, 0.15, height * 0.55), "LeafLight", scale=(1.0, 1.0, 1.3), jitter=height * 0.012)
    b.blob(height * 0.11, (0.22, -0.2, height * 0.5), "Leaf", scale=(1.0, 1.0, 1.3), jitter=height * 0.012)
    # 자작나무다운 검은 껍질 무늬: 줄기 둘레에 납작한 조각을 높이마다 엇갈려 붙인다
    trunkHeight = height * 0.92
    for k in range(10):
        z = trunkHeight * (0.08 + 0.07 * k)
        radius = 0.13 - 0.07 * z / trunkHeight
        a = math.radians(k * 137.5)
        b.box((radius * 1.1, 0.03, 0.05 + 0.03 * (k % 3)), (radius * 0.98 * math.cos(a), radius * 0.98 * math.sin(a), z),
              "BarkMark", rot=Matrix.Rotation(a + math.pi / 2, 4, "Z"))
    for target in ((-0.25, 0.15, height * 0.55), (0.22, -0.2, height * 0.5)):
        a = math.atan2(target[1], target[0])
        b.trunk(0.03, 0.015, math.hypot(target[0], target[1]) * 1.3, "BarkBirch", segments=4, base=(0, 0, target[2] - 0.35),
                rot=Matrix.Rotation(a, 4, "Z") @ rotY(55))
    b.roots(3, 0.13, 0.3, "BarkBirch")
    return b.build()


def makeDeadTree(name, seed, height):
    """잎 없이 가지만 남은 마른 나무(분위기용)."""
    b = TreeBuilder(name, seed)
    b.trunk(0.22, 0.07, height, "Bark", jitter=0.02)
    branches = []
    for i in range(5):
        a = math.tau * i / 5 + b.rng.uniform(-0.4, 0.4)
        z = height * b.rng.uniform(0.4, 0.8)
        length = height * b.rng.uniform(0.25, 0.4)
        tilt = b.rng.uniform(35, 60)
        b.trunk(0.06, 0.015, length, "Bark", segments=4, base=(0, 0, z),
                rot=Matrix.Rotation(a, 4, "Z") @ rotY(tilt))
        branches.append((a, z, length, tilt))
    # 가지마다 잔가지 하나, 꺾인 꼭대기 끝, 뿌리: 앙상한 막대 몇 개보다 마른 나무답게
    for a, z, length, tilt in branches:
        rot = Matrix.Rotation(a, 4, "Z") @ rotY(tilt)
        fork = rot.to_3x3() @ Vector((0, 0, length * 0.55))
        side = 1 if b.rng.random() < 0.5 else -1
        b.trunk(0.03, 0.008, length * 0.45, "Bark", segments=3, base=(fork.x, fork.y, z + fork.z),
                rot=Matrix.Rotation(a + side * 0.7, 4, "Z") @ rotY(tilt - 20))
    b.trunk(0.07, 0.03, height * 0.05, "Bark", segments=4, base=(0, 0, height - 0.02),
            rot=Matrix.Rotation(b.rng.uniform(0, math.tau), 4, "Z") @ rotY(40))
    b.roots(5, 0.22, 0.5, "Bark")
    return b.build()


BUILDERS = [
    lambda: makePine("Tree_Pine_A", 11, 6.5, 3, 1.7, "LeafPine"),
    lambda: makePine("Tree_Pine_B", 23, 9.0, 4, 2.0, "LeafPineDark"),
    lambda: makePine("Tree_Pine_C", 37, 4.5, 3, 1.3, "LeafPine"),
    lambda: makeRoundTree("Tree_Round_A", 41, 5.5, 1.6, 4),
    lambda: makeRoundTree("Tree_Round_B", 53, 7.0, 2.0, 5),
    lambda: makeBirch("Tree_Birch_A", 67, 6.5),
    lambda: makeDeadTree("Tree_Dead_A", 79, 5.0),
]


def main():
    """나무를 모두 만들어 FBX로 내보내고, 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    objs = [build() for build in BUILDERS]
    for obj in objs:
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"[Trees] {obj.name}: {tris} tris -> {exportFbx(obj, outDir)}")
    if blendPath or previewDir:
        layoutGrid(objs, (0, 0, 0), 4, 7.0)
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        renderPreview(objs, os.path.join(os.path.abspath(previewDir), "preview_trees.png"), elevation=25)


main()
