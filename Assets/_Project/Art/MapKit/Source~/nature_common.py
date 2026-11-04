"""
자연물(나무·덤불) 생성 스크립트가 함께 쓰는 도구. (generate_trees.py, generate_bushes.py)
규칙은 mapkit_common.py와 같다. (미터 단위, 피벗은 밑동 중앙, 메시 하나)
정점 흔들기는 고정 시드로 매번 같은 모양을 만든다.
"""
import math
import random

import bmesh
from mathutils import Matrix, Vector

from mapkit_common import MeshBuilder


class NatureBuilder(MeshBuilder):
    """정점을 흔들어 각진 로우폴리 모양을 만들고 모든 면을 플랫 셰이딩하는 MeshBuilder."""

    def __init__(self, name, seed):
        super().__init__(name, "Nature", bevel=0.0)
        self.rng = random.Random(seed)

    def jitter(self, verts, amount, keepBottom=False):
        """정점마다 무작위로 조금씩 옮긴다. keepBottom이면 가장 낮은 고리는 높이를 유지한다."""
        minZ = min(v.co.z for v in verts)
        for v in verts:
            offset = Vector((self.rng.uniform(-amount, amount),
                             self.rng.uniform(-amount, amount),
                             self.rng.uniform(-amount, amount)))
            if keepBottom and abs(v.co.z - minZ) < 1e-4:
                offset.z = 0.0
            v.co += offset

    def trunk(self, radiusBottom, radiusTop, height, matName, segments=6, base=(0, 0, 0), rot=None, jitter=0.0):
        """밑면이 base에 닿는 각진 줄기/가지. rot은 밑면 기준으로 기울인다."""
        verts = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                                      radius1=radiusBottom, radius2=radiusTop, depth=height)["verts"]
        bmesh.ops.translate(self.bm, vec=Vector((0, 0, height / 2)), verts=verts)
        if jitter > 0:
            self.jitter(verts, jitter, keepBottom=True)
        matrix = Matrix.Translation(base) @ (rot or Matrix())
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.applyFaces(verts, matName, False)

    def cone(self, radius, height, baseZ, matName, segments=8, jitter=0.0, twist=0.0):
        """밑면 높이가 baseZ인 뾰족한 원뿔(침엽수 층). twist(도)로 층마다 각을 엇갈린다."""
        verts = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                                      radius1=radius, radius2=0.0, depth=height)["verts"]
        matrix = Matrix.Translation((0, 0, baseZ + height / 2)) @ Matrix.Rotation(math.radians(twist), 4, "Z")
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        bmesh.ops.remove_doubles(self.bm, verts=verts, dist=1e-4)
        verts = [v for v in verts if v.is_valid]
        if jitter > 0:
            self.jitter(verts, jitter)
        self.applyFaces(verts, matName, False)

    def blob(self, radius, center, matName, scale=(1.0, 1.0, 1.0), jitter=0.0):
        """정이십면체(1단계 분할)를 흔든 덩어리 잎."""
        verts = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=radius)["verts"]
        if jitter > 0:
            self.jitter(verts, jitter)
        matrix = Matrix.Translation(center) @ Matrix.Rotation(self.rng.uniform(0, math.tau), 4, "Z") \
            @ Matrix.Diagonal((*scale, 1.0))
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.applyFaces(verts, matName, False)
