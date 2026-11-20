"""
사냥 시간 구울(SK_Ghoul) 생성 스크립트. (Phase 11-4·11-5)
마네킹이 뒤틀린 모습: 키 2.4m(늘 구부정해 머리는 약 1.9m), 무릎 아래까지 내려오는 긴 팔, 긴 손톱 3개, 튀어나온 척추 마디,
비대칭 어깨, 세로로 찢어진 입과 빛나는 눈, 더러운 회색 몸과 검붉은 틈.

애니메이션(24fps, Blender에서 키를 직접 찍음):
- Ghoul_Idle    (반복) 구부정하게 숨 쉬다가 가끔 머리를 홱 꺾음
- Ghoul_Stalk   (반복) 긴 팔로 바닥을 짚으며 다가가는 걸음
- Ghoul_Crawl   (반복) 네 발로 빠르게 기어 달리기
- Ghoul_Attack  (한 번) 두 팔을 치켜들었다가 휘두르는 할퀴기 — 맞는 순간 ATTACK_HIT_FRAME
- Ghoul_Twitch  (반복, 덧입히는 레이어) 가끔 경련 — 첫 프레임이 기준(0)이고 나머지는 차이만 담는다
- Ghoul_Shriek  (한 번) 머리를 젖히고 떨며 비명
- Ghoul_Crouch     (반복) 은신: 깊게 웅크려 앞 바닥을 짚고 숨죽임
- Ghoul_CrouchDown (한 번) 선 자세 → 은신 자세로 웅크림
- Ghoul_CrouchUp   (한 번) 은신 자세 → 선 자세로 일어남
- Ghoul_Lunge      (한 번) 은신 자세에서 날아들어 덮치는 도약 공격 — 맞는 순간 LUNGE_HIT_FRAME
- Ghoul_Peek       (정지) 로비 편의점 유리에 손을 대고 안을 들여다보는 자세
- Ghoul_Stare      (정지) 로비에서 목을 빼고 머리를 꺾어 노려보는 자세(진열대 뒤·카메라 앞)
일부 키는 보간 없이(CONSTANT) 찍어 뚝뚝 끊기는 움직임을 만든다.

실행:
  blender -b --python generate_ghoul.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

리그는 Generic(Humanoid 아님)이다. 뼈 이름은 character_common의 이름을 그대로 쓰지만 비율이 달라 리타깃하지 않는다.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import configure, resetScene, rotX, rotY  # noqa: E402
from character_common import (CharacterBuilder, createArmature, exportCharacterFbx, humanoidBones,  # noqa: E402
                              renderCharacterPreview)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "GhoulSkin":   ((0.50, 0.48, 0.45), 0.0, 0.85, 0.0),
    "GhoulJoint":  ((0.30, 0.28, 0.27), 0.0, 0.8, 0.0),
    "GhoulCrack":  ((0.30, 0.04, 0.04), 0.0, 0.7, 0.0),
    "GhoulSocket": ((0.03, 0.02, 0.02), 0.0, 0.9, 0.0),
    "GhoulTeeth":  ((0.82, 0.78, 0.66), 0.0, 0.5, 0.0),
    "GhoulClaw":   ((0.10, 0.09, 0.08), 0.2, 0.4, 0.0),
    "GhoulEye":    ((1.00, 0.28, 0.12), 0.0, 0.3, 6.0),
    "GhoulWrap":   ((0.52, 0.47, 0.37), 0.0, 0.95, 0.0),
    "GhoulBruise": ((0.36, 0.33, 0.33), 0.0, 0.9, 0.0),
}
configure(palette, "M_Char_")

SKIN, JOINT, CRACK, SOCKET, TEETH, CLAW, EYE = ("GhoulSkin", "GhoulJoint", "GhoulCrack", "GhoulSocket",
                                                "GhoulTeeth", "GhoulClaw", "GhoulEye")
WRAP, BRUISE = "GhoulWrap", "GhoulBruise"

FPS = 24
HEIGHT_SCALE = 2.4 / 1.8   # 사람 뼈대를 2.4m로 키운다.
ROUND = 8

# 팔을 사람의 약 1.4배로 늘린다. (키를 키우기 전 사람 기준 좌표, 왼쪽만 — 오른쪽은 대칭)
ARM_OVERRIDES = {
    "LeftUpperArm": ((0.17, 0, 1.44), (0.55, 0, 1.44)),
    "LeftLowerArm": ((0.55, 0, 1.44), (0.97, 0, 1.44)),
    "LeftHand": ((0.97, 0, 1.44), (1.15, 0, 1.44)),
}

# 공격 클립에서 손이 앞으로 내려와 맞는 프레임. (Unity의 attackHitDelay = 전환 시간 + 이 프레임 / FPS)
ATTACK_HIT_FRAME = 11


# =============================================================== 모델

def makeTorso(b):
    """앙상한 골반·허리·갈비뼈 가슴, 튀어나온 척추 마디, 검붉은 틈."""
    b.segment("Hips", 0.13, 0.14, SKIN, start=-1.0, end=0.85, segments=ROUND, flatten=0.65)
    b.segment("Spine", 0.085, 0.1, SKIN, start=0.25, end=1.0, segments=ROUND, flatten=0.7)
    b.segment("Spine", 0.078, 0.078, JOINT, start=-0.05, end=0.3, segments=ROUND, flatten=0.7)
    b.segment("Chest", 0.13, 0.16, SKIN, start=0.0, end=1.0, segments=ROUND, flatten=0.6)
    b.segment("UpperChest", 0.16, 0.1, SKIN, start=0.0, end=0.95, segments=ROUND, flatten=0.58)
    # 등(+Y)으로 튀어나온 척추 마디
    for bone, t, back in (("Spine", 0.6, 0.075), ("Chest", 0.25, 0.1), ("Chest", 0.75, 0.105), ("UpperChest", 0.4, 0.1)):
        b.boxOn(bone, (0.04, 0.05, 0.045), b.along(bone, t) + Vector((0, back, 0)), JOINT, rot=rotX(30))
    # 갈라진 틈
    b.boxOn("Chest", (0.012, 0.008, 0.16), b.along("Chest", 0.6) + Vector((0.05, -0.1, 0)), CRACK, rot=rotY(25))
    b.boxOn("Spine", (0.01, 0.008, 0.11), b.along("Spine", 0.6) + Vector((-0.03, -0.072, 0)), CRACK, rot=rotY(-30))
    b.boxOn("UpperChest", (0.01, 0.008, 0.1), b.along("UpperChest", 0.4) + Vector((-0.07, -0.095, 0)), CRACK, rot=rotY(10))
    # 가죽 밑으로 비치는 갈비뼈: 가운데서 바깥 아래로 처지는 V자 막대 세 쌍
    for t in (0.2, 0.47, 0.74):
        center = b.along("Chest", t)
        for sx in (-1, 1):
            b.boxOn("Chest", (0.1, 0.014, 0.014), center + Vector((sx * 0.055, -0.094 + 0.006 * t, -0.012)), JOINT, rot=rotY(sx * 18))
    # 척추 마디를 골반·목까지 잇고, 엉덩이·허리 쪽 틈을 더한다
    for bone, t, back in (("Hips", 0.4, 0.09), ("Spine", 0.15, 0.072), ("Chest", 0.5, 0.108), ("UpperChest", 0.75, 0.085)):
        b.boxOn(bone, (0.035, 0.045, 0.04), b.along(bone, t) + Vector((0, back, 0)), JOINT, rot=rotX(30))
    b.boxOn("Neck", (0.025, 0.03, 0.03), b.along("Neck", 0.35) + Vector((0, 0.03, 0)), JOINT, rot=rotX(25))
    b.boxOn("Hips", (0.01, 0.008, 0.12), b.along("Hips", -0.3) + Vector((0.07, -0.088, 0)), CRACK, rot=rotY(-35))
    b.boxOn("Spine", (0.08, 0.008, 0.01), b.along("Spine", 0.2) + Vector((0.02, -0.072, 0)), CRACK, rot=rotY(8))
    # 썩은 멍 자국
    b.boxOn("Chest", (0.06, 0.006, 0.05), b.along("Chest", 0.3) + Vector((-0.07, -0.085, 0)), BRUISE, rot=rotY(20))
    b.boxOn("Hips", (0.07, 0.006, 0.05), b.along("Hips", 0.1) + Vector((-0.06, 0.088, 0)), BRUISE, rot=rotY(-15))


def makeHead(b):
    """긴 목, 마네킹 달걀형 머리, 세로로 찢어진 입과 이빨, 눈구멍 속 빛나는 눈."""
    b.segment("Neck", 0.036, 0.03, JOINT, start=-0.3, end=1.0, segments=6)
    head = b.along("Head", 0.4)
    b.sphereOn("Head", 0.13, head, SKIN, segments=10, scale=(0.85, 0.95, 1.15))
    for side in (-1, 1):
        b.sphereOn("Head", 0.03, head + Vector((side * 0.045, -0.1, 0.035)), SOCKET, segments=6)
        b.sphereOn("Head", 0.017, head + Vector((side * 0.045, -0.118, 0.035)), EYE, segments=6)
    b.boxOn("Head", (0.032, 0.02, 0.16), head + Vector((0, -0.113, -0.05)), SOCKET)
    for k in (-1, 0, 1):
        for side in (-1, 1):
            b.boxOn("Head", (0.012, 0.012, 0.02), head + Vector((side * 0.019, -0.12, -0.05 + k * 0.04)), TEETH)
    # 눈두덩(비대칭으로 한쪽이 더 처짐), 이마에서 정수리로 번진 금, 한쪽으로 비틀린 아래턱
    for side, droop in ((-1, 0.0), (1, -0.01)):
        b.boxOn("Head", (0.05, 0.022, 0.016), head + Vector((side * 0.045, -0.103, 0.066 + droop)), JOINT, rot=rotY(side * -14))
    for x, z, length, angle in ((0.02, 0.1, 0.07, 25), (0.04, 0.135, 0.05, -40), (-0.01, 0.12, 0.04, 70)):
        b.boxOn("Head", (0.008, 0.008, length), head + Vector((x, -0.085, z)), CRACK, rot=rotX(-35) @ rotY(angle))
    b.boxOn("Head", (0.085, 0.06, 0.03), head + Vector((0.012, -0.06, -0.125)), SKIN, rot=rotY(9))
    for side in (-1, 1):
        b.boxOn("Head", (0.01, 0.012, 0.022), head + Vector((0.012 + side * 0.022, -0.092, -0.108)), TEETH, rot=rotY(9))


def makeArm(b, side):
    """비대칭 어깨(왼쪽이 크다), 길고 가는 팔, 손바닥과 길게 휜 손톱 3개."""
    sign = 1 if side == "Left" else -1
    if side == "Left":
        b.ball("LeftUpperArm", 0.085, JOINT, segments=ROUND)
        b.boxOn("LeftShoulder", (0.12, 0.13, 0.12), b.along("LeftShoulder", 0.75) + Vector((0, 0, 0.03)), SKIN)
    else:
        b.ball("RightUpperArm", 0.06, JOINT, segments=ROUND)
    b.segment(side + "UpperArm", 0.045, 0.035, SKIN, start=0.05, end=0.95, segments=ROUND)
    b.ball(side + "LowerArm", 0.04, JOINT, segments=ROUND)
    b.segment(side + "LowerArm", 0.035, 0.025, SKIN, start=0.05, end=0.95, segments=ROUND)
    b.ball(side + "Hand", 0.028, JOINT, segments=6)
    # 손바닥은 손목에서 손 뼈 70%까지 덮고, 손톱은 손바닥 끝에서 바로 이어진다.
    palmLength = (b.tail(side + "Hand") - b.head(side + "Hand")).length * 0.7
    b.boxOn(side + "Hand", (palmLength, 0.065, 0.03), b.along(side + "Hand", 0.35), SKIN)
    palmEnd = b.along(side + "Hand", 0.7)
    for offset in (-0.022, 0.0, 0.022):
        center = palmEnd + Vector((sign * 0.115, offset, -0.015))
        b.boxOn(side + "Hand", (0.24, 0.013, 0.012), center, CLAW, rot=rotY(sign * 14))
        # 손톱 마디: 뿌리의 관절 혹과 끝에서 한 번 더 아래로 꺾인 갈고리
        b.boxOn(side + "Hand", (0.022, 0.02, 0.02), palmEnd + Vector((sign * 0.008, offset, -0.012)), JOINT)
        tip = center + Vector((sign * 0.145, 0, -0.045))
        b.boxOn(side + "Hand", (0.07, 0.011, 0.01), tip, CLAW, rot=rotY(sign * 40))
    # 터진 이음선(마네킹 몸이 찢어진 자리)과 더러운 붕대: 왼쪽은 손목, 오른쪽은 위팔에만 둬 좌우를 어긋나게
    if side == "Left":
        b.segment("LeftLowerArm", 0.032, 0.03, WRAP, start=0.8, end=0.93, segments=6)
        # 풀린 붕대 끝: 팔을 내려도 막대처럼 튀어나오지 않게 팔 아랫면을 따라 붙인다
        b.boxOn("LeftLowerArm", (0.1, 0.014, 0.004), b.along("LeftLowerArm", 0.76) + Vector((0, -0.008, -0.03)), WRAP, rot=rotX(-10))
        b.boxOn("LeftUpperArm", (0.01, 0.008, 0.09), b.along("LeftUpperArm", 0.4) + Vector((0, -0.044, 0)), CRACK, rot=rotY(60))
    else:
        b.segment("RightUpperArm", 0.048, 0.046, CRACK, start=0.47, end=0.53, segments=ROUND)
        for k in range(4):
            b.boxOn("RightUpperArm", (0.02, 0.012, 0.02), b.along("RightUpperArm", 0.5) + Vector((0, -0.04 if k % 2 else 0.04, 0.03 if k < 2 else -0.03)),
                    SKIN, rot=rotY(30 + k * 25))
        b.segment("RightUpperArm", 0.04, 0.038, WRAP, start=0.75, end=0.88, segments=6)


def makeLeg(b, side):
    """가는 다리와 뒤로 꺾여 보이는 긴 발, 발톱."""
    b.ball(side + "UpperLeg", 0.055, JOINT, segments=ROUND)
    b.segment(side + "UpperLeg", 0.06, 0.045, SKIN, start=0.05, end=0.95, segments=ROUND)
    b.ball(side + "LowerLeg", 0.045, JOINT, segments=ROUND)
    b.segment(side + "LowerLeg", 0.042, 0.03, SKIN, start=0.05, end=0.95, segments=ROUND)
    b.ball(side + "Foot", 0.03, JOINT, segments=6)
    # 발목에서 발끝까지 잇는 발등 마디와, 그 아래 길쭉한 발바닥. (발목 공과 틈이 없도록 높이를 맞춘다)
    b.segment(side + "Foot", 0.028, 0.022, SKIN, start=0.0, end=1.0, segments=6)
    heel = b.head(side + "Foot")
    toe = b.head(side + "Toes")
    b.boxOn(side + "Foot", (0.06, 0.24, 0.06), Vector((heel.x, (heel.y + toe.y) / 2, 0.05)), SKIN, rot=rotX(-8))
    for offset in (-0.02, 0.0, 0.02):
        b.boxOn(side + "Toes", (0.012, 0.1, 0.012), Vector((toe.x + offset, toe.y - 0.07, 0.012)), CLAW)
        b.boxOn(side + "Toes", (0.011, 0.035, 0.011), Vector((toe.x + offset, toe.y - 0.13, 0.004)), CLAW, rot=rotX(30))
    # 무릎 옆 혹과 정강이 금: 왼쪽 정강이는 이음선이 터져 벌어진 틈
    sign = 1 if side == "Left" else -1
    b.boxOn(side + "LowerLeg", (0.03, 0.035, 0.04), b.head(side + "LowerLeg") + Vector((sign * 0.04, 0.0, -0.02)), JOINT)
    if side == "Left":
        b.segment("LeftLowerLeg", 0.043, 0.041, CRACK, start=0.4, end=0.45, segments=ROUND)
    else:
        b.boxOn("RightUpperLeg", (0.008, 0.008, 0.14), b.along("RightUpperLeg", 0.5) + Vector((-0.02, -0.055, 0)), CRACK, rot=rotY(20))
        b.boxOn("RightUpperLeg", (0.05, 0.006, 0.07), b.along("RightUpperLeg", 0.3) + Vector((0.01, -0.058, 0)), BRUISE)


def makeGhoul():
    """뼈대(사람 비율을 키 2.4m로 키우고 팔을 늘림)와 구울 몸을 만든다."""
    bones = humanoidBones(scale=HEIGHT_SCALE, overrides=ARM_OVERRIDES)
    armature = createArmature("Armature", bones)
    b = CharacterBuilder("Ghoul", bones)
    makeTorso(b)
    makeHead(b)
    for side in ("Left", "Right"):
        makeArm(b, side)
        makeLeg(b, side)
    mesh = b.buildCharacter(armature)
    mesh.name = "SK_Ghoul"
    return armature, mesh


# =============================================================== 자세

# 뼈 회전은 뼈 기준 오일러(도). 뼈의 Y축이 뼈 방향이다.
# - 몸통·목·머리(위로 향한 뼈): X+ = 앞으로 숙임, Z = 옆으로 기울임
# - 다리(아래로 향한 뼈): X- = 다리를 앞으로, 무릎 X+ = 정강이를 뒤로
# - 팔(옆으로 향한 뼈): X- = 팔을 내림, 왼팔 Z- / 오른팔 Z+ = 팔을 앞으로


def arm(pose, side, x, z, elbow=0.0, hand=0.0):
    """팔 자세. 앞으로(z)·굽힘(elbow, hand)은 왼팔 기준 값이며 오른팔은 부호를 바꿔 넣는다."""
    s = 1 if side == "Left" else -1
    pose[side + "UpperArm"] = (x, 0, s * z)
    pose[side + "LowerArm"] = (0, 0, s * elbow)
    pose[side + "Hand"] = (0, 0, s * hand)


def leg(pose, side, thigh, knee, foot=0.0):
    """다리 자세. thigh는 음수일수록 앞으로, knee는 양수일수록 무릎을 굽힌다."""
    pose[side + "UpperLeg"] = (thigh, 0, 0)
    pose[side + "LowerLeg"] = (knee, 0, 0)
    pose[side + "Foot"] = (foot, 0, 0)


def hunch(spine=25, chest=15, upper=10, neck=-18, head=-12, headTilt=12, shoulderUp=12):
    """기본 구부정한 자세: 숙인 몸통, 앞을 노려보는 머리, 축 늘어진 긴 팔, 굽힌 다리, 올라간 왼어깨."""
    pose = {
        "Spine": (spine, 0, 0), "Chest": (chest, 0, 0), "UpperChest": (upper, 0, 0),
        "Neck": (neck, 0, 0), "Head": (head, 0, headTilt),
        "LeftShoulder": (shoulderUp, 0, 0),
    }
    arm(pose, "Left", -70, -45, elbow=-25, hand=-10)
    arm(pose, "Right", -75, -40, elbow=-20, hand=-15)
    leg(pose, "Left", -20, 50, -28)
    leg(pose, "Right", -18, 46, -26)
    return pose


def merged(base, **changes):
    """자세 사전을 복사해 일부 뼈만 바꾼다."""
    pose = dict(base)
    pose.update(changes)
    return pose


class Animator:
    """한 액션에 키를 찍는 도우미. 모든 뼈를 매 키마다 찍어 이전 값이 남지 않게 한다."""

    def __init__(self, armature, name):
        self.armature = armature
        self.action = bpy.data.actions.new(name)
        self.action.use_fake_user = True
        if armature.animation_data is None:
            armature.animation_data_create()
        armature.animation_data.action = self.action
        for pose_bone in armature.pose.bones:
            pose_bone.rotation_mode = "XYZ"

    def key(self, frame, pose, hipsDrop=0.0, hipsForward=0.0, interpolation="BEZIER"):
        """frame에 자세를 찍는다. hipsDrop은 골반을 내리는 거리(m), hipsForward는 앞으로 미는 거리(m)."""
        bpy.context.preferences.edit.keyframe_new_interpolation_type = interpolation
        for pose_bone in self.armature.pose.bones:
            degrees = pose.get(pose_bone.name, (0, 0, 0))
            pose_bone.rotation_euler = tuple(math.radians(d) for d in degrees)
            pose_bone.keyframe_insert("rotation_euler", frame=frame, group=pose_bone.name)
        hips = self.armature.pose.bones["Hips"]
        # 골반 뼈는 위를 향하므로 뼈 기준 Y = 위, Z = 앞(-Y 월드).
        hips.location = (0.0, -hipsDrop, hipsForward)
        hips.keyframe_insert("location", frame=frame, group="Hips")


def animateIdle(armature):
    """구부정하게 숨 쉬다가 30프레임쯤 머리를 홱 꺾었다 돌아온다. (2초 반복)"""
    a = Animator(armature, "Ghoul_Idle")
    base = hunch()
    breathe = merged(base, UpperChest=(6, 0, 0), Neck=(-22, 0, 0))
    a.key(0, base, hipsDrop=0.16)
    a.key(14, breathe, hipsDrop=0.14)
    a.key(28, base, hipsDrop=0.16)
    # 머리 홱 꺾기 (보간 없음)
    a.key(30, merged(base, Head=(-20, 0, 45)), hipsDrop=0.16, interpolation="CONSTANT")
    a.key(34, merged(base, Head=(-8, 0, 30)), hipsDrop=0.16, interpolation="CONSTANT")
    a.key(37, base, hipsDrop=0.16)
    a.key(48, base, hipsDrop=0.16)


def stalkPose(phase):
    """다가가는 걸음 한 순간. phase 0 = 왼발 앞·오른팔 앞, 1 = 왼발 지나감, 2 = 오른발 앞, 3 = 오른발 지나감."""
    pose = hunch(spine=35, chest=15, upper=8, neck=-28, head=-12, headTilt=8)
    if phase in (0, 2):
        front, back = ("Left", "Right") if phase == 0 else ("Right", "Left")
        leg(pose, front, -38, 62, -20)
        leg(pose, back, 14, 34, -40)
        arm(pose, back, -50, -80, elbow=-15, hand=-20)   # 다리 반대쪽 팔이 앞으로 뻗어 바닥을 짚음
        arm(pose, front, -80, -15, elbow=-30, hand=-5)
        twist = 8 if phase == 0 else -8
        pose["Spine"] = (35, twist, 0)
    else:
        lifted, planted = ("Right", "Left") if phase == 1 else ("Left", "Right")
        leg(pose, lifted, -15, 85, -10)
        leg(pose, planted, -12, 40, -28)
        arm(pose, "Left", -70, -45, elbow=-25, hand=-10)
        arm(pose, "Right", -70, -45, elbow=-25, hand=-10)
    return pose


def animateStalk(armature):
    """긴 팔로 바닥을 짚으며 다가가는 걸음. (32프레임 = 1.33초 반복, 한 주기에 두 걸음)"""
    a = Animator(armature, "Ghoul_Stalk")
    for frame, phase, drop in ((0, 0, 0.24), (8, 1, 0.18), (16, 2, 0.24), (24, 3, 0.18)):
        a.key(frame, stalkPose(phase), hipsDrop=drop)
    # 20프레임에 머리를 홱 돌렸다가 돌아온다 (보간 없음)
    a.key(20, merged(stalkPose(2), Head=(-15, 0, -40)), hipsDrop=0.22, interpolation="CONSTANT")
    a.key(22, stalkPose(2), hipsDrop=0.21)
    a.key(32, stalkPose(0), hipsDrop=0.24)


def crawlPose(phase):
    """네 발로 기는 한 순간. phase 0 = 앞발(팔) 앞으로·뒷발 뒤로, 1 = 모음, 2 = 반대, 3 = 모음."""
    pose = {
        "Spine": (55, 0, 0), "Chest": (20, 0, 0), "UpperChest": (15, 0, 0),
        "Neck": (-45, 0, 0), "Head": (-35, 0, 0), "LeftShoulder": (8, 0, 0),
    }
    reach = {0: 28, 1: 0, 2: -28, 3: 0}[phase]
    # 몸통이 수평이면 축의 뜻이 바뀐다: 팔의 Z(원래 "앞으로")는 바닥 쪽, X(원래 "올리기")는 머리 쪽(앞)·엉덩이 쪽(뒤)이 된다.
    # 앞발(팔)은 바닥을 향하게(Z ∓CRAWL_ARM_DOWN) 두고 X로 앞뒤로 딛으며, 팔꿈치를 굽혀 길이를 줄이고 손바닥은 바닥에 붙인다.
    crawlArm(pose, "Left", CRAWL_ARM_FORWARD + reach)
    crawlArm(pose, "Right", CRAWL_ARM_FORWARD - reach)
    leg(pose, "Left", -70 + reach, 100 - reach * 0.5, -30)
    leg(pose, "Right", -70 - reach, 100 + reach * 0.5, -30)
    return pose


# 기어가기 앞발(팔) 자세 값(도). 확인 미리보기: preview_ghoul_crawl_*_side.png
CRAWL_ARM_DOWN = 80      # 팔을 바닥 쪽으로 내리는 정도
CRAWL_ARM_FORWARD = 20   # 앞으로 딛는 기본 각도 (+ 걸음에 따라 ±28)
CRAWL_ELBOW = 45         # 팔꿈치 굽힘 (팔이 장대처럼 곧게 바닥을 찌르지 않게)
CRAWL_WRIST = 40         # 손목을 펴서 손바닥·손톱을 앞으로


def crawlArm(pose, side, forward, down=None, elbow=None):
    """기어갈 때의 앞발(팔) 자세. forward는 머리 쪽으로 딛는 각도(도), down·elbow를 비우면 기어가기 값을 쓴다."""
    s = 1 if side == "Left" else -1
    pose[side + "UpperArm"] = (forward, 0, -s * (CRAWL_ARM_DOWN if down is None else down))
    pose[side + "LowerArm"] = (-(CRAWL_ELBOW if elbow is None else elbow), 0, 0)
    pose[side + "Hand"] = (CRAWL_WRIST, 0, 0)


def animateCrawl(armature):
    """네 발로 빠르게 기어 달리기. (16프레임 = 0.67초 반복)"""
    a = Animator(armature, "Ghoul_Crawl")
    for frame, phase, drop in ((0, 0, 0.62), (4, 1, 0.55), (8, 2, 0.62), (12, 3, 0.55), (16, 0, 0.62)):
        a.key(frame, crawlPose(phase), hipsDrop=drop, hipsForward=0.05)


def animateAttack(armature):
    """두 팔을 치켜들었다가(7) 휘둘러 맞히고(ATTACK_HIT_FRAME) 따라 휘두른 뒤(14) 대기 자세로 돌아온다. (26프레임)"""
    a = Animator(armature, "Ghoul_Attack")
    base = hunch()
    windup = merged(base, Spine=(12, 0, 0), Chest=(0, 0, 0), UpperChest=(-12, 0, 0), Neck=(-10, 0, 0), Head=(-25, 0, 0))
    arm(windup, "Left", 35, 15, elbow=-70, hand=-20)
    arm(windup, "Right", 35, 15, elbow=-70, hand=-20)
    strike = merged(base, Spine=(42, 0, 0), Chest=(18, 0, 0), UpperChest=(10, 0, 0), Neck=(-30, 0, 0), Head=(-15, 0, 0))
    arm(strike, "Left", -35, -75, elbow=-10, hand=-5)
    arm(strike, "Right", -35, -75, elbow=-10, hand=-5)
    follow = merged(strike, Spine=(48, 0, 0))
    arm(follow, "Left", -60, -95, elbow=-20, hand=-10)
    arm(follow, "Right", -60, -95, elbow=-20, hand=-10)

    a.key(0, base, hipsDrop=0.16)
    a.key(7, windup, hipsDrop=0.05)
    a.key(9, windup, hipsDrop=0.05, interpolation="LINEAR")
    a.key(ATTACK_HIT_FRAME, strike, hipsDrop=0.2, hipsForward=0.12, interpolation="LINEAR")
    a.key(14, follow, hipsDrop=0.22, hipsForward=0.12)
    a.key(26, base, hipsDrop=0.16)


def animateShriek(armature):
    """머리를 젖히고 팔을 벌려 떨며 비명. (40프레임, 떨림은 보간 없이 2프레임마다)"""
    a = Animator(armature, "Ghoul_Shriek")
    base = hunch()
    scream = merged(base, Spine=(5, 0, 0), Chest=(-10, 0, 0), UpperChest=(-15, 0, 0), Neck=(-30, 0, 0), Head=(-35, 0, 0))
    arm(scream, "Left", 10, 35, elbow=-25, hand=-30)
    arm(scream, "Right", 10, 35, elbow=-25, hand=-30)
    a.key(0, base, hipsDrop=0.16)
    a.key(6, scream, hipsDrop=0.04)
    for i, frame in enumerate(range(8, 34, 2)):
        jitter = 5 if i % 2 == 0 else -5
        a.key(frame, merged(scream, Head=(-35, 0, jitter), UpperChest=(-15 + jitter * 0.4, 0, 0)),
              hipsDrop=0.04, interpolation="CONSTANT")
    a.key(34, scream, hipsDrop=0.04)
    a.key(40, base, hipsDrop=0.16)


def animateTwitch(armature):
    """덧입히는 경련(6초 반복). 첫 프레임이 기준(모두 0)이고, 40·100프레임 근처에 짧게 홱 떨린다."""
    a = Animator(armature, "Ghoul_Twitch")
    rest = {}
    a.key(0, rest)
    a.key(39, rest)
    a.key(40, {"Head": (10, 0, 35), "Neck": (15, 0, 0)}, interpolation="CONSTANT")
    a.key(42, {"Head": (0, 0, 20), "LeftShoulder": (20, 0, 0)}, interpolation="CONSTANT")
    a.key(45, rest)
    a.key(99, rest)
    a.key(100, {"Head": (-10, 0, -30), "RightLowerArm": (0, 0, 30)}, interpolation="CONSTANT")
    a.key(103, rest)
    a.key(144, rest)


# 은신 자세 골반 높이(m). 확인 미리보기: preview_ghoul_crouch_*_side.png
CROUCH_HIPS_DROP = 0.82


def crouchPose(breath=0.0):
    """
    은신 자세(도약 준비): 네 발 포식자처럼 몸통을 거의 수평으로 눕혀 바닥 가까이 낮추고, 머리는 어깨보다 낮게 앞으로 내민다.
    앞발(팔)은 팔꿈치를 바깥으로 굽혀 어깨를 낮춘 채 짚고, 뒷다리는 몸 밑으로 깊게 접어 언제든 튀어 나갈 힘을 모은다.
    breath(0~1)만큼 등이 부풀었다 가라앉는다.
    """
    pose = {
        "Spine": (58, 0, 0), "Chest": (20 - breath * 4, 0, 0), "UpperChest": (16 - breath * 3, 0, 0),
        "Neck": (-48 - breath * 2, 0, 0), "Head": (-28, 0, 4), "LeftShoulder": (10, 0, 0),
    }
    crawlArm(pose, "Left", CROUCH_ARM_FORWARD + 4, down=CROUCH_ARM_DOWN, elbow=CROUCH_ELBOW)
    crawlArm(pose, "Right", CROUCH_ARM_FORWARD - 4, down=CROUCH_ARM_DOWN, elbow=CROUCH_ELBOW)
    leg(pose, "Left", -92, 140, -40)
    leg(pose, "Right", -84, 134, -42)
    return pose


# 은신 앞발(팔) 자세 값(도)
CROUCH_ARM_FORWARD = 55
CROUCH_ARM_DOWN = 40
CROUCH_ELBOW = 55


def animateCrouch(armature):
    """은신 자세를 유지하며 낮게 숨 쉬고, 가끔 머리를 홱 꺾는다. (48프레임 = 2초 반복)"""
    a = Animator(armature, "Ghoul_Crouch")
    a.key(0, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)
    a.key(20, crouchPose(1.0), hipsDrop=CROUCH_HIPS_DROP - 0.02)
    a.key(32, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)
    # 숨죽이고 있다가 소리 나는 쪽으로 머리만 홱 돌림 (보간 없음)
    a.key(36, merged(crouchPose(), Head=(-32, 0, 38)), hipsDrop=CROUCH_HIPS_DROP, interpolation="CONSTANT")
    a.key(40, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)
    a.key(48, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)


def animateCrouchDown(armature):
    """선 자세(대기)에서 은신 자세로 빠르게 웅크린다. 끝 프레임이 Ghoul_Crouch 첫 프레임과 같다. (8프레임)"""
    a = Animator(armature, "Ghoul_CrouchDown")
    a.key(0, hunch(), hipsDrop=0.16)
    # 무릎이 먼저 꺾이고 몸통·팔이 뒤따라 떨어진다
    halfway = merged(hunch(spine=34, chest=18, upper=12, neck=-30, head=-22))
    leg(halfway, "Left", -70, 105, -36)
    leg(halfway, "Right", -64, 98, -36)
    crawlArm(halfway, "Left", 10)
    crawlArm(halfway, "Right", 4)
    a.key(3, halfway, hipsDrop=0.52)
    a.key(8, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)


def animateCrouchUp(armature):
    """은신 자세에서 몸을 펴며 선 자세(대기)로 일어난다. 끝 프레임이 Ghoul_Idle 첫 프레임과 같다. (12프레임)"""
    a = Animator(armature, "Ghoul_CrouchUp")
    a.key(0, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)
    # 손으로 바닥을 밀어 상체부터 들고, 다리가 펴지며 살짝 솟았다 가라앉는다
    push = merged(crouchPose(), Spine=(30, 0, 0), Chest=(14, 0, 0), Neck=(-30, 0, 0))
    crawlArm(push, "Left", 22, down=70, elbow=55)
    crawlArm(push, "Right", 16, down=70, elbow=55)
    a.key(4, push, hipsDrop=0.68)
    rise = hunch(spine=20, chest=12, upper=8)
    a.key(9, rise, hipsDrop=0.1)
    a.key(12, hunch(), hipsDrop=0.16)


# 도약 공격에서 손톱이 상대를 내리찍는 프레임. (Unity의 lungeHitDelay = 전환 시간 + 이 프레임 / FPS)
LUNGE_HIT_FRAME = 11


def animateLunge(armature):
    """
    은신 자세에서 뛰어올라 위에서 덮치는 런지. (32프레임)
    0 도약 준비 → 1 뒷다리에 힘을 싣고 어깨·머리를 더 낮춤 → 3 뒷다리를 차며 몸을 앞으로 쭉 폄
    → 6 정점: 몸통을 편 채 고개를 들고 두 팔을 머리 앞 위로 뻗음 → 9 내려오며 팔을 앞 아래로 내뻗고 다리를 앞으로 끌어옴
    → LUNGE_HIT_FRAME 착지하며 앞발 손톱으로 내리찍음 → 17 몸을 접어 짓누름 → 32 대기 자세.
    실제 이동(뛰어올랐다 떨어지는 약 0.45초 = 11프레임)은 게임 코드가 하므로 골반은 앞·위로 밀지 않는다.
    """
    a = Animator(armature, "Ghoul_Lunge")
    a.key(0, crouchPose(), hipsDrop=CROUCH_HIPS_DROP)

    coil = merged(crouchPose(), Spine=(62, 0, 0), Neck=(-54, 0, 0), Head=(-30, 0, 0))
    crawlArm(coil, "Left", CROUCH_ARM_FORWARD, down=CROUCH_ARM_DOWN - 4, elbow=CROUCH_ELBOW + 6)
    crawlArm(coil, "Right", CROUCH_ARM_FORWARD - 6, down=CROUCH_ARM_DOWN - 4, elbow=CROUCH_ELBOW + 6)
    leg(coil, "Left", -100, 148, -44)
    leg(coil, "Right", -92, 142, -46)
    a.key(1, coil, hipsDrop=CROUCH_HIPS_DROP + 0.03, interpolation="LINEAR")

    # 뒷다리를 차는 순간: 다리가 뒤로 펴지고 팔은 앞으로 내뻗기 시작한다
    push = {
        "Spine": (80, 0, 0), "Chest": (10, 0, 0), "UpperChest": (4, 0, 0),
        "Neck": (-55, 0, 0), "Head": (-30, 0, 0), "LeftShoulder": (12, 0, 0),
    }
    crawlArm(push, "Left", 62, down=40, elbow=20)
    crawlArm(push, "Right", 58, down=40, elbow=20)
    leg(push, "Left", 50, 35, 30)
    leg(push, "Right", 38, 55, 25)
    a.key(3, push, hipsDrop=LUNGE_FLIGHT_DROP + 0.08, interpolation="LINEAR")

    # 정점: 몸을 쭉 편 채 고개를 들고, 두 팔을 머리 앞 위로 치켜 뻗어 위에서 덮칠 준비를 한다
    peak = {
        "Spine": (78, 0, 0), "Chest": (4, 0, 0), "UpperChest": (-4, 0, 0),
        "Neck": (-50, 0, 0), "Head": (-30, 0, 0), "LeftShoulder": (16, 0, 0),
    }
    crawlArm(peak, "Left", 96, down=4, elbow=10)
    crawlArm(peak, "Right", 92, down=4, elbow=10)
    leg(peak, "Left", 55, 25, 40)
    leg(peak, "Right", 45, 40, 35)
    a.key(6, peak, hipsDrop=LUNGE_FLIGHT_DROP)

    # 내려오는 중: 몸통이 앞으로 기울고, 팔을 앞 아래로 내뻗어 손톱을 세우며, 뒷다리를 앞으로 끌어온다
    descend = {
        "Spine": (92, 0, 0), "Chest": (12, 0, 0), "UpperChest": (6, 0, 0),
        "Neck": (-60, 0, 0), "Head": (-32, 0, 0), "LeftShoulder": (12, 0, 0),
    }
    crawlArm(descend, "Left", 68, down=36, elbow=8)
    crawlArm(descend, "Right", 64, down=36, elbow=8)
    leg(descend, "Left", 30, 80, 20)
    leg(descend, "Right", 20, 90, 15)
    a.key(9, descend, hipsDrop=LUNGE_FLIGHT_DROP, interpolation="LINEAR")

    # 착지하며 내리찍기: 앞발이 앞 바닥을 찍고, 뒷다리는 몸 밑으로 끌려온다
    strike = {
        "Spine": (64, 0, 0), "Chest": (22, 0, 0), "UpperChest": (14, 0, 0),
        "Neck": (-56, 0, 0), "Head": (-34, 0, 0), "LeftShoulder": (10, 0, 0),
    }
    crawlArm(strike, "Left", 58, down=62, elbow=22)
    crawlArm(strike, "Right", 54, down=62, elbow=22)
    leg(strike, "Left", -60, 120, -30)
    leg(strike, "Right", -45, 110, -25)
    a.key(LUNGE_HIT_FRAME, strike, hipsDrop=0.48)

    pin = merged(crouchPose(), Spine=(62, 0, 0), Neck=(-54, 0, 0), Head=(-22, 0, 10))
    crawlArm(pin, "Left", 62, down=40, elbow=45)
    crawlArm(pin, "Right", 58, down=40, elbow=45)
    a.key(17, pin, hipsDrop=CROUCH_HIPS_DROP - 0.04)
    a.key(32, hunch(), hipsDrop=0.16)


# 런지 비행 중 골반 높이(m). 낮고 길게 날아가도록 서 있을 때보다 낮다.
LUNGE_FLIGHT_DROP = 0.45


def peekPose():
    """
    로비 편의점 유리 밖에서 안을 들여다보는 정지 자세: 상체를 앞으로 기울여 얼굴을 유리에 바짝 대고,
    두 팔을 얼굴 양옆 높이로 들어 손바닥과 손톱을 유리에 붙인다. 머리는 한쪽으로 꺾어 섬뜩하게 한다.
    """
    pose = hunch(spine=PEEK_SPINE, chest=8, upper=2, neck=-24, head=-14, headTilt=PEEK_HEAD_TILT, shoulderUp=16)
    # 위팔은 옆으로 벌리고 아래팔은 위로 세워(LowerArm X) 손이 얼굴 양옆 위에서 유리를 짚게 한다. 오른팔은 조금 낮춰 비대칭으로.
    for side, s, lower in (("Left", 1, 0), ("Right", -1, 5)):
        pose[side + "UpperArm"] = (PEEK_ARM_DOWN - lower, 0, s * PEEK_ARM_FORWARD)
        pose[side + "LowerArm"] = (PEEK_FOREARM_UP - lower, 0, 0)
        pose[side + "Hand"] = (PEEK_WRIST, 0, 0)
    leg(pose, "Left", -14, 24, -10)
    leg(pose, "Right", -6, 18, -12)
    return pose


# 들여다보기 자세 값(도). 확인 미리보기: preview_ghoul_peek_0*.png
PEEK_SPINE = 34          # 상체를 유리 쪽으로 기울임
PEEK_HEAD_TILT = 24      # 한쪽으로 꺾은 머리
PEEK_ARM_DOWN = -25      # 벌린 위팔을 어깨보다 조금 내림
PEEK_ARM_FORWARD = -15   # 위팔을 앞으로 내미는 정도(작을수록 옆으로 벌림)
PEEK_FOREARM_UP = 95     # 아래팔을 위로 세움
PEEK_WRIST = 40          # 손목을 젖혀 손바닥을 유리에 댐


def animatePeek(armature):
    """들여다보기 정지 자세. 움직임 없이 같은 자세를 두 번 찍어 1초짜리 반복 클립으로 만든다."""
    a = Animator(armature, "Ghoul_Peek")
    a.key(0, peekPose(), hipsDrop=0.08, hipsForward=0.04)
    a.key(24, peekPose(), hipsDrop=0.08, hipsForward=0.04)


def starePose():
    """
    로비에서 진열대 뒤로 머리만 내밀거나 카메라 바로 앞에 나타날 때의 정지 자세:
    목을 앞으로 길게 빼고 머리를 크게 꺾어 노려보며, 긴 팔은 기본 구부정 자세처럼 축 늘어뜨린다.
    """
    return hunch(spine=28, chest=14, upper=10, neck=STARE_NECK, head=-10, headTilt=STARE_HEAD_TILT, shoulderUp=18)


# 노려보기 자세 값(도). 확인 미리보기: preview_ghoul_stare_0*.png
STARE_NECK = -34       # 목을 앞으로 빼는 정도
STARE_HEAD_TILT = 40   # 머리를 옆으로 꺾는 정도


def animateStare(armature):
    """노려보기 정지 자세. 같은 자세를 두 번 찍어 1초짜리 반복 클립으로 만든다."""
    a = Animator(armature, "Ghoul_Stare")
    a.key(0, starePose(), hipsDrop=0.14)
    a.key(24, starePose(), hipsDrop=0.14)


ANIMATIONS = [animateIdle, animateStalk, animateCrawl, animateAttack, animateShriek, animateTwitch,
              animateCrouch, animateCrouchDown, animateCrouchUp, animateLunge, animatePeek, animateStare]

# 미리보기로 찍을 (액션, 프레임)
PREVIEW_SHOTS = [("Ghoul_Idle", 0, 35), ("Ghoul_Stalk", 0, 35), ("Ghoul_Crawl", 0, 35), ("Ghoul_Crawl", 0, 90),
                 ("Ghoul_Crawl", 4, 90), ("Ghoul_Crawl", 8, 90), ("Ghoul_Attack", 7, 35),
                 ("Ghoul_Attack", ATTACK_HIT_FRAME, 35), ("Ghoul_Shriek", 10, 35), ("Ghoul_Twitch", 40, 35),
                 ("Ghoul_Crouch", 0, 35), ("Ghoul_Crouch", 0, 90), ("Ghoul_CrouchDown", 3, 90), ("Ghoul_CrouchUp", 4, 90),
                 ("Ghoul_Lunge", 3, 90), ("Ghoul_Lunge", 6, 90), ("Ghoul_Lunge", 9, 90), ("Ghoul_Lunge", LUNGE_HIT_FRAME, 90),
                 ("Ghoul_Lunge", 17, 90), ("Ghoul_Lunge", 6, 35), ("Ghoul_Peek", 0, 0), ("Ghoul_Peek", 0, 90),
                 ("Ghoul_Stare", 0, 0), ("Ghoul_Stare", 0, 90)]


# =============================================================== 실행

def verifyExport(path):
    """내보낸 FBX를 빈 씬에 다시 불러와 들어 있는 액션 이름과 길이를 출력한다."""
    resetScene()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bpy.ops.import_scene.fbx(filepath=path)
    for action in bpy.data.actions:
        start, end = action.frame_range
        print(f"[Ghoul] FBX 액션 {action.name}: {start:.0f}~{end:.0f} ({(end - start) / FPS:.2f}초)")


def main():
    """구울 모델과 애니메이션을 만들어 FBX로 내보내고, 미리보기와 .blend를 저장한 뒤 FBX 내용을 확인한다."""
    resetScene()
    bpy.context.scene.render.fps = FPS
    armature, mesh = makeGhoul()
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    size = mesh.dimensions
    print(f"[Ghoul] {tris} tris, {size.x:.2f}x{size.y:.2f}x{size.z:.2f}m, 뼈 {len(armature.data.bones)}개")

    for animate in ANIMATIONS:
        animate(armature)
    bpy.context.preferences.edit.keyframe_new_interpolation_type = "BEZIER"

    if previewDir:
        folder = os.path.abspath(previewDir)
        for actionName, frame, azimuth in PREVIEW_SHOTS:
            armature.animation_data.action = bpy.data.actions[actionName]
            bpy.context.scene.frame_set(frame)
            suffix = "_side" if azimuth == 90 else ""
            renderCharacterPreview([mesh], os.path.join(folder, f"preview_ghoul_{actionName[6:].lower()}_{frame}{suffix}.png"),
                                   elevation=10, azimuth=azimuth)

    armature.animation_data.action = None
    bpy.context.scene.frame_set(0)
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_euler = (0, 0, 0)
        pose_bone.location = (0, 0, 0)
    if previewDir:
        renderCharacterPreview([mesh], os.path.join(os.path.abspath(previewDir), "preview_ghoul_rest.png"), elevation=5, azimuth=0)
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    path = exportCharacterFbx(armature, mesh, outDir, "SK_Ghoul", withAnimation=True)
    print("[Ghoul] ->", path)
    verifyExport(path)


main()
