"""
가드가 던지는 로우폴리 연막탄(SM_SmokeGrenade) 생성 스크립트.
원통 몸체 + 하늘색 띠(연막 색과 같아 무엇이 터질지 알 수 있게) + 신관·안전손잡이·안전핀 고리.

실행:
  blender -b --python generate_smoke_grenade.py -- <모델 출력 폴더> [<.blend 저장 경로>] [<미리보기 폴더>]

규칙은 mapkit_common.py와 같다(미터 단위). 날아가며 중심을 축으로 구르므로 원점은 몸체 중심이다.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "MapKit", "Source~"))
from mapkit_common import MeshBuilder, configure, exportFbx, renderPreview, resetScene, rotX, rotY  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
outDir = argv[0] if len(argv) > 0 else os.path.join(os.path.dirname(__file__), "..", "Models")
blendPath = argv[1] if len(argv) > 1 else None
previewDir = argv[2] if len(argv) > 2 else None

# 이름: (색, 금속도, 거칠기, 발광세기)
palette = {
    "SmokeBody":  ((0.27, 0.30, 0.27), 0.2, 0.6, 0.0),
    "SmokeBand":  ((0.62, 0.84, 0.96), 0.0, 0.5, 0.0),
    "SmokeMetal": ((0.48, 0.49, 0.50), 0.8, 0.35, 0.0),
    "SmokeVent":  ((0.06, 0.06, 0.07), 0.0, 0.8, 0.0),
}
configure(palette, "M_Char_")

BODY_RADIUS = 0.032
BODY_BOTTOM = -0.065
BODY_TOP = 0.045


def makeSmokeGrenade():
    """연막탄 메시를 만든다. (몸체 중심이 원점, 신관이 +Z)"""
    b = MeshBuilder("SmokeGrenade", "Weapon", bevel=0.002)
    bodyHeight = BODY_TOP - BODY_BOTTOM
    b.cylinder(BODY_RADIUS, BODY_RADIUS, bodyHeight, (0, 0, (BODY_TOP + BODY_BOTTOM) / 2), "SmokeBody", segments=16)
    # 띠는 몸체보다 살짝 두껍게 감아 색이 확실히 구분되게 한다
    b.cylinder(BODY_RADIUS + 0.0012, BODY_RADIUS + 0.0012, 0.024, (0, 0, 0.0), "SmokeBand", segments=16)
    b.cylinder(BODY_RADIUS + 0.0012, BODY_RADIUS + 0.0012, 0.008, (0, 0, -0.04), "SmokeBand", segments=16)
    # 바닥 테두리·윗면 테두리
    b.cylinder(BODY_RADIUS + 0.001, BODY_RADIUS + 0.001, 0.006, (0, 0, BODY_BOTTOM + 0.003), "SmokeMetal", segments=16)
    b.cylinder(BODY_RADIUS + 0.001, BODY_RADIUS - 0.004, 0.006, (0, 0, BODY_TOP + 0.003), "SmokeMetal", segments=16)
    # 신관과 연기 구멍
    b.cylinder(0.019, 0.017, 0.022, (0, 0, BODY_TOP + 0.017), "SmokeMetal", segments=12)
    b.cylinder(0.011, 0.011, 0.007, (0, 0, BODY_TOP + 0.031), "SmokeMetal", segments=10)
    with b.noBevel():
        for x, y in ((0.018, 0.018), (-0.018, 0.018), (0.018, -0.018), (-0.018, -0.018)):
            b.cylinder(0.004, 0.004, 0.002, (x, y, BODY_TOP + 0.0055), "SmokeVent", segments=6)
    # 안전손잡이: 신관 위에서 몸체 옆(+X)을 따라 내려온다
    b.box((0.022, 0.013, 0.004), (0.022, 0, BODY_TOP + 0.03), "SmokeMetal")
    b.box((0.004, 0.013, 0.012), (0.033, 0, BODY_TOP + 0.024), "SmokeMetal", rot=rotY(-25))
    b.box((0.004, 0.013, 0.085), (BODY_RADIUS + 0.003, 0, 0.0), "SmokeMetal", rot=rotY(-3))
    # 안전핀과 고리(반대편 -X)
    b.tube((-0.002, 0, BODY_TOP + 0.022), (-0.024, 0, BODY_TOP + 0.022), 0.0016, "SmokeMetal", segments=6)
    b.hollowCylinder(0.012, 0.012, 0.003, 0.0025, (-0.034, 0, BODY_TOP + 0.02), "SmokeMetal", segments=12, rot=rotX(90))
    return b.build()


def main():
    """연막탄을 만들어 FBX로 내보내고, 선택적으로 .blend와 미리보기를 저장한다."""
    resetScene()
    grenade = makeSmokeGrenade()
    tris = sum(len(p.vertices) - 2 for p in grenade.data.polygons)
    size = grenade.dimensions
    print(f"[SmokeGrenade] {tris} tris, {size.x:.3f}x{size.y:.3f}x{size.z:.3f}m -> {exportFbx(grenade, outDir)}")
    if blendPath:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blendPath))
    if previewDir:
        renderPreview([grenade], os.path.join(os.path.abspath(previewDir), "preview_smoke_grenade.png"), elevation=20, azimuth=60)


main()
