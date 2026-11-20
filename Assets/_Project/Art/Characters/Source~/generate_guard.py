"""
로우폴리 무장 군인 가드(SK_Guard) 생성 스크립트. (Phase 11-3, 외형은 시설 경비원에서 군인으로 교체)
올리브색 전투복, 얼굴을 덮는 검은 복면, 얼굴 대부분을 가리는 노란 단일 렌즈 고글, 헤드셋과 헬멧(NVG 마운트·레일·전등), 코요테색 방탄 조끼(탄창 주머니·무전기),
전투 허리띠와 허벅지 권총집, 팔꿈치·무릎 보호대, 장갑, 코요테색 전투화.
1인칭 팔다리 메시가 잘 보이도록 팔다리를 마네킹보다 조금 굵게 만든다.

실행:
  blender -b --python generate_guard.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 character_common.py와 같다. (Humanoid 뼈대, 발 원점, 정면 -Y = Unity +Z, 부위마다 뼈 하나에 100% 가중치)
1인칭에서는 팔다리 뼈(위팔·허벅지 아래)에 붙은 삼각형만 보이므로, 소매·장갑·보호대·바지·전투화는 반드시 팔다리 뼈에 붙인다.
캐릭터의 왼쪽은 블렌더 +X, 오른쪽은 -X.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

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
    "SoldierFatigue":  ((0.27, 0.30, 0.20), 0.0, 0.85, 0.0),
    "SoldierFatigueDk": ((0.18, 0.20, 0.14), 0.0, 0.85, 0.0),
    "SoldierGear":     ((0.44, 0.37, 0.26), 0.0, 0.8, 0.0),
    "SoldierGearDk":   ((0.32, 0.27, 0.19), 0.0, 0.8, 0.0),
    "SoldierHelmet":   ((0.23, 0.26, 0.19), 0.1, 0.6, 0.0),
    "SoldierBlack":    ((0.04, 0.04, 0.05), 0.1, 0.65, 0.0),
    "SoldierStrap":    ((0.12, 0.12, 0.11), 0.0, 0.8, 0.0),
    "SoldierVisor":    ((0.86, 0.84, 0.22), 0.3, 0.12, 0.0),
    "SoldierVisorShine": ((0.97, 0.97, 0.80), 0.2, 0.1, 0.0),
    "SoldierMetal":    ((0.55, 0.56, 0.58), 0.8, 0.35, 0.0),
    "GuardLens":       ((1.00, 0.95, 0.75), 0.0, 0.3, 1.5),
}
configure(palette, "M_Char_")

FATIGUE = "SoldierFatigue"
FATIGUE_DARK = "SoldierFatigueDk"
GEAR = "SoldierGear"
GEAR_DARK = "SoldierGearDk"
HELMET = "SoldierHelmet"
BLACK = "SoldierBlack"
STRAP = "SoldierStrap"
VISOR = "SoldierVisor"
METAL = "SoldierMetal"

ROUND = 10


def makeTorso(b):
    """전투복 상의, 방탄 조끼(앞뒤 판·옆 띠·어깨끈), 탄창 주머니, 무전기, 전투 허리띠와 주머니."""
    b.segment("Hips", 0.14, 0.15, FATIGUE, start=-1.1, end=0.9, segments=ROUND, flatten=0.72)
    b.segment("Spine", 0.145, 0.15, FATIGUE, start=0.0, end=1.0, segments=ROUND, flatten=0.7)
    b.segment("Chest", 0.155, 0.175, FATIGUE, start=0.0, end=1.0, segments=ROUND, flatten=0.66)
    b.segment("UpperChest", 0.175, 0.13, FATIGUE, start=0.0, end=0.95, segments=ROUND, flatten=0.64)
    b.segment("UpperChest", 0.08, 0.07, FATIGUE, start=0.85, end=1.15, segments=8, flatten=0.85)

    # 조끼: 옆구리를 감싸는 띠(허리 뼈)와 가슴 앞뒤 판(가슴 뼈)
    b.segment("Spine", 0.16, 0.168, GEAR, start=0.05, end=0.75, segments=ROUND, flatten=0.76)
    plateZ = 1.28
    for side in (-1, 1):
        b.boxOn("Chest", (0.3, 0.045, 0.32), Vector((0, side * 0.122, plateZ)), GEAR)
    for x in (-0.1, 0.1):
        b.boxOn("UpperChest", (0.065, 0.26, 0.022), b.along("UpperChest", 0.8) + Vector((x, 0, 0.035)), GEAR)
    # 앞판: 탄창 주머니 셋(덮개), 가운데 행정 주머니, 이름표, 왼쪽 가슴 무전기와 안테나
    for x in (-0.08, 0.0, 0.08):
        pouch = Vector((x, -0.158, 1.19))
        b.boxOn("Chest", (0.066, 0.04, 0.1), pouch, GEAR_DARK)
        b.boxOn("Chest", (0.07, 0.044, 0.022), pouch + Vector((0, -0.002, 0.045)), GEAR)
        b.boxOn("Chest", (0.03, 0.006, 0.012), pouch + Vector((0, -0.024, 0.035)), BLACK)
    b.boxOn("Chest", (0.1, 0.02, 0.06), Vector((0, -0.153, 1.31)), GEAR_DARK)
    b.boxOn("Chest", (0.09, 0.004, 0.022), Vector((-0.06, -0.147, 1.41)), FATIGUE_DARK)
    radio = Vector((0.095, -0.16, 1.39))
    b.boxOn("Chest", (0.045, 0.03, 0.08), radio, BLACK)
    b.boxOn("Chest", (0.008, 0.008, 0.11), radio + Vector((0.012, 0, 0.095)), BLACK)
    # 뒤판: 수분 주머니
    b.boxOn("Chest", (0.2, 0.035, 0.22), Vector((0, 0.16, 1.27)), GEAR_DARK)

    # 전투 허리띠: 버클, 왼쪽 허리 투척 주머니, 오른쪽 허리 응급 주머니
    b.segment("Hips", 0.158, 0.16, BLACK, start=0.35, end=0.85, segments=ROUND, flatten=0.76)
    b.boxOn("Hips", (0.05, 0.012, 0.035), b.along("Hips", 0.6) + Vector((0, -0.124, 0)), METAL)
    b.boxOn("Hips", (0.06, 0.06, 0.08), b.along("Hips", 0.45) + Vector((0.165, -0.03, -0.01)), GEAR_DARK)
    b.boxOn("Hips", (0.07, 0.05, 0.07), b.along("Hips", 0.45) + Vector((-0.12, 0.09, -0.01)), GEAR_DARK)


# 머리 반지름. 목(0.055)에 비해 작아 보이지 않게 키우고, 키(1.8m)가 늘지 않도록 중심을 조금 낮췄다.
HEAD_RADIUS = 0.124


def makeHead(b):
    """복면 쓴 머리(코·입 돌출 없음), 노란 단일 렌즈 고글(끈·두꺼운 테), 헤드셋 귀덮개, 헬멧(테·돔·NVG 마운트·옆 레일·오른쪽 전등)."""
    r = HEAD_RADIUS
    head = b.along("Head", 0.36)
    b.segment("Neck", 0.058, 0.052, BLACK, start=-0.2, end=1.0, segments=8)
    b.sphereOn("Head", r, head, BLACK, segments=ROUND, scale=(0.9, 0.98, 1.08))

    # 고글: 머리를 두르는 끈 + 얼굴 대부분을 덮는 노란 단일 렌즈와 두꺼운 검은 테.
    # 렌즈는 가운데 판과 양옆으로 꺾인 판 세 장으로 얼굴 곡면을 감싼다(한 장짜리 평판이면 옆에서 떠 보인다).
    # 윗테가 헬멧 테 아래로 드러나도록 눈 높이보다 조금 낮게 둔다
    eyes = head + Vector((0, 0, -0.03))
    b.cylinder(r, r, 0.03, eyes, STRAP, segments=ROUND, rot=Matrix.Diagonal((0.95, 1.0, 1.0, 1.0)))
    b._bind("Head")
    # 옆 판은 가운데 판 끝에서 바로 꺾여 이어지고, 테는 렌즈와 같은 폭이라 판 사이에 검은 줄이 생기지 않는다(바깥 끝만 테를 더 둘렀다).
    # 끈 원기둥의 앞 꼭짓점이 렌즈를 뚫고 비치지 않게 렌즈를 끈보다 앞에 둔다.
    lensHeight, frontY, centerWidth, sideWidth, sideAngle = 0.085, -r * 0.99, 0.08, 0.056, math.radians(40)
    b.boxOn("Head", (centerWidth, 0.02, lensHeight + 0.028), eyes + Vector((0, frontY + 0.01, 0)), BLACK)
    b.boxOn("Head", (centerWidth + 0.004, 0.008, lensHeight), eyes + Vector((0, frontY - 0.004, 0)), VISOR)
    for sx in (-1, 1):
        turn = Matrix.Rotation(sx * sideAngle, 4, "Z")
        along = turn.to_3x3() @ Vector((sx, 0, 0))
        inward = turn.to_3x3() @ Vector((0, 1, 0))
        hinge = eyes + Vector((sx * centerWidth / 2, frontY, 0))
        sideCenter = hinge + along * (sideWidth / 2)
        b.boxOn("Head", (sideWidth + 0.012, 0.02, lensHeight + 0.028), sideCenter + along * 0.006 + inward * 0.01, BLACK, rot=turn)
        b.boxOn("Head", (sideWidth + 0.004, 0.008, lensHeight), sideCenter - inward * 0.004, VISOR, rot=turn)
    b.boxOn("Head", (0.045, 0.004, 0.01), eyes + Vector((-0.015, frontY - 0.009, 0.03)), "SoldierVisorShine")

    # 헤드셋 귀덮개
    for sx in (-1, 1):
        b.cylinder(0.038, 0.038, 0.032, head + Vector((sx * r * 0.93, 0.005, -0.008)), BLACK, segments=8, rot=rotY(90))
        b._bind("Head")

    # 헬멧: 이마 위 테와 둥근 돔(얼굴은 덮지 않고, 키 1.8m를 넘지 않게 납작하게), 앞 NVG 마운트, 옆 레일, 오른쪽(-X) 레일의 전등
    b.cylinder(r * 1.12, r * 1.06, 0.05, head + Vector((0, 0.008, r * 0.45)), HELMET, segments=ROUND)
    b._bind("Head")
    b.sphereOn("Head", r * 1.07, head + Vector((0, 0.008, r * 0.45)), HELMET, segments=ROUND, scale=(1.0, 1.06, 0.66))
    b.boxOn("Head", (0.045, 0.02, 0.035), head + Vector((0, -r * 1.12, r * 0.7)), BLACK)
    for sx in (-1, 1):
        b.boxOn("Head", (0.014, 0.11, 0.022), head + Vector((sx * r * 1.13, 0.008, r * 0.44)), BLACK)
    b.cylinder(0.013, 0.013, 0.05, head + Vector((-r * 1.18, -0.035, r * 0.44)), BLACK, segments=8, rot=rotX(90))
    b._bind("Head")
    b.cylinder(0.011, 0.011, 0.004, head + Vector((-r * 1.18, -0.062, r * 0.44)), "GuardLens", segments=8, rot=rotX(90))
    b._bind("Head")
    b.boxOn("Head", (0.04, 0.03, 0.03), head + Vector((0, r * 1.08, r * 0.35)), BLACK)


def makeArm(b, side):
    """전투복 소매(어깨 주머니·국기 패치), 팔꿈치 보호대, 장갑(손등 보호대·손가락 홈). 모두 팔 뼈에 붙인다."""
    b.sphereOn(side + "UpperArm", 0.07, b.along(side + "UpperArm", 0.05), FATIGUE, segments=ROUND)
    b.segment(side + "UpperArm", 0.058, 0.05, FATIGUE, start=0.05, end=0.95, segments=ROUND)
    b.ball(side + "LowerArm", 0.05, FATIGUE, segments=ROUND)
    b.segment(side + "LowerArm", 0.048, 0.04, FATIGUE, start=0.05, end=0.85, segments=ROUND)
    b.segment(side + "LowerArm", 0.045, 0.045, BLACK, start=0.8, end=1.0, segments=ROUND)
    b.ball(side + "Hand", 0.034, BLACK, segments=8)
    b.boxOn(side + "Hand", (0.13, 0.085, 0.04), b.along(side + "Hand", 0.5), BLACK)
    sign = 1 if side == "Left" else -1
    b.boxOn(side + "Hand", (0.055, 0.028, 0.03), b.along(side + "Hand", 0.15) + Vector((sign * 0.01, -0.05, -0.01)), BLACK)

    b.boxOn(side + "UpperArm", (0.085, 0.012, 0.07), b.along(side + "UpperArm", 0.32) + Vector((0, -0.057, 0)), FATIGUE_DARK)
    b.boxOn(side + "UpperArm", (0.05, 0.006, 0.035), b.along(side + "UpperArm", 0.32) + Vector((0, -0.065, 0.008)), GEAR)
    b.boxOn(side + "LowerArm", (0.08, 0.025, 0.075), b.head(side + "LowerArm") + Vector((0, 0.045, 0)), BLACK)
    b.boxOn(side + "LowerArm", (0.025, 0.1, 0.085), b.head(side + "LowerArm") + Vector((sign * 0.02, 0.0, 0)), STRAP)
    b.boxOn(side + "Hand", (0.05, 0.07, 0.012), b.along(side + "Hand", 0.6) + Vector((0, 0, 0.025)), GEAR_DARK)
    fingers = b.along(side + "Hand", 0.78)
    for dy in (-0.021, 0.0, 0.021):
        b.boxOn(side + "Hand", (0.055, 0.004, 0.042), fingers + Vector((0, dy, 0)), STRAP)


def makeLeg(b, side):
    """전투복 바지(옆 건빵 주머니), 무릎 보호대, 코요테색 전투화(밑창·끈). 오른쪽 허벅지에 권총집."""
    sign = 1 if side == "Left" else -1
    b.ball(side + "UpperLeg", 0.07, FATIGUE, segments=ROUND)
    b.segment(side + "UpperLeg", 0.085, 0.062, FATIGUE, start=0.0, end=0.95, segments=ROUND)
    b.ball(side + "LowerLeg", 0.06, FATIGUE, segments=ROUND)
    b.segment(side + "LowerLeg", 0.058, 0.052, FATIGUE, start=0.05, end=0.62, segments=ROUND)
    b.segment(side + "LowerLeg", 0.062, 0.057, GEAR, start=0.6, end=1.05, segments=ROUND)
    heel = b.head(side + "Foot")
    toe = b.head(side + "Toes")
    b.boxOn(side + "Foot", (0.1, 0.19, 0.08), Vector((heel.x, (heel.y + toe.y) / 2 + 0.025, 0.04)), GEAR)
    b.boxOn(side + "Toes", (0.095, 0.085, 0.06), Vector((toe.x, toe.y - 0.035, 0.03)), GEAR)

    pocket = b.along(side + "UpperLeg", 0.55) + Vector((sign * 0.075, 0, 0))
    b.boxOn(side + "UpperLeg", (0.022, 0.1, 0.11), pocket, FATIGUE_DARK)
    b.boxOn(side + "UpperLeg", (0.026, 0.104, 0.025), pocket + Vector((0, 0, 0.05)), FATIGUE)
    knee = b.head(side + "LowerLeg")
    b.boxOn(side + "LowerLeg", (0.095, 0.035, 0.11), knee + Vector((0, -0.058, -0.02)), BLACK)
    b.segment(side + "LowerLeg", 0.063, 0.063, STRAP, start=0.16, end=0.2, segments=ROUND)
    b.boxOn(side + "Foot", (0.106, 0.196, 0.018), Vector((heel.x, (heel.y + toe.y) / 2 + 0.025, 0.009)), BLACK)
    b.boxOn(side + "Toes", (0.101, 0.09, 0.018), Vector((toe.x, toe.y - 0.035, 0.009)), BLACK)
    for k in range(4):
        b.boxOn(side + "LowerLeg", (0.05, 0.008, 0.008), Vector((heel.x, -0.06, 0.11 + k * 0.04)), STRAP)
    if side == "Right":
        holster = b.along("RightUpperLeg", 0.3) + Vector((-0.085, -0.01, 0))
        b.boxOn("RightUpperLeg", (0.045, 0.08, 0.17), holster, BLACK)
        b.boxOn("RightUpperLeg", (0.04, 0.035, 0.05), holster + Vector((0, 0.01, 0.1)), STRAP)
        for dz in (-0.05, 0.03):
            b.segment("RightUpperLeg", 0.083, 0.08, STRAP, start=0.3 + dz * 2.2, end=0.33 + dz * 2.2, segments=ROUND)


def makeGuard():
    """뼈대와 군인 가드 몸을 만든다."""
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
    """가드를 만들어 검사하고 FBX로 내보낸다. 선택적으로 .blend와 미리보기를 저장한다."""
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
