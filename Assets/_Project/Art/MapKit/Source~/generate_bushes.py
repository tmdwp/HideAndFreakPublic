"""
숲 맵 변신 사물용 로우폴리 덤불(MapKit Nature) 생성 스크립트. 흔든 다면체 잎 덩어리를 모아 각진 면(플랫 셰이딩)으로 만든다.

실행:
  blender -b --python generate_bushes.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 밑동 중앙, 메시 하나)
마네킹이 변신할 수 있도록 가장 긴 변이 DisguiseMaxSize(3m)보다 작게 만든다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mapkit_common import configure, exportFbx, layoutGrid, renderPreview, resetScene, rotY
from nature_common import NatureBuilder
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models", "Nature")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "Bark":      ((0.30, 0.19, 0.11), 0.0, 0.95, 0.0),
    "Leaf":      ((0.24, 0.45, 0.15), 0.0, 0.9, 0.0),
    "LeafLight": ((0.45, 0.60, 0.20), 0.0, 0.9, 0.0),
    "LeafDark":  ((0.12, 0.32, 0.13), 0.0, 0.9, 0.0),
    "Berry":     ((0.62, 0.08, 0.10), 0.0, 0.5, 0.0),
}
configure(palette, "M_Nature_")


def ring(b, count, radius, z, size, matName, scale, jitterRatio=0.1, zRange=0.0):
    """중심 둘레에 잎 덩어리 count개를 고르게(조금씩 흐트러뜨려) 두른다."""
    for i in range(count):
        a = math.tau * i / count + b.rng.uniform(-0.25, 0.25)
        r = radius * b.rng.uniform(0.85, 1.1)
        s = size * b.rng.uniform(0.85, 1.1)
        center = (r * math.cos(a), r * math.sin(a), z + b.rng.uniform(-zRange, zRange))
        b.blob(s, center, matName if i % 2 == 0 else "LeafLight", scale=scale, jitter=s * jitterRatio)


# ---------------------------------------------------------------- 덤불

def makeRoundBush(name, seed):
    """둥글게 뭉친 기본 덤불. (높이 약 1.1m, 폭 약 1.6m)"""
    b = NatureBuilder(name, seed)
    b.blob(0.55, (0, 0, 0.5), "Leaf", scale=(1.0, 1.0, 0.9), jitter=0.06)
    ring(b, 5, 0.45, 0.35, 0.38, "Leaf", (1.0, 1.0, 0.85), zRange=0.08)
    return b.build()


def makeWideBush(name, seed):
    """낮고 옆으로 퍼진 덤불. (높이 약 0.8m, 폭 약 2.3×1.5m)"""
    b = NatureBuilder(name, seed)
    for i in range(7):
        t = i / 6 - 0.5
        x = t * 1.6 + b.rng.uniform(-0.08, 0.08)
        y = b.rng.uniform(-0.3, 0.3)
        size = 0.42 - abs(t) * 0.2
        b.blob(size, (x, y, size * 0.6), "Leaf" if i % 2 == 0 else "LeafLight", scale=(1.15, 1.0, 0.7),
               jitter=size * 0.12)
    b.blob(0.38, (0.1, 0.05, 0.42), "Leaf", scale=(1.2, 1.0, 0.75), jitter=0.04)
    return b.build()


def makeTallBush(name, seed):
    """가는 줄기 몇 개 위로 잎 덩어리를 층층이 쌓은 키 큰 덤불. (높이 약 1.9m)"""
    b = NatureBuilder(name, seed)
    for i in range(3):
        a = math.tau * i / 3 + b.rng.uniform(-0.3, 0.3)
        b.trunk(0.05, 0.025, 1.3, "Bark", segments=5, base=(0.08 * math.cos(a), 0.08 * math.sin(a), 0),
                rot=Matrix.Rotation(a, 4, "Z") @ rotY(b.rng.uniform(6, 14)))
    b.blob(0.45, (0, 0, 0.55), "LeafDark", scale=(1.0, 1.0, 1.0), jitter=0.05)
    b.blob(0.42, (0.08, -0.05, 1.0), "Leaf", scale=(1.0, 1.0, 1.1), jitter=0.05)
    b.blob(0.32, (-0.05, 0.06, 1.48), "LeafLight", scale=(1.0, 1.0, 1.2), jitter=0.04)
    ring(b, 3, 0.3, 0.8, 0.26, "LeafDark", (1.0, 1.0, 1.1), zRange=0.25)
    return b.build()


def makeBerryBush(name, seed, berries):
    """짙은 잎 덩어리에 빨간 열매가 박힌 덤불. (높이 약 1m, 폭 약 1.4m)"""
    b = NatureBuilder(name, seed)
    mainRadius, mainCenter, mainScale = 0.5, Vector((0, 0, 0.45)), (1.0, 1.0, 0.9)
    b.blob(mainRadius, mainCenter, "LeafDark", scale=mainScale, jitter=0.05)
    ring(b, 4, 0.4, 0.3, 0.33, "LeafDark", (1.0, 1.0, 0.85), zRange=0.06)
    for _ in range(berries):
        # 큰 덩어리 윗쪽 반구 겉면에 열매를 박는다.
        a = b.rng.uniform(0, math.tau)
        up = b.rng.uniform(0.0, 0.85)
        direction = Vector((math.cos(a) * math.sqrt(1 - up * up), math.sin(a) * math.sqrt(1 - up * up), up))
        point = mainCenter + Vector((direction.x * mainScale[0], direction.y * mainScale[1],
                                     direction.z * mainScale[2])) * mainRadius * 1.08
        b.blob(0.05, point, "Berry")
    return b.build()


BUILDERS = [
    lambda: makeRoundBush("Bush_Round_A", 101),
    lambda: makeWideBush("Bush_Wide_A", 113),
    lambda: makeTallBush("Bush_Tall_A", 127),
    lambda: makeBerryBush("Bush_Berry_A", 131, 16),
]


def main():
    """덤불을 모두 만들어 FBX로 내보내고, 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    objs = [build() for build in BUILDERS]
    for obj in objs:
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        size = obj.dimensions
        print(f"[Bushes] {obj.name}: {tris} tris, {size.x:.2f}x{size.y:.2f}x{size.z:.2f}m -> {exportFbx(obj, outDir)}")
    if blendPath or previewDir:
        layoutGrid(objs, (0, 0, 0), 4, 3.0)
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        renderPreview(objs, os.path.join(os.path.abspath(previewDir), "preview_bushes.png"), elevation=25)


main()
