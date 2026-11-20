"""
로우폴리 매장 마네킹(SK_Mannequin) 생성 스크립트. (Phase 11-2)
얼굴 없는 달걀형 머리, 어깨·팔꿈치·손목·골반·무릎·발목·허리의 둥근 관절, 매끈한 몸통, 흰색 무광.
관절이 드러나는 것이 마네킹 정체성이자, 사냥 시간에 뒤틀린 구울로 바뀔 때의 대비가 된다.

실행:
  blender -b --python generate_mannequin.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 character_common.py와 같다. (Humanoid 뼈대, 발 원점, 정면 -Y = Unity +Z, 부위마다 뼈 하나에 100% 가중치)
"""
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import configure, resetScene, rotX  # noqa: E402
from character_common import (CHECK_POSE, CharacterBuilder, createArmature, exportCharacterFbx,  # noqa: E402
                              humanoidBones, renderCharacterPreview, setPose, validateHumanoid)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "MannequinBody":  ((0.86, 0.85, 0.82), 0.0, 0.55, 0.0),
    "MannequinJoint": ((0.66, 0.65, 0.62), 0.0, 0.5, 0.0),
    "MannequinDark":  ((0.22, 0.22, 0.22), 0.4, 0.45, 0.0),
}
configure(palette, "M_Char_")

BODY = "MannequinBody"
JOINT = "MannequinJoint"

# 둥근 부위(머리·관절·몸통)의 분할 수. 로우폴리로 각지되 너무 거칠지 않게.
ROUND = 10


def makeTorso(b):
    """골반·허리·가슴을 앞뒤로 납작한 원기둥으로 잇고, 허리에 관절 고리를 둔다."""
    b.segment("Hips", 0.125, 0.145, BODY, start=-1.1, end=0.85, segments=ROUND, flatten=0.7)
    b.segment("Spine", 0.125, 0.135, BODY, start=0.25, end=1.0, segments=ROUND, flatten=0.66)
    b.segment("Spine", 0.115, 0.115, JOINT, start=-0.05, end=0.3, segments=ROUND, flatten=0.7)
    b.segment("Chest", 0.14, 0.17, BODY, start=0.0, end=1.0, segments=ROUND, flatten=0.62)
    b.segment("UpperChest", 0.17, 0.12, BODY, start=0.0, end=0.95, segments=ROUND, flatten=0.6)
    # 조립식 몸통 이음선: 가슴 위아래 경계와 골반 윗단에 얇은 띠
    b.segment("UpperChest", 0.174, 0.173, JOINT, start=-0.03, end=0.04, segments=ROUND, flatten=0.61)
    b.segment("Hips", 0.146, 0.146, JOINT, start=0.78, end=0.86, segments=ROUND, flatten=0.71)
    # 엉덩이 뒤(+Y) 진열대 받침 소켓: 매장 마네킹이 막대에 꽂혀 서 있던 자리
    socket = b.along("Hips", -0.4) + Vector((0, 0.1, 0))
    b.cylinder(0.026, 0.026, 0.016, socket, JOINT, segments=8, rot=rotX(90))
    b._bind("Hips")
    b.cylinder(0.012, 0.012, 0.018, socket + Vector((0, 0.002, 0)), "MannequinDark", segments=6, rot=rotX(90))
    b._bind("Hips")


# 머리 반지름. 목(0.045)에 비해 작아 보이지 않게 키우고, 키(1.8m)가 늘지 않도록 중심을 조금 낮췄다.
HEAD_RADIUS = 0.118


def makeHead(b):
    """목 기둥과 얼굴 없는 달걀형 머리."""
    b.segment("Neck", 0.045, 0.04, JOINT, start=-0.3, end=1.0, segments=8)
    head = b.along("Head", 0.36) + Vector((0, -0.01, 0))
    b.sphereOn("Head", HEAD_RADIUS, head, BODY, segments=ROUND, scale=(0.86, 0.98, 1.18))
    # 목 받침 칼라와 뒤통수 제조 각인 판. 얼굴은 정체성이라 비워 둔다.
    b.segment("Neck", 0.06, 0.05, JOINT, start=-0.35, end=-0.1, segments=ROUND)
    b.boxOn("Head", (0.045, 0.012, 0.028), head + Vector((0, HEAD_RADIUS * 0.94, -0.02)), JOINT, rot=rotX(-12))
    b.boxOn("Head", (0.03, 0.004, 0.006), head + Vector((0, HEAD_RADIUS * 0.94 + 0.007, -0.016)), "MannequinDark", rot=rotX(-12))


def makeArm(b, side):
    """어깨 관절 공, 위팔, 팔꿈치 공, 아래팔, 손목 공, 벙어리장갑 모양 손."""
    b.ball(side + "UpperArm", 0.058, JOINT, segments=ROUND)
    b.segment(side + "UpperArm", 0.048, 0.04, BODY, start=0.12, end=0.88, segments=ROUND)
    b.ball(side + "LowerArm", 0.04, JOINT, segments=ROUND)
    b.segment(side + "LowerArm", 0.038, 0.029, BODY, start=0.1, end=0.92, segments=ROUND)
    b.ball(side + "Hand", 0.028, JOINT, segments=8)
    # T자에서 손바닥은 아래(-Z)를 본다. 손 길이는 뼈 방향, 폭은 앞뒤(Y), 두께는 위아래(Z).
    b.boxOn(side + "Hand", (0.13, 0.075, 0.032), b.along(side + "Hand", 0.5), BODY)
    sign = 1 if side == "Left" else -1
    b.boxOn(side + "Hand", (0.05, 0.025, 0.026), b.along(side + "Hand", 0.15) + Vector((sign * 0.01, -0.045, -0.008)), BODY)
    # 손가락 네 개를 가르는 홈(손등·손바닥 양쪽에 보이게 손보다 살짝 두껍게)
    fingers = b.along(side + "Hand", 0.78)
    for dy in (-0.019, 0.0, 0.019):
        b.boxOn(side + "Hand", (0.055, 0.004, 0.034), fingers + Vector((0, dy, 0)), JOINT)
    # 어깨 소켓 테(몸통 쪽)
    b.segment(side + "Shoulder", 0.064, 0.064, JOINT, start=0.8, end=0.9, segments=ROUND)


def makeLeg(b, side):
    """골반 관절 공, 허벅지, 무릎 공, 정강이, 발목 공, 신발 모양 발."""
    b.ball(side + "UpperLeg", 0.058, JOINT, segments=ROUND)
    b.segment(side + "UpperLeg", 0.075, 0.052, BODY, start=0.1, end=0.9, segments=ROUND)
    b.ball(side + "LowerLeg", 0.052, JOINT, segments=ROUND)
    b.segment(side + "LowerLeg", 0.05, 0.035, BODY, start=0.1, end=0.92, segments=ROUND)
    b.ball(side + "Foot", 0.036, JOINT, segments=8)
    heel = b.head(side + "Foot")
    toe = b.head(side + "Toes")
    b.boxOn(side + "Foot", (0.085, 0.17, 0.06), Vector((heel.x, (heel.y + toe.y) / 2 + 0.02, 0.035)), BODY)
    b.boxOn(side + "Toes", (0.08, 0.08, 0.045), Vector((toe.x, toe.y - 0.035, 0.0225)), BODY)
    # 허벅지 소켓 테와 밑창(발바닥 높이 0 유지)
    b.segment(side + "UpperLeg", 0.079, 0.079, JOINT, start=0.08, end=0.12, segments=ROUND)
    b.boxOn(side + "Foot", (0.089, 0.174, 0.012), Vector((heel.x, (heel.y + toe.y) / 2 + 0.02, 0.006)), JOINT)
    b.boxOn(side + "Toes", (0.084, 0.082, 0.012), Vector((toe.x, toe.y - 0.035, 0.006)), JOINT)


def makeMannequin():
    """뼈대와 마네킹 몸을 만든다."""
    bones = humanoidBones()
    armature = createArmature("Armature", bones)
    b = CharacterBuilder("Mannequin", bones)
    makeTorso(b)
    makeHead(b)
    for side in ("Left", "Right"):
        makeArm(b, side)
        makeLeg(b, side)
    mesh = b.buildCharacter(armature)
    mesh.name = "SK_Mannequin"
    return armature, mesh


def main():
    """마네킹을 만들어 검사하고 FBX로 내보낸다. 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    armature, mesh = makeMannequin()
    problems = validateHumanoid(armature, mesh)
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    size = mesh.dimensions
    print(f"[Mannequin] {tris} tris, {size.x:.2f}x{size.y:.2f}x{size.z:.2f}m, 뼈 {len(armature.data.bones)}개")
    if problems:
        for problem in problems:
            print("[Mannequin] 문제:", problem)
        raise SystemExit(1)
    print("[Mannequin] ->", exportCharacterFbx(armature, mesh, outDir, "SK_Mannequin"))

    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        folder = os.path.abspath(previewDir)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_mannequin_front.png"), elevation=5, azimuth=0)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_mannequin_side.png"), elevation=5, azimuth=90)
        setPose(armature, CHECK_POSE)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_mannequin_pose.png"), elevation=15, azimuth=25)
        setPose(armature, {})


main()
