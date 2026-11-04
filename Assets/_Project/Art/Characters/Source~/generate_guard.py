"""
로우폴리 시설 경비원(SK_Guard) 생성 스크립트. (Phase 11-3)
남색 제복, 모자, 검은 조끼, 허리띠와 손전등 집, 장갑, 장화. 1인칭 팔다리 메시가 잘 보이도록 팔다리를 마네킹보다 조금 굵게 만든다.

실행:
  blender -b --python generate_guard.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 character_common.py와 같다. (Humanoid 뼈대, 발 원점, 정면 -Y = Unity +Z, 부위마다 뼈 하나에 100% 가중치)
1인칭에서는 팔다리 뼈(위팔·허벅지 아래)에 붙은 삼각형만 보이므로, 소매·장갑·바지·장화는 반드시 팔다리 뼈에 붙인다.
"""
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import configure, resetScene, rotX, rotY  # noqa: E402
from character_common import (CHECK_POSE, CharacterBuilder, createArmature, exportCharacterFbx,  # noqa: E402
                              humanoidBones, renderCharacterPreview, setPose, validateHumanoid)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "GuardUniform": ((0.13, 0.17, 0.29), 0.0, 0.8, 0.0),
    "GuardVest":    ((0.08, 0.09, 0.11), 0.0, 0.75, 0.0),
    "GuardBlack":   ((0.04, 0.04, 0.05), 0.1, 0.6, 0.0),
    "GuardSkin":    ((0.80, 0.62, 0.50), 0.0, 0.7, 0.0),
    "GuardBadge":   ((0.85, 0.68, 0.22), 0.6, 0.35, 0.0),
    "GuardLens":    ((1.00, 0.95, 0.75), 0.0, 0.3, 1.5),
}
configure(palette, "M_Char_")

UNIFORM = "GuardUniform"
VEST = "GuardVest"
BLACK = "GuardBlack"
SKIN = "GuardSkin"
BADGE = "GuardBadge"

ROUND = 10


def makeTorso(b):
    """제복 상의(골반·허리·가슴), 그 위의 검은 조끼, 가슴 배지, 허리띠와 손전등 집."""
    b.segment("Hips", 0.14, 0.15, UNIFORM, start=-1.1, end=0.9, segments=ROUND, flatten=0.72)
    b.segment("Spine", 0.145, 0.15, UNIFORM, start=0.0, end=1.0, segments=ROUND, flatten=0.7)
    b.segment("Chest", 0.155, 0.175, UNIFORM, start=0.0, end=1.0, segments=ROUND, flatten=0.66)
    b.segment("UpperChest", 0.175, 0.13, UNIFORM, start=0.0, end=0.95, segments=ROUND, flatten=0.64)
    # 조끼: 가슴·허리를 조금 더 크게 감싼다.
    b.segment("Spine", 0.155, 0.165, VEST, start=0.3, end=1.02, segments=8, flatten=0.74)
    b.segment("Chest", 0.168, 0.185, VEST, start=0.0, end=1.0, segments=8, flatten=0.7)
    b.segment("UpperChest", 0.185, 0.16, VEST, start=0.0, end=0.55, segments=8, flatten=0.68)
    b.boxOn("Chest", (0.05, 0.012, 0.05), b.along("Chest", 0.75) + Vector((0.08, -0.13, 0)), BADGE)
    # 무전기(왼쪽 가슴)
    b.boxOn("UpperChest", (0.045, 0.035, 0.08), b.along("UpperChest", 0.2) + Vector((-0.08, -0.125, 0)), BLACK)
    # 허리띠와 오른쪽 허리의 손전등 집
    b.segment("Hips", 0.155, 0.158, BLACK, start=0.35, end=0.85, segments=ROUND, flatten=0.74)
    holster = b.along("Hips", 0.2) + Vector((-0.165, -0.02, -0.03))
    b.boxOn("Hips", (0.05, 0.06, 0.13), holster, BLACK)
    b.cylinder(0.022, 0.022, 0.06, holster + Vector((0, 0, 0.09)), BLACK, segments=8)
    b._bind("Hips")
    b.cylinder(0.03, 0.03, 0.02, holster + Vector((0, 0, 0.13)), "GuardLens", segments=8)
    b._bind("Hips")


# 머리 반지름. 목(0.055)에 비해 작아 보이지 않게 키우고, 키(1.8m)가 늘지 않도록 중심을 조금 낮췄다.
HEAD_RADIUS = 0.124


def makeHead(b):
    """목, 얼굴(살색 머리), 모자(챙 + 윗부분). 모자·챙·코는 머리 반지름에 맞춰 크기가 정해진다."""
    r = HEAD_RADIUS
    b.segment("Neck", 0.055, 0.05, SKIN, start=-0.2, end=1.0, segments=8)
    head = b.along("Head", 0.36)
    b.sphereOn("Head", r, head, SKIN, segments=ROUND, scale=(0.9, 0.98, 1.08))
    b.boxOn("Head", (0.035, 0.035, 0.045), head + Vector((0, -r * 0.95, -0.012)), SKIN)  # 코
    capBase = head + Vector((0, 0, r * 0.52))
    b.cylinder(r, r * 0.93, 0.075, capBase + Vector((0, 0, 0.0375)), UNIFORM, segments=ROUND)
    b._bind("Head")
    b.boxOn("Head", (r * 1.6, r * 0.85, 0.012), capBase + Vector((0, -r * 1.05, 0.0)), BLACK, rot=rotX(-8))
    b.boxOn("Head", (0.04, 0.008, 0.034), capBase + Vector((0, -r * 0.97, 0.037)), BADGE)


def makeArm(b, side):
    """제복 소매(어깨 덮개 포함), 팔꿈치 덮개, 검은 장갑."""
    b.sphereOn(side + "UpperArm", 0.07, b.along(side + "UpperArm", 0.05), UNIFORM, segments=ROUND)
    b.segment(side + "UpperArm", 0.058, 0.05, UNIFORM, start=0.05, end=0.95, segments=ROUND)
    b.ball(side + "LowerArm", 0.05, UNIFORM, segments=ROUND)
    b.segment(side + "LowerArm", 0.048, 0.04, UNIFORM, start=0.05, end=0.85, segments=ROUND)
    b.segment(side + "LowerArm", 0.043, 0.043, BLACK, start=0.85, end=1.0, segments=ROUND)
    b.ball(side + "Hand", 0.034, BLACK, segments=8)
    b.boxOn(side + "Hand", (0.13, 0.085, 0.04), b.along(side + "Hand", 0.5), BLACK)
    sign = 1 if side == "Left" else -1
    b.boxOn(side + "Hand", (0.055, 0.028, 0.03), b.along(side + "Hand", 0.15) + Vector((sign * 0.01, -0.05, -0.01)), BLACK)


def makeLeg(b, side):
    """제복 바지(무릎 덮개 포함)와 검은 장화."""
    b.ball(side + "UpperLeg", 0.07, UNIFORM, segments=ROUND)
    b.segment(side + "UpperLeg", 0.085, 0.062, UNIFORM, start=0.0, end=0.95, segments=ROUND)
    b.ball(side + "LowerLeg", 0.06, UNIFORM, segments=ROUND)
    b.segment(side + "LowerLeg", 0.058, 0.052, UNIFORM, start=0.05, end=0.62, segments=ROUND)
    b.segment(side + "LowerLeg", 0.06, 0.055, BLACK, start=0.6, end=1.05, segments=ROUND)
    heel = b.head(side + "Foot")
    toe = b.head(side + "Toes")
    b.boxOn(side + "Foot", (0.1, 0.19, 0.08), Vector((heel.x, (heel.y + toe.y) / 2 + 0.025, 0.04)), BLACK)
    b.boxOn(side + "Toes", (0.095, 0.085, 0.06), Vector((toe.x, toe.y - 0.035, 0.03)), BLACK)


def makeGuard():
    """뼈대와 경비원 몸을 만든다."""
    bones = humanoidBones()
    armature = createArmature("Armature", bones)
    b = CharacterBuilder("Guard", bones)
    makeTorso(b)
    makeHead(b)
    for side in ("Left", "Right"):
        makeArm(b, side)
        makeLeg(b, side)
    mesh = b.buildCharacter(armature)
    mesh.name = "SK_Guard"
    return armature, mesh


def main():
    """경비원을 만들어 검사하고 FBX로 내보낸다. 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    armature, mesh = makeGuard()
    problems = validateHumanoid(armature, mesh)
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    size = mesh.dimensions
    print(f"[Guard] {tris} tris, {size.x:.2f}x{size.y:.2f}x{size.z:.2f}m, 뼈 {len(armature.data.bones)}개")
    if problems:
        for problem in problems:
            print("[Guard] 문제:", problem)
        raise SystemExit(1)
    print("[Guard] ->", exportCharacterFbx(armature, mesh, outDir, "SK_Guard"))

    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        folder = os.path.abspath(previewDir)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_guard_front.png"), elevation=5, azimuth=0)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_guard_side.png"), elevation=5, azimuth=90)
        setPose(armature, CHECK_POSE)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_guard_pose.png"), elevation=15, azimuth=25)
        setPose(armature, {})


main()
