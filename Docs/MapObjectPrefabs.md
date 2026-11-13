# 맵 물체 프리팹 (Prefabs/Obj)

맵에 놓는 물체(소품·가구·구조물 조각)는 모두 `Assets/_Project/Prefabs/Obj` 아래 프리팹으로 관리한다.
맵 씬에는 프리팹 인스턴스가 놓이므로, 프리팹(또는 원본 모델)을 고치면 모든 맵에 반영된다. 맵마다 다른 것(위치·크기·바꿔 끼운 발광 머티리얼·변신 등록)은 인스턴스 오버라이드로 남는다.

## 폴더

| 모델 (Art/MapKit/Models) | 프리팹 폴더 (Prefabs/Obj) | 이름 규칙 |
|---|---|---|
| Prop | Kit/Prop | `SM_` 뗌 (예: `SM_Barrel` → `Barrel`) |
| Structure | Kit/Structure | `SM_` 뗌 |
| Subway | Subway | `SM_Sub_` 뗌 |
| City | City | `SM_City_` 뗌 |
| GiantRoom 가구 | GiantRoom | `SM_GR_` 뗌 |
| GiantRoom 작은 물건 | GiantRoom/Small | `SM_GR_Small_` 뗌 |
| Nature | Nature | `SM_` 뗌 (나무 프리팹 `Tree_*`는 손으로 만든 것이라 도구가 덮어쓰지 않음) |
| Modern Vending Machine 팩 | Subway/VendingMachine | 팩 프리팹의 변형 |
| (직접 만든 것) | Train, TrainCar | 그대로 |

- 모델 프리팹은 모델(FBX)의 **프리팹 변형**이고, 메시마다 MeshCollider가 붙어 있다. 모델을 Blender로 다시 만들면 프리팹이 따라 바뀐다.
- 도구가 만든 프리팹에는 라벨 `MapObject`가 붙는다. 라벨이 없는 프리팹(손으로 만든 것)은 다시 만들기에서 덮어쓰지 않는다.

## 크기만 다른 같은 물체 → 프리팹 하나

| 옛 모델 | 프리팹 | 맵에서 |
|---|---|---|
| SM_Crate_1m / SM_Crate_06m | Kit/Prop/Crate | 작은 상자는 0.6배 (`MapAssets.Kit.CrateSmall` = `Crate.prefab?scale=0.6`) |
| SM_Floor_4x4 / SM_Floor_2x2 | Kit/Structure/Floor | 2×2는 가로·세로 0.5배 |
| SM_Wall_4m / 2m / 1m | Kit/Structure/Wall | 길이 방향(X)만 0.5배 / 0.25배 |
| SM_Locker / SM_Locker_Row3 | Kit/Prop/Locker | 3칸 묶음은 Locker 3개를 겹친 묶음 프리팹 `Locker_Row3` |
| SM_Box_Cardboard / SM_Box_Cardboard_Stack | Kit/Prop/Box_Cardboard | 더미는 상자 3개(맨 위는 약간 작게)를 겹친 묶음 프리팹 `Box_Cardboard_Stack` |

빌더 배치 경로에 `?scale=0.6`(균일) 또는 `?scale=0.5,1,0.5`(축별)를 붙이면 그 배율로 놓인다. (`MapPiecePlacer.LoadAsset`)
옛 모델 FBX(Crate_06m·Floor_2x2·Wall_2m·Wall_1m·Locker_Row3·Box_Cardboard_Stack)는 더 만들지 않는다. 아래 "기존 맵 교체"를 마친 뒤 지운다.

## 속이 빈 물체

| 모델 | 바꾼 점 |
|---|---|
| Kit Bucket | 위가 열린 양동이(바닥 1.5cm) + 바깥 테두리 고리 |
| Kit TrashBin | 위가 열린 휴지통 + 테두리 고리 |
| Kit Lamp_Ceiling | 갓이 속 빈 원뿔(안의 전구가 보임) |
| Subway CleaningCart | 카트의 양동이 속이 빔 |
| Subway WashBasin | 상판에 구멍 + 속이 빈 사각 세면볼 2개 |
| Subway Toilet | 속이 빈 타원 변기 그릇 + 고리 모양 시트 |
| GiantRoom Small_Mug | 속이 빈 머그잔(바닥 6mm, 커피 없음) |
| GiantRoom TrashCan | 속이 빈 휴지통 + 바닥의 구겨진 종이 |
| GiantRoom Small_TapeRoll | 가운데가 뚫린 테이프·심지 고리 |
| GiantRoom FloorLamp / DeskLamp | 갓이 속 빈 원뿔 |

공용 도형: `mapkit_common.py`의 `MeshBuilder.hollowCylinder`(위가 열린 원통 또는 양쪽이 뚫린 관)·`hollowBox`(위가 열린 상자).

## 메뉴

| 메뉴 | 하는 일 |
|---|---|
| `GameSettingTools/맵 물체/물체 프리팹 만들기·갱신 (Prefabs/Obj)` | 모든 물체 프리팹을 만들거나(라벨 있는 것만) 다시 만든다 |
| `GameSettingTools/맵 물체/맵 씬의 물체를 프리팹으로 교체 (전체 맵)` | 기존 맵 씬의 물체를 프리팹 인스턴스로 바꾸고 맵 최적화를 다시 한다 (결과 `Temp/MapObjectPrefabs/migration.txt`) |
| 맵 빌더(`맵 생성/…`) | 시작할 때 빠진 프리팹만 만들고, 모델 대신 프리팹을 놓는다 |

## 기존 맵 교체가 유지하는 것

이름·부모·순서·위치·회전·크기(통일 배율 반영)·켜짐·레이어·정적 플래그, 변신 사물 등록(번호·예전 벽 표시),
같은 경로 자식의 위치(빼 둔 서랍)·머티리얼(맵별 발광 등)·충돌체 볼록 여부·꺼 둔 충돌체, 모델에 없던 자식(나중에 붙인 조명 등).
