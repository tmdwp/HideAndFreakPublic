"""
MapKit 모델 생성 스크립트들이 함께 쓰는 도구.
(generate_mapkit.py: 기본 구조물·소품, generate_subway_props.py: 지하철역 소품)

- 단위는 미터, 피벗은 바닥 중앙. 앞면은 블렌더 -Y 쪽(Unity에서 +Z).
- FBX는 Unity 축(-Z forward, Y up)에 맞춰 회전/스케일을 굽는다.
- UV는 월드 스케일 박스 투영(1 UV = 1m).
"""
import bpy
import bmesh
import math
import os
from contextlib import contextmanager
from mathutils import Matrix, Vector

# 머티리얼 이름 → (색, 금속도, 거칠기, 발광세기). configure()로 정한다.
_palette = {}
_prefix = "M_Kit_"
_materialCache = {}


def configure(palette, prefix):
    """스크립트마다 쓸 팔레트와 머티리얼 이름 접두어(예: "M_Kit_")를 정한다."""
    global _palette, _prefix
    _palette = palette
    _prefix = prefix
    _materialCache.clear()


def getMaterial(name):
    """팔레트 이름으로 공용 머티리얼(접두어 + 이름)을 만들거나 재사용한다."""
    if name in _materialCache:
        return _materialCache[name]
    color, metallic, roughness, emission = _palette[name]
    mat = bpy.data.materials.new(_prefix + name)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    bsdf = _principledNode(mat)
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = (*color, 1.0)
    mat.metallic = metallic
    mat.roughness = roughness
    _materialCache[name] = mat
    return mat


def _principledNode(mat):
    """머티리얼의 Principled BSDF 노드. 이름은 Blender 버전·UI 언어(번역)에 따라 달라 종류로 찾고, 없으면 만들어 출력에 잇는다."""
    nodes = mat.node_tree.nodes
    for node in nodes:
        if node.type == "BSDF_PRINCIPLED":
            return node
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    output = next((n for n in nodes if n.type == "OUTPUT_MATERIAL"), None) or nodes.new("ShaderNodeOutputMaterial")
    mat.node_tree.links.new(bsdf.outputs[0], output.inputs[0])
    return bsdf


def rotX(deg):
    """X축 회전 행렬."""
    return Matrix.Rotation(math.radians(deg), 4, "X")


def rotY(deg):
    """Y축 회전 행렬."""
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def rotZ(deg):
    """Z축 회전 행렬."""
    return Matrix.Rotation(math.radians(deg), 4, "Z")


# 디테일을 붙일 면 방향 → 면 좌표계(u: 가로 = X, v: 세로 = Z, 바깥 = -Y)를 그 방향으로 돌리는 회전.
FACING = {
    "-Y": Matrix(),
    "+Y": Matrix.Rotation(math.pi, 4, "Z"),
    "+X": Matrix.Rotation(math.pi / 2, 4, "Z"),
    "-X": Matrix.Rotation(-math.pi / 2, 4, "Z"),
    "+Z": Matrix.Rotation(-math.pi / 2, 4, "X"),
}

# 작은 디테일이 베벨을 받으면 박스 하나가 12 → 44 삼각형으로 불어나서, 디테일 엣지만 빼고 베벨하려고 쓰는 엣지 속성.
BEVEL_WEIGHT_ATTR = "bevel_weight_edge"


def _faceRot(facing):
    """facing이 이름이면 FACING에서 찾고, 기울어진 판처럼 따로 계산한 회전 행렬이면 그대로 쓴다."""
    return FACING[facing] if isinstance(facing, str) else facing


def surfaceOf(center, rot, halfDepth):
    """rot으로 돌린 판(두께 2*halfDepth)의 앞면(-Y쪽) 중심. 기울어진 판에 면 디테일을 붙일 때 facing=rot과 함께 쓴다."""
    return Vector(center) + rot.to_3x3() @ Vector((0.0, -halfDepth, 0.0))


class MeshBuilder:
    """기본 도형을 하나의 bmesh에 쌓아 오브젝트 하나로 만드는 도우미."""

    def __init__(self, name, category, bevel=0.01):
        self.name = name
        self.category = category
        self.bevel = bevel
        self.bm = bmesh.new()
        self.materialNames = []
        self.noBevelDepth = 0
        self.usedNoBevel = False
        # 레이어를 도중에 추가하면 이미 만든 BMVert 참조가 무효가 되므로 처음에 만들어 둔다(build에서 지운다)
        self.detailLayer = self.bm.verts.layers.int.new("detail")

    def materialIndex(self, matName):
        """머티리얼 슬롯 번호를 돌려준다(없으면 추가)."""
        if matName not in self.materialNames:
            self.materialNames.append(matName)
        return self.materialNames.index(matName)

    def applyFaces(self, verts, matName, smooth):
        """새로 만든 정점들에 붙은 면에 머티리얼/스무딩을 지정한다."""
        index = self.materialIndex(matName)
        faces = {f for v in verts for f in v.link_faces}
        for face in faces:
            face.material_index = index
            face.smooth = smooth(face) if callable(smooth) else smooth
        if self.noBevelDepth > 0:
            for v in verts:
                v[self.detailLayer] = 1
            self.usedNoBevel = True

    @contextmanager
    def noBevel(self):
        """이 블록 안에서 만든 도형은 베벨을 받지 않는다(볼트·라벨·슬릿 같은 작은 디테일용)."""
        self.noBevelDepth += 1
        try:
            yield
        finally:
            self.noBevelDepth -= 1

    # ------------------------------------------------------------ 면 디테일
    # 아래 도우미는 모두 '면 좌표'를 쓴다: center는 붙일 면 위의 점, (u, v)는 그 면의 가로·세로 오프셋.
    # 같은 디테일을 앞면·옆면·윗면에 그대로 재사용하려고 facing으로 방향만 바꾼다.

    def _facePoint(self, facing, center, u, v, out):
        """면 좌표(u, v, 바깥쪽 거리)를 월드 위치로 바꾼다."""
        return Vector(center) + _faceRot(facing).to_3x3() @ Vector((u, -out, v))

    def plate(self, facing, center, width, height, depth, matName, u=0.0, v=0.0, tilt=0.0):
        """면에 붙는 얇은 판(라벨·명판·패드). tilt는 면 가로축 기준 기울기(도)."""
        with self.noBevel():
            rot = _faceRot(facing) @ rotX(tilt)
            self.box((width, depth, height), self._facePoint(facing, center, u, v, depth / 2), matName, rot=rot)

    def bolts(self, facing, center, points, matName, radius=0.012, depth=0.008):
        """면 위 (u, v) 점마다 육각 볼트 머리."""
        with self.noBevel():
            rot = _faceRot(facing) @ rotX(90)
            for u, v in points:
                self.cylinder(radius, radius * 0.9, depth, self._facePoint(facing, center, u, v, depth / 2),
                              matName, segments=6, rot=rot)

    def cornerBolts(self, facing, center, width, height, matName, inset=0.03, radius=0.012, depth=0.008):
        """width x height 판의 네 모서리 안쪽에 볼트."""
        du, dv = width / 2 - inset, height / 2 - inset
        self.bolts(facing, center, [(-du, -dv), (du, -dv), (-du, dv), (du, dv)], matName, radius, depth)

    def handle(self, facing, center, length, matName, vertical=True, standoff=0.03, thickness=0.02, u=0.0, v=0.0):
        """막대 손잡이와 양끝 받침(문·서랍·카트)."""
        with self.noBevel():
            rot = _faceRot(facing)
            bar = (thickness, thickness, length) if vertical else (length, thickness, thickness)
            self.box(bar, self._facePoint(facing, center, u, v, standoff + thickness / 2), matName, rot=rot)
            for end in (-1, 1):
                du, dv = (0.0, end * (length / 2 - thickness)) if vertical else (end * (length / 2 - thickness), 0.0)
                self.box((thickness * 0.8, standoff, thickness * 0.8),
                         self._facePoint(facing, center, u + du, v + dv, standoff / 2), matName, rot=rot)

    def frame(self, facing, center, width, height, border, depth, matName, u=0.0, v=0.0):
        """면 위 직사각 테두리(화면 베젤·창틀·투입구 테두리). width x height는 바깥 크기."""
        with self.noBevel():
            rot = _faceRot(facing)
            for side in (-1, 1):
                self.box((width, depth, border),
                         self._facePoint(facing, center, u, v + side * (height - border) / 2, depth / 2), matName, rot=rot)
                self.box((border, depth, height - 2 * border),
                         self._facePoint(facing, center, u + side * (width - border) / 2, v, depth / 2), matName, rot=rot)

    def louver(self, facing, center, width, height, count, matName, depth=0.015, u=0.0, v=0.0):
        """가로 슬릿 환기구: 기울어진 날개 count개."""
        with self.noBevel():
            pitch = height / count
            for i in range(count):
                dv = -height / 2 + pitch * (i + 0.5)
                self.box((width, depth, pitch * 0.55), self._facePoint(facing, center, u, v + dv, depth / 2),
                         matName, rot=_faceRot(facing) @ rotX(-35))

    def hinges(self, facing, center, u, vs, matName, radius=0.012, length=0.07):
        """문 옆선(u)의 세로 경첩 원통(vs 높이마다)."""
        with self.noBevel():
            for v in vs:
                self.cylinder(radius, radius, length, self._facePoint(facing, center, u, v, radius * 0.6),
                              matName, segments=8)

    def textBars(self, facing, center, width, lineHeight, lines, matName, depth=0.003, u=0.0, v=0.0, gap=1.8):
        """글자 대신 쓰는 짧은 막대 줄(안내판·라벨의 글씨 느낌). lines는 줄마다 폭 비율."""
        with self.noBevel():
            for i, ratio in enumerate(lines):
                dv = -i * lineHeight * gap
                w = width * ratio
                self.box((w, depth, lineHeight), self._facePoint(facing, center, u - (width - w) / 2, v + dv, depth / 2),
                         matName, rot=_faceRot(facing))

    def box(self, size, center, matName, rot=None):
        """직육면체. size=(x,y,z), center=중심 위치."""
        verts = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        matrix = Matrix.Translation(center) @ (rot or Matrix()) @ Matrix.Diagonal((*size, 1.0))
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.applyFaces(verts, matName, False)

    def cylinder(self, radiusBottom, radiusTop, depth, center, matName, segments=16, rot=None):
        """원기둥/원뿔. 기본은 Z축 방향이며 rot으로 눕힌다. 옆면만 부드럽게 셰이딩."""
        verts = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                                      radius1=radiusBottom, radius2=radiusTop, depth=depth)["verts"]
        matrix = Matrix.Translation(center) @ (rot or Matrix())
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.applyFaces(verts, matName, lambda f: len(f.verts) == 4)

    def tube(self, start, end, radius, matName, segments=8):
        """두 점을 잇는 원기둥(자전거 뼈대·지지대처럼 비스듬한 막대를 끝점으로 놓으려고)."""
        start, end = Vector(start), Vector(end)
        axis = end - start
        rot = axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
        self.cylinder(radius, radius, axis.length, (start + end) / 2, matName, segments=segments, rot=rot)

    def hollowCylinder(self, radiusBottom, radiusTop, depth, wall, center, matName, floor=None, innerMat=None, segments=16, rot=None):
        """
        속이 빈 원통: 바깥 옆면 + 윗 테두리 + 안쪽 옆면 + (floor가 있으면) 안쪽 바닥·바깥 바닥, 없으면 아래도 뚫린 관.
        컵·양동이·휴지통(floor 두께 지정), 테이프 심지·갓(floor=None). 기본은 Z축 방향이고 rot으로 눕히거나 납작하게(Diagonal) 바꾼다.
        안쪽 면은 innerMat(없으면 matName)으로 칠한다. 닫힌 면이라 법선은 build()에서 바깥을 보게 맞춰진다.
        """
        bm = self.bm
        z0, z1 = -depth / 2, depth / 2
        zf = z0 + floor if floor is not None else z0

        def radiusAt(z):
            return radiusBottom + (radiusTop - radiusBottom) * (z - z0) / depth

        def ring(radius, z):
            return [bm.verts.new((radius * math.cos(2 * math.pi * k / segments), radius * math.sin(2 * math.pi * k / segments), z))
                    for k in range(segments)]

        outerBottom = ring(radiusBottom, z0)
        outerTop = ring(radiusTop, z1)
        innerTop = ring(radiusTop - wall, z1)
        innerBottom = ring(radiusAt(zf) - wall, zf)
        rims = []
        for k in range(segments):
            k2 = (k + 1) % segments
            bm.faces.new([outerBottom[k], outerBottom[k2], outerTop[k2], outerTop[k]])
            rims.append(bm.faces.new([outerTop[k], outerTop[k2], innerTop[k2], innerTop[k]]))
            bm.faces.new([innerTop[k], innerTop[k2], innerBottom[k2], innerBottom[k]])
        if floor is not None:
            bm.faces.new(innerBottom)
            bm.faces.new(list(reversed(outerBottom)))
        else:
            for k in range(segments):
                k2 = (k + 1) % segments
                rims.append(bm.faces.new([innerBottom[k], innerBottom[k2], outerBottom[k2], outerBottom[k]]))

        allVerts = outerBottom + outerTop + innerTop + innerBottom
        matrix = Matrix.Translation(center) @ (rot or Matrix())
        bmesh.ops.transform(bm, matrix=matrix, verts=allVerts)
        self.applyFaces(outerBottom + outerTop, matName, lambda f: len(f.verts) == 4)
        self.applyFaces(innerTop + innerBottom, innerMat or matName, lambda f: len(f.verts) == 4)
        # 테두리 고리 면은 평평하게 (옆면만 부드럽게)
        for face in rims:
            face.smooth = False

    def hollowBox(self, size, wall, center, matName, floor=None, innerMat=None):
        """위가 열린 상자(세면대·서랍 통): 바닥판(floor 두께, 기본 wall) + 옆판 4개. center는 상자 중심, size는 바깥 크기."""
        sx, sy, sz = size
        cx, cy, cz = center
        floor = wall if floor is None else floor
        inner = innerMat or matName
        self.box((sx, sy, floor), (cx, cy, cz - sz / 2 + floor / 2), inner)
        for side in (-1, 1):
            self.box((wall, sy, sz), (cx + side * (sx / 2 - wall / 2), cy, cz), matName)
            self.box((sx - 2 * wall, wall, sz), (cx, cy + side * (sy / 2 - wall / 2), cz), matName)

    def sphere(self, radius, center, matName, segments=12, scale=(1.0, 1.0, 1.0)):
        """구(scale로 납작하게 할 수 있다)."""
        verts = bmesh.ops.create_uvsphere(self.bm, u_segments=segments, v_segments=segments // 2,
                                          radius=radius)["verts"]
        matrix = Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1.0))
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.applyFaces(verts, matName, True)

    def polyhedron(self, points, faceIndices, matName):
        """정점 목록과 면 인덱스로 임의 다면체(경사로 등)를 만든다."""
        verts = [self.bm.verts.new(p) for p in points]
        for indices in faceIndices:
            self.bm.faces.new([verts[i] for i in indices])
        self.applyFaces(verts, matName, False)

    def pipeBend(self, bendRadius, pipeRadius, matName, angleDeg=90, steps=8, ringSegments=14):
        """원점 중심 XY 평면 위의 굽은 배관(엘보)."""
        rings = []
        for s in range(steps + 1):
            a = math.radians(angleDeg) * s / steps
            centerPoint = Vector((bendRadius * math.cos(a), bendRadius * math.sin(a), 0.0))
            radial = Vector((math.cos(a), math.sin(a), 0.0))
            up = Vector((0.0, 0.0, 1.0))
            ring = []
            for k in range(ringSegments):
                t = 2 * math.pi * k / ringSegments
                ring.append(self.bm.verts.new(centerPoint + pipeRadius * (math.cos(t) * radial + math.sin(t) * up)))
            rings.append(ring)
        for s in range(steps):
            for k in range(ringSegments):
                k2 = (k + 1) % ringSegments
                self.bm.faces.new([rings[s][k], rings[s][k2], rings[s + 1][k2], rings[s + 1][k]])
        self.bm.faces.new(rings[0])
        self.bm.faces.new(list(reversed(rings[-1])))
        allVerts = [v for ring in rings for v in ring]
        self.applyFaces(allVerts, matName, lambda f: len(f.verts) == 4)

    def boxProjectUv(self):
        """면 법선의 주축 기준 월드 스케일 박스 UV(1 UV = 1m)."""
        uvLayer = self.bm.loops.layers.uv.verify()
        for face in self.bm.faces:
            n = face.normal
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            for loop in face.loops:
                co = loop.vert.co
                if ax >= ay and ax >= az:
                    uv = (co.y * (1 if n.x > 0 else -1), co.z)
                elif ay >= az:
                    uv = (co.x * (-1 if n.y > 0 else 1), co.z)
                else:
                    uv = (co.x, co.y * (1 if n.z > 0 else -1))
                loop[uvLayer].uv = uv

    def build(self):
        """bmesh를 메시 오브젝트로 만들고 모서리 베벨 모디파이어를 붙인다."""
        ngons = [f for f in self.bm.faces if len(f.verts) > 4]
        bmesh.ops.triangulate(self.bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.normal_update()
        self.boxProjectUv()
        if self.usedNoBevel and self.bevel > 0:
            self._writeBevelWeights()
        self.bm.verts.layers.int.remove(self.bm.verts.layers.int["detail"])
        mesh = bpy.data.meshes.new("SM_" + self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for matName in self.materialNames:
            mesh.materials.append(getMaterial(matName))
        obj = bpy.data.objects.new("SM_" + self.name, mesh)
        obj["mapkitCategory"] = self.category
        bpy.context.scene.collection.objects.link(obj)
        if self.bevel > 0:
            mod = obj.modifiers.new("Bevel", "BEVEL")
            mod.width = self.bevel
            mod.segments = 1
            mod.limit_method = "ANGLE"
            mod.angle_limit = math.radians(40)
            mod.harden_normals = False
            if self.usedNoBevel:
                # 베벨 모디파이어는 각도 제한과 정점 그룹 제한을 함께 못 써서, 각도 판정을 미리 해 엣지 가중치로 넘긴다
                mod.limit_method = "WEIGHT"
                mod.edge_weight = BEVEL_WEIGHT_ATTR
        return obj

    def _writeBevelWeights(self):
        """각도 40도 넘는 엣지 중 디테일(NoBevel) 정점끼리의 엣지를 뺀 것에만 베벨 가중치 1을 준다."""
        weights = self.bm.edges.layers.float.new(BEVEL_WEIGHT_ATTR)
        detail = self.bm.verts.layers.int["detail"]
        limit = math.radians(40)
        for edge in self.bm.edges:
            isDetail = all(v[detail] for v in edge.verts)
            angle = edge.calc_face_angle(None)
            edge[weights] = 1.0 if (not isDetail and angle is not None and angle > limit) else 0.0


def resetScene():
    """기본 씬의 오브젝트/메시/머티리얼을 비운다."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    _materialCache.clear()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def exportFbx(obj, folder):
    """오브젝트 하나를 원점에 두고 Unity용 FBX로 내보낸다."""
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
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


def layoutGrid(objs, origin, columns, cell):
    """미리보기/블렌드 파일용으로 오브젝트를 격자로 늘어놓고 이름표를 붙인다."""
    for i, obj in enumerate(objs):
        col, row = i % columns, i // columns
        obj.location = (origin[0] + col * cell, origin[1] - row * cell, origin[2])
        if obj.name == "SM_Lamp_Ceiling":
            obj.location.z += 0.9
        if obj.name.startswith("SM_Pipe") or obj.name.startswith("SM_Duct"):
            obj.location.z += 0.25
        curve = bpy.data.curves.new("Label_" + obj.name, "FONT")
        curve.body = obj.name[3:]
        curve.size = cell * 0.07
        curve.align_x = "CENTER"
        label = bpy.data.objects.new("Label_" + obj.name, curve)
        label.location = (obj.location.x, obj.location.y - cell * 0.42, 0.01)
        bpy.context.scene.collection.objects.link(label)


def renderPreview(objs, path, elevation=50, azimuth=30):
    """워크벤치로 오브젝트 묶음 미리보기 PNG를 렌더한다."""
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
        scene.display.shading.show_shadows = True
        scene.display.shading.show_cavity = True
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 1800, 1100
    bpy.context.view_layer.update()
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        for corner in obj.bound_box:
            p = obj.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, p))
            hi = Vector(map(max, hi, p))
    center = (lo + hi) / 2
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.rotation_euler = (math.radians(90 - elevation), 0, math.radians(azimuth))
    bpy.context.view_layer.update()
    direction = cam.matrix_world.to_3x3() @ Vector((0, 0, 1))
    cam.location = center + direction * 100
    cam.data.clip_end = 500
    cam.data.ortho_scale = (hi - lo).length * 1.15
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
