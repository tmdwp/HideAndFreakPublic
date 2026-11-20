"""
가드의 로우폴리 소총(SM_GuardRifle) 생성 스크립트. (Phase 11-3)
상자·원기둥을 조합한 각진 소총. 총구·양손 위치를 빈 오브젝트로 함께 만들어, Unity에서 추정 없이 그대로 쓴다.
- Muzzle: 총구 끝 (사격 이펙트 시작점)
- Right Hand: 권총 손잡이 (원점)
- Left Hand: 총열 덮개 아래 (받치는 손)

실행:
  blender -b --python generate_guard_rifle.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다. (미터 단위, 앞 = 블렌더 -Y = Unity +Z). 원점은 오른손이 잡는 권총 손잡이.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import MeshBuilder, configure, renderPreview, resetScene, rotX, rotY  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "GunMetal": ((0.16, 0.17, 0.18), 0.7, 0.45, 0.0),
    "GunGrip":  ((0.05, 0.05, 0.06), 0.1, 0.7, 0.0),
    "GunSight": ((0.85, 0.35, 0.10), 0.0, 0.5, 0.0),
}
configure(palette, "M_Char_")

# 손·총구 위치 (블렌더 좌표). 손 위치는 3인칭 양손 IK가 잡는 곳이다.
RIGHT_HAND = (0.0, 0.03, -0.035)
LEFT_HAND = (0.0, -0.33, 0.0)
MUZZLE = (0.0, -0.65, 0.045)


def makeRifle():
    """총 메시를 만든다. (권총 손잡이가 원점, 총구가 -Y)"""
    b = MeshBuilder("GuardRifle", "Weapon", bevel=0.004)
    # 몸체와 개머리판
    b.box((0.05, 0.28, 0.07), (0, -0.06, 0.035), "GunMetal")
    b.box((0.045, 0.22, 0.09), (0, 0.19, 0.0), "GunGrip")
    b.box((0.05, 0.03, 0.12), (0, 0.31, -0.005), "GunGrip")
    # 권총 손잡이·방아쇠울
    b.box((0.035, 0.045, 0.11), (0, 0.035, -0.05), "GunGrip", rot=rotX(-15))
    b.box((0.01, 0.012, 0.032), (0, -0.01, -0.02), "GunMetal")
    # 탄창
    b.box((0.03, 0.06, 0.13), (0, -0.09, -0.06), "GunGrip", rot=rotX(10))
    # 총열 덮개·총열
    b.box((0.05, 0.28, 0.06), (0, -0.34, 0.035), "GunGrip")
    b.cylinder(0.012, 0.012, 0.17, (0, -0.565, 0.045), "GunMetal", segments=8, rot=rotX(90))
    b.cylinder(0.018, 0.018, 0.04, (0, -0.63, 0.045), "GunMetal", segments=8, rot=rotX(90))
    # 위 레일·가늠자
    b.box((0.022, 0.46, 0.012), (0, -0.2, 0.078), "GunMetal")
    b.box((0.02, 0.02, 0.035), (0, 0.04, 0.095), "GunMetal")
    b.box((0.012, 0.012, 0.03), (0, -0.41, 0.095), "GunSight")
    # 양옆 총열 덮개 환기 구멍, 오른쪽(-X) 탄피 배출구·장전 손잡이, 왼쪽 조정간, 탄창 홈,
    # 손잡이 미끄럼 방지 홈, 개머리판 고무 패드, 앞 가늠쇠 받침, 멜빵 고리 (손·총구 위치는 그대로)
    for facing, x in (("+X", 0.025), ("-X", -0.025)):
        for k in range(4):
            u = (0.27 + k * 0.05) * (1 if facing == "-X" else -1)
            b.plate(facing, (x, 0, 0), 0.03, 0.014, 0.003, "GunMetal", u=u, v=0.035)
    b.plate("-X", (-0.025, 0, 0), 0.07, 0.022, 0.002, "GunMetal", u=-0.04, v=0.05)
    b.plate("-X", (-0.025, 0, 0), 0.012, 0.012, 0.025, "GunMetal", u=0.03, v=0.055)
    b.bolts("+X", (0.025, 0, 0), [(-0.05, 0.03)], "GunSight", radius=0.006, depth=0.004)
    for facing, x in (("+X", 0.015), ("-X", -0.015)):
        b.plate(facing, (x, 0, 0), 0.004, 0.1, 0.002, "GunMetal", u=-0.09 if facing == "+X" else 0.09, v=-0.06, tilt=0)
    with b.noBevel():
        for k in range(3):
            b.box((0.037, 0.004, 0.008), (0, 0.013 + k * 0.01, -0.03 - k * 0.025), "GunMetal", rot=rotX(-15))
        b.box((0.052, 0.012, 0.125), (0, 0.329, -0.005), "GunMetal")
        b.box((0.02, 0.026, 0.012), (0, -0.41, 0.08), "GunMetal")
        for y, z in ((-0.42, 0.0), (0.27, -0.05)):
            b.hollowCylinder(0.012, 0.012, 0.006, 0.004, (0, y, z), "GunMetal", segments=8, rot=rotY(90))
    return b.build()


def addSpot(name, location, parent):
    """총 메시의 자식으로 위치 표시용 빈 오브젝트를 만든다."""
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_size = 0.03
    empty.location = location
    empty.parent = parent
    bpy.context.scene.collection.objects.link(empty)
    return empty


def exportWithSpots(obj, spots, folder):
    """총 메시와 위치 표시를 Unity용 FBX 하나로 내보낸다. (Unity 축에 맞춰 회전을 굽는다)"""
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for target in [obj] + spots:
        target.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(folder, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=True,
        add_leaf_bones=False, bake_anim=False, path_mode="AUTO", embed_textures=False,
    )
    return path


def main():
    """소총을 만들어 FBX로 내보내고, 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    rifle = makeRifle()
    spots = [addSpot("Muzzle", MUZZLE, rifle), addSpot("Right Hand", RIGHT_HAND, rifle), addSpot("Left Hand", LEFT_HAND, rifle)]
    tris = sum(len(p.vertices) - 2 for p in rifle.data.polygons)
    size = rifle.dimensions
    print(f"[Rifle] {tris} tris, {size.x:.2f}x{size.y:.2f}x{size.z:.2f}m -> {exportWithSpots(rifle, spots, outDir)}")
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        renderPreview([rifle], os.path.join(os.path.abspath(previewDir), "preview_rifle.png"), elevation=20, azimuth=70)


main()
