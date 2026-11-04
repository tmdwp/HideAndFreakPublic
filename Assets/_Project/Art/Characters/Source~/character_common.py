"""
로우폴리 캐릭터(마네킹·가드·구울) 생성 스크립트가 함께 쓰는 도구.
MapKit(../../MapKit/Source~/mapkit_common.py)의 팔레트·머티리얼·도형 규칙을 그대로 쓴다.

- 단위는 미터, 발이 원점, 정면은 블렌더 -Y(Unity +Z). 캐릭터의 왼쪽은 블렌더 +X.
- 뼈대는 Unity Humanoid 자동 매핑이 알아보는 이름(Hips, Spine, LeftUpperArm …)을 쓴다. 기본 자세는 T자.
- 부위마다 도형 하나를 뼈 하나에 100% 가중치로 붙인다. (로우폴리라 자동 가중치보다 깔끔하고 결과가 항상 같다)
- FBX에는 뼈대 + 메시 하나가 들어간다.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import MeshBuilder  # noqa: E402

# Humanoid 뼈대 (이름, 부모, 머리 위치, 꼬리 위치). 사람 키 1.8m 기준, 왼쪽(+X)만 적고 오른쪽은 대칭으로 만든다.
_CENTER_BONES = [
    ("Hips", None, (0, 0, 0.95), (0, 0, 1.05)),
    ("Spine", "Hips", (0, 0, 1.05), (0, 0, 1.20)),
    ("Chest", "Spine", (0, 0, 1.20), (0, 0, 1.35)),
    ("UpperChest", "Chest", (0, 0, 1.35), (0, 0, 1.48)),
    ("Neck", "UpperChest", (0, 0, 1.48), (0, 0, 1.58)),
    ("Head", "Neck", (0, 0, 1.58), (0, 0, 1.80)),
]
_LEFT_BONES = [
    ("LeftShoulder", "UpperChest", (0.03, 0, 1.44), (0.17, 0, 1.44)),
    ("LeftUpperArm", "LeftShoulder", (0.17, 0, 1.44), (0.45, 0, 1.44)),
    ("LeftLowerArm", "LeftUpperArm", (0.45, 0, 1.44), (0.70, 0, 1.44)),
    ("LeftHand", "LeftLowerArm", (0.70, 0, 1.44), (0.86, 0, 1.44)),
    ("LeftUpperLeg", "Hips", (0.10, 0, 0.93), (0.10, 0, 0.50)),
    ("LeftLowerLeg", "LeftUpperLeg", (0.10, 0, 0.50), (0.10, 0, 0.09)),
    ("LeftFoot", "LeftLowerLeg", (0.10, 0, 0.09), (0.10, -0.12, 0.03)),
    ("LeftToes", "LeftFoot", (0.10, -0.12, 0.03), (0.10, -0.21, 0.03)),
]

# Unity Humanoid에 꼭 있어야 하는 뼈. (UpperChest·Shoulder·Toes·Neck은 있으면 쓰는 선택 뼈)
REQUIRED_BONES = [
    "Hips", "Spine", "Chest", "Head",
    "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
    "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot",
]


def _mirror(name):
    """왼쪽 뼈 이름을 오른쪽 이름으로 바꾼다. (Left로 시작하지 않으면 그대로)"""
    return "Right" + name[4:] if name and name.startswith("Left") else name


def humanoidBones(scale=1.0, overrides=None):
    """
    Humanoid 뼈대 정의 목록을 만든다. (이름, 부모, 머리, 꼬리)
    scale: 키 배율(1 = 1.8m). overrides: {왼쪽/가운데 뼈 이름: (머리, 꼬리)} — 비율이 다른 캐릭터(구울 등)가 위치를 바꿀 때.
    오른쪽 뼈는 왼쪽을 X 대칭으로 만든다.
    """
    overrides = overrides or {}
    bones = []
    for name, parent, head, tail in _CENTER_BONES + _LEFT_BONES:
        head, tail = overrides.get(name, (head, tail))
        head = Vector(head) * scale
        tail = Vector(tail) * scale
        bones.append((name, parent, head, tail))
    for name, parent, head, tail in list(bones):
        if name.startswith("Left"):
            bones.append((_mirror(name), _mirror(parent),
                          Vector((-head.x, head.y, head.z)), Vector((-tail.x, tail.y, tail.z))))
    return bones


def createArmature(name, bones):
    """뼈 정의 목록으로 뼈대 오브젝트를 만든다. (연결하지 않은 뼈, 비틀림 0)"""
    data = bpy.data.armatures.new(name)
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    edit = {}
    for boneName, parent, head, tail in bones:
        bone = data.edit_bones.new(boneName)
        bone.head = head
        bone.tail = tail
        bone.roll = 0.0
        bone.use_connect = False
        if parent:
            bone.parent = edit[parent]
        edit[boneName] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)
    return obj


class CharacterBuilder(MeshBuilder):
    """
    뼈에 붙는 도형을 하나의 메시에 쌓는 MeshBuilder. 도형을 만들 때마다 새 정점을 지정한 뼈에 100% 가중치로 묶는다.
    모든 면은 플랫 셰이딩(각진 로우폴리), 모서리 베벨은 쓰지 않는다.
    """

    def __init__(self, name, bones):
        super().__init__(name, "Character", bevel=0.0)
        self.bones = {boneName: (head, tail) for boneName, _, head, tail in bones}
        self.boneOrder = [boneName for boneName, _, _, _ in bones]
        self.deform = self.bm.verts.layers.deform.verify()

    # ------------------------------------------------------------ 가중치

    def _bind(self, boneName):
        """아직 뼈에 묶이지 않은 정점(방금 만든 도형)을 boneName에 묶고, 면을 플랫 셰이딩한다."""
        if boneName not in self.bones:
            raise KeyError(f"없는 뼈: {boneName}")
        group = self.boneOrder.index(boneName)
        for v in self.bm.verts:
            weights = v[self.deform]
            if len(weights) == 0:
                weights[group] = 1.0
                for face in v.link_faces:
                    face.smooth = False

    def head(self, boneName):
        """뼈 머리(관절) 위치."""
        return Vector(self.bones[boneName][0])

    def tail(self, boneName):
        """뼈 꼬리 위치."""
        return Vector(self.bones[boneName][1])

    def along(self, boneName, t):
        """뼈 머리(0)에서 꼬리(1) 사이의 위치."""
        return self.head(boneName).lerp(self.tail(boneName), t)

    # ------------------------------------------------------------ 뼈에 붙는 도형

    def boxOn(self, boneName, size, center, matName, rot=None):
        """뼈에 붙는 직육면체. size=(x,y,z), center=월드 위치."""
        self.box(size, center, matName, rot)
        self._bind(boneName)

    def sphereOn(self, boneName, radius, center, matName, segments=8, scale=(1.0, 1.0, 1.0)):
        """뼈에 붙는 각진 구(관절 공, 머리 등)."""
        self.sphere(radius, center, matName, segments=segments, scale=scale)
        self._bind(boneName)

    def segment(self, boneName, radiusStart, radiusEnd, matName, start=0.0, end=1.0, segments=8, flatten=1.0, offset=(0, 0, 0)):
        """
        뼈를 따라 놓이는 원기둥(팔·다리 마디). start/end는 뼈 길이 비율(0=머리, 1=꼬리, 1을 넘어도 됨).
        flatten: 앞뒤(Y) 방향 납작한 정도(1 = 원), offset: 월드 좌표 추가 이동.
        """
        a = self.along(boneName, start)
        b = self.along(boneName, end)
        direction = b - a
        length = direction.length
        rot = Vector((0, 0, 1)).rotation_difference(direction.normalized()).to_matrix().to_4x4()
        verts = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                                      radius1=radiusStart, radius2=radiusEnd, depth=length)["verts"]
        matrix = Matrix.Translation((a + b) / 2 + Vector(offset)) @ rot @ Matrix.Diagonal((1.0, flatten, 1.0, 1.0))
        bmesh.ops.transform(self.bm, matrix=matrix, verts=verts)
        self.applyFaces(verts, matName, False)
        self._bind(boneName)

    def ball(self, boneName, radius, matName, at=0.0, segments=8):
        """관절 공. at은 뼈 길이 비율(0 = 이 뼈의 머리 관절)."""
        self.sphereOn(boneName, radius, self.along(boneName, at), matName, segments=segments)

    # ------------------------------------------------------------ 완성

    def buildCharacter(self, armature):
        """메시를 만들고 정점 그룹(뼈 이름)과 Armature 모디파이어를 붙여 뼈대의 자식으로 둔다."""
        # 도형을 만든 뒤 남은 묶이지 않은 정점이 있으면 실수이므로 알린다.
        loose = sum(1 for v in self.bm.verts if len(v[self.deform]) == 0)
        if loose:
            raise RuntimeError(f"{self.name}: 뼈에 묶이지 않은 정점 {loose}개")
        obj = self.build()
        for boneName in self.boneOrder:
            obj.vertex_groups.new(name=boneName)
        obj.parent = armature
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = armature
        return obj


def countWeightedBones(meshObj):
    """정점이 하나라도 묶인 뼈 이름 목록."""
    used = set()
    for v in meshObj.data.vertices:
        for g in v.groups:
            if g.weight > 0:
                used.add(meshObj.vertex_groups[g.group].name)
    return sorted(used)


def validateHumanoid(armature, meshObj):
    """필수 Humanoid 뼈가 있고 가중치가 붙었는지 확인해 문제 목록을 돌려준다. (빈 목록 = 통과)"""
    problems = []
    names = {bone.name for bone in armature.data.bones}
    weighted = set(countWeightedBones(meshObj))
    for required in REQUIRED_BONES:
        if required not in names:
            problems.append(f"필수 뼈 없음: {required}")
        elif required not in weighted:
            problems.append(f"가중치 없는 필수 뼈: {required}")
    return problems


def setPose(armature, rotations):
    """포즈 확인용으로 뼈를 돌린다. rotations: {뼈 이름: (축, 각도)}. 빈 사전이면 T자로 되돌린다."""
    for pose in armature.pose.bones:
        pose.rotation_mode = "XYZ"
        pose.rotation_euler = (0, 0, 0)
    for boneName, (axis, degrees) in rotations.items():
        pose = armature.pose.bones[boneName]
        euler = [0.0, 0.0, 0.0]
        euler["XYZ".index(axis)] = math.radians(degrees)
        pose.rotation_euler = euler
    bpy.context.view_layer.update()


# 걷는 중간처럼 팔을 내리고 무릎·팔꿈치를 굽힌 확인용 자세.
# 뼈의 Y축은 뼈 방향(비틀림 축)이다. 비틀림 0인 팔은 X축으로 돌리면 위아래, Z축으로 돌리면 앞뒤로 움직인다.
CHECK_POSE = {
    "LeftUpperArm": ("X", -70), "RightUpperArm": ("X", -70),
    "LeftLowerArm": ("Z", -40), "RightLowerArm": ("Z", 40),
    "LeftUpperLeg": ("X", -30), "RightUpperLeg": ("X", 20),
    "LeftLowerLeg": ("X", 45), "RightLowerLeg": ("X", 15),
    "Spine": ("X", 8), "Head": ("Z", 20),
}


def renderCharacterPreview(objs, path, elevation=5, azimuth=0):
    """워크벤치로 세로 미리보기 PNG를 렌더한다. (키 전체가 들어가도록 높이 기준으로 화면을 맞춘다)"""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1000, 1300
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        evaluated = obj.evaluated_get(depsgraph)
        for v in evaluated.to_mesh().vertices:
            p = obj.matrix_world @ v.co
            lo = Vector(map(min, lo, p))
            hi = Vector(map(max, hi, p))
        evaluated.to_mesh_clear()
    center = (lo + hi) / 2
    cam = bpy.data.objects.get("PreviewCam")
    if cam is None:
        cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
        scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.sensor_fit = "VERTICAL"
    cam.rotation_euler = (math.radians(90 - elevation), 0, math.radians(azimuth))
    bpy.context.view_layer.update()
    direction = cam.matrix_world.to_3x3() @ Vector((0, 0, 1))
    cam.location = center + direction * 50
    cam.data.clip_end = 200
    size = hi - lo
    cam.data.ortho_scale = max(size.z * 1.12, max(size.x, size.y) * 1.12 * 1300 / 1000)
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def exportCharacterFbx(armature, meshObj, folder, fileName, withAnimation=False):
    """뼈대와 메시를 Unity용 FBX 하나로 내보낸다. (발 원점, -Z forward, Y up, 끝 뼈 없음)"""
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    meshObj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    path = os.path.join(folder, fileName + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=False,
        use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=False,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", use_armature_deform_only=True,
        bake_anim=withAnimation, bake_anim_use_all_actions=withAnimation, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        path_mode="AUTO", embed_textures=False,
    )
    return path
