"""
Humanoid 확인용 빈 몸(SK_TestBody) 생성 스크립트. (Phase 11-1)
부위마다 단순 도형을 뼈 하나에 붙인 사람 모양이다. Unity에서 Humanoid 아바타가 만들어지고
Kevin 걷기·달리기와 Mixamo 감정 표현이 제대로 붙는지 먼저 확인한 뒤, 같은 뼈대로 마네킹·가드를 만든다.

실행:
  blender -b --python generate_test_body.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

미리보기는 T자(정면·옆)와 확인 자세(팔 내림·무릎 굽힘) 두 장이다. 확인 자세에서 도형이 관절을 따라 움직이면 가중치가 맞다.
"""
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import configure, resetScene  # noqa: E402
from character_common import (CHECK_POSE, CharacterBuilder, createArmature, exportCharacterFbx,  # noqa: E402
                              humanoidBones, renderCharacterPreview, setPose, validateHumanoid)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "Body":  ((0.80, 0.80, 0.78), 0.0, 0.8, 0.0),
    "Joint": ((0.45, 0.46, 0.48), 0.0, 0.7, 0.0),
    "Mark":  ((0.75, 0.25, 0.20), 0.0, 0.7, 0.0),
}
configure(palette, "M_Char_")


def makeTestBody():
    """뼈대와 단순 도형 몸을 만든다. 앞쪽 표시(가슴의 빨간 상자)로 정면 방향을 확인한다."""
    bones = humanoidBones()
    armature = createArmature("Armature", bones)
    b = CharacterBuilder("TestBody", bones)

    # 몸통
    b.boxOn("Hips", (0.30, 0.18, 0.16), b.along("Hips", 0.3), "Body")
    b.boxOn("Spine", (0.27, 0.16, 0.15), b.along("Spine", 0.5), "Body")
    b.boxOn("Chest", (0.32, 0.19, 0.16), b.along("Chest", 0.5), "Body")
    b.boxOn("UpperChest", (0.36, 0.20, 0.14), b.along("UpperChest", 0.5), "Body")
    b.boxOn("UpperChest", (0.08, 0.02, 0.06), b.along("UpperChest", 0.4) + Vector((0, -0.11, 0)), "Mark")
    b.segment("Neck", 0.05, 0.05, "Joint", segments=6)
    b.sphereOn("Head", 0.11, b.along("Head", 0.5), "Body", segments=8, scale=(0.9, 1.0, 1.15))

    for side in ("Left", "Right"):
        b.ball(side + "Shoulder", 0.05, "Joint", at=1.0)
        b.boxOn(side + "Shoulder", (0.10, 0.12, 0.10), b.along(side + "Shoulder", 0.6), "Body")
        b.segment(side + "UpperArm", 0.05, 0.042, "Body", start=0.08, end=0.92)
        b.ball(side + "LowerArm", 0.043, "Joint")
        b.segment(side + "LowerArm", 0.04, 0.032, "Body", start=0.08, end=0.95)
        b.ball(side + "Hand", 0.032, "Joint")
        b.boxOn(side + "Hand", (0.13, 0.04, 0.08), b.along(side + "Hand", 0.5), "Body")
        b.ball(side + "UpperLeg", 0.07, "Joint")
        b.segment(side + "UpperLeg", 0.075, 0.055, "Body", start=0.08, end=0.92)
        b.ball(side + "LowerLeg", 0.055, "Joint")
        b.segment(side + "LowerLeg", 0.05, 0.04, "Body", start=0.08, end=0.92)
        b.ball(side + "Foot", 0.04, "Joint")
        b.boxOn(side + "Foot", (0.09, 0.16, 0.07), b.along(side + "Foot", 0.5), "Body")
        b.boxOn(side + "Toes", (0.09, 0.09, 0.05), b.along(side + "Toes", 0.5), "Body")

    mesh = b.buildCharacter(armature)
    mesh.name = "SK_TestBody"
    return armature, mesh


def main():
    """빈 몸을 만들어 검사하고 FBX로 내보낸다. 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    armature, mesh = makeTestBody()
    problems = validateHumanoid(armature, mesh)
    tris = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
    size = mesh.dimensions
    print(f"[TestBody] {tris} tris, {size.x:.2f}x{size.y:.2f}x{size.z:.2f}m, 뼈 {len(armature.data.bones)}개")
    if problems:
        for problem in problems:
            print("[TestBody] 문제:", problem)
        raise SystemExit(1)
    print("[TestBody] ->", exportCharacterFbx(armature, mesh, outDir, "SK_TestBody"))

    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        folder = os.path.abspath(previewDir)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_testbody_front.png"), elevation=5, azimuth=0)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_testbody_side.png"), elevation=5, azimuth=90)
        setPose(armature, CHECK_POSE)
        renderCharacterPreview([mesh], os.path.join(folder, "preview_testbody_pose.png"), elevation=15, azimuth=25)
        setPose(armature, {})


main()
