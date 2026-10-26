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


class MeshBuilder:
    """기본 도형을 하나의 bmesh에 쌓아 오브젝트 하나로 만드는 도우미."""

    def __init__(self, name, category, bevel=0.01):
        self.name = name
        self.category = category
        self.bevel = bevel
        self.bm = bmesh.new()
        self.materialNames = []

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
        return obj


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
