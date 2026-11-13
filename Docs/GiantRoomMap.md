# 거대한 방 맵 (Map_GiantRoom)

밤의 아이 방을 10배로 키운 맵. 방 안쪽 50 × 50m, 높이 35m, 바닥 높이 0. 플레이어(1.8m)는 실제로 18cm 크기가 된 셈이다.
메뉴: `GameSettingTools/맵 생성/거대한 방 (Map_GiantRoom)` → `Assets/_Project/Scenes/Custom/Map_GiantRoom.unity`
빌더: `Assets/_Project/Editor/GiantRoomBuilder.cs`, 미리보기: `Temp/GiantRoom/*.png`, 크기 보고서: `Temp/GiantRoom/sizes.txt`

## 배치 (x 동서, z 남북, 단위 m)

| 가구 | 자리 | 윗면 높이 | 오르는 길 |
|---|---|---|---|
| 침대 | 북서 x −24.7~−10.7, z 4.7~24.7 | 이불 5.5 | 동쪽으로 바닥까지 흘러내린 이불 자락(걸어서 오르는 비탈, 최대 42°) |
| 협탁 | 침대 옆 x −8, z 22.5 | 5.8 | 침대 위에서 점프 |
| 책상 + 의자 | 동쪽 벽 x 18.7~24.7, z 0~12 | 7.5 (의자 4.7) | 책상 앞으로 늘어진 스탠드 전선(타고 오름, 바닥을 따라 동쪽 벽 콘센트까지) |
| 책장 (선반 6단) | 북쪽 벽 x 0~8 | 선반 0.3 / 3.8 / 7.3 / 10.8 / 14.3, 꼭대기 18 | 앞에 쌓은 책 계단(→3.8), 3단·꼭대기 책에서 늘어진 책갈피 끈(3.8→10.8→18) |
| 소파 | 서쪽 벽 x −24.7~−16.7, z −17~1 | 좌판 4.8 | 탁자에서 좌판까지 걸쳐 놓은 큰 책 다리 |
| 낮은 탁자 + 러그 | x −13.5~−8.5, z −13~−3 | 4.5 | 동쪽에서 책을 쌓은 계단(한 단 0.4m = 책 한 권) |
| 옷장 | 남서 | 20 | 없음 (장식) |
| 장난감 상자 | 남동 x 13, z −21 | 4.6 | 없음 |
| 문 | 남쪽 벽 x 4 | — | 가드 출발 지점이 문 앞 |
| 창 | 북쪽 벽 x −8~4, 높이 20~31 | — | 밤하늘·달빛 |

마네킹은 방 곳곳과 가구 위에서, 가드는 문 앞에서 나온다.

## 변신 규칙

- **변신 사물은 작은 물건(연필·지우개 등)만이다.** 실제 크기 모델을 10배로 키워 변신 크기 제한(3m) 안에 들어온다.
- 가구·벽·책 계단·책장 속 책·사다리·밧줄은 변신 대상이 아니다.
- 변신한 마네킹은 지금처럼 벽과 가구 옆면을 타고 오를 수 있다.

## 매달린 줄 타기 (전선·책갈피 끈)

- `Climbable`(Content): 타고 오르는 물체 표시(종류: 사다리·밧줄). 이 맵은 밧줄 종류(전선·책갈피 끈)만 쓴다. 오르는 선 = 오브젝트 위치에서 위로 `height`, 위 끝에서 `topExit` 쪽으로 내려선다. 붙는 범위는 트리거(Ignore Raycast 레이어).
- `ClimbAbility`(MovementEcm2): 가드와 마네킹(변신 안 했을 때) 공통.
  - 가까이에서 바라보고 **W** → 붙기, **W/S** → 오르내리기, 위 끝 → 내려서기, 아래 끝에서 **S** → 놓기, **Space** → 뒤로 튕기며 놓기.
  - 오르는 속도는 이동 속도 × 0.7이라 서버 이동 검증 예산 안이다.
- 모드 맵에서도 `Climbable`을 쓸 수 있다. (Content 어셈블리라 모드 허용 컴포넌트)

## 오브젝트 모델 목록

Blender 생성 스크립트: `Assets/_Project/Art/MapKit/Source~/generate_giantroom_props.py` → `Assets/_Project/Art/MapKit/Models/GiantRoom/SM_GR_*.fbx`
(모두 **실제 크기**로 만들고 빌더가 10배로 키운다. 피벗은 바닥 중앙, 앞면 +Z. 발광 부분은 머티리얼 `M_GR_Glow`·`M_GR_Screen`.)

### 가구 (변신 안 됨) — 실제 크기 → 맵 크기

| 모델 | 실제(가로×깊이×높이 m) | 맵 크기 |
|---|---|---|
| SM_GR_Bed | 1.46 × 2.0 × 1.0 (매트리스 0.5) | 14.6 × 20 × 10 |
| SM_GR_Nightstand | 0.47 × 0.42 × 0.58 (속이 빈 몸체 + 서랍 2개) | 4.7 × 4.2 × 5.8 |
| SM_GR_Desk | 1.2 × 0.6 × 0.75 (서랍장은 속이 빈 몸체 + 서랍 3개) | 12 × 6 × 7.5 |
| SM_GR_DeskChair | 0.44 × 0.44 × 0.87 (좌판 0.47) | 4.4 × 4.4 × 8.7 |
| SM_GR_Bookshelf | 0.8 × 0.3 × 1.8 (선반 6단) | 8 × 3 × 18 |
| SM_GR_Book | 0.25 × 0.18 × 0.04 | 2.5 × 1.8 × 0.4. 책장(세워 꽂음)·책 계단·책 다리·책갈피 책은 장식, 침대·의자·바닥의 책은 변신 사물. 표지 색은 맵에서 바꿔 끼운다 |
| SM_GR_Wardrobe | 1.04 × 0.64 × 2.04 | 10.4 × 6.4 × 20.4 |
| SM_GR_Sofa | 1.8 × 0.8 × 0.8 (좌판 0.48) | 18 × 8 × 8 |
| SM_GR_CoffeeTable | 1.0 × 0.5 × 0.45 | 10 × 5 × 4.5 |
| SM_GR_Rug | 2.0 × 1.4 × 0.01 | 20 × 14 × 0.1 |
| SM_GR_FloorLamp | 0.4 × 0.4 × 1.6 | 4 × 4 × 16 |
| SM_GR_DeskLamp | 0.16 × 0.3 × 0.45 | 1.6 × 3 × 4.5 |
| SM_GR_CeilingLamp | 0.5 × 0.5 × 0.14 | 5 × 5 × 1.4 |
| SM_GR_ToyBox | 0.62 × 0.47 × 0.46 | 6.2 × 4.7 × 4.6 |
| SM_GR_TrashCan | 0.31 × 0.31 × 0.35 | 3.1 × 3.1 × 3.5 |
| SM_GR_Door | 1.0 × 0.08 × 2.1 | 10 × 0.8 × 21 |
| SM_GR_BlanketDrape | 0.7 × 1.1 × 0.57 (처지는 곡선 42°→20°) | 7 × 11 × 5.7 |

### 작은 물건 (변신 사물) — 실제 크기 → 맵 크기

| 모델 | 실제(m) | 맵 크기(m) |
|---|---|---|
| SM_GR_Small_Pencil | 0.18 × 0.008 | 1.8 × 0.08 |
| SM_GR_Small_Eraser | 0.05 × 0.02 × 0.012 | 0.5 × 0.2 × 0.12 |
| SM_GR_Small_Ruler | 0.25 × 0.03 × 0.002 | 2.5 × 0.3 × 0.02 |
| SM_GR_Small_Sharpener | 0.03 × 0.02 × 0.015 | 0.3 × 0.2 × 0.15 |
| SM_GR_Small_Crayon | 0.09 × 0.009 | 0.9 × 0.09 |
| SM_GR_Small_GlueStick | 지름 0.026 × 0.1 | 0.26 × 1.0 |
| SM_GR_Small_Scissors | 0.17 × 0.07 | 1.7 × 0.7 |
| SM_GR_Small_Coin | 지름 0.025 × 0.002 | 0.25 × 0.02 |
| SM_GR_Small_Dice | 0.03 | 0.3 |
| SM_GR_Small_ToyBlock | 0.04 | 0.4 |
| SM_GR_Small_Mug | 0.1 × 0.08 × 0.095 | 1.0 × 0.8 × 0.95 |
| SM_GR_Small_RubberDuck | 0.08 × 0.11 × 0.1 | 0.8 × 1.1 × 1.0 |
| SM_GR_Small_Battery | 0.05 × 0.014 | 0.5 × 0.14 |
| SM_GR_Small_StickyNotes | 0.075 × 0.075 × 0.012 | 0.75 × 0.75 × 0.12 |
| SM_GR_Small_Smartphone | 0.07 × 0.15 × 0.008 | 0.7 × 1.5 × 0.08 |
| SM_GR_Small_ToyCar | 0.08 × 0.04 × 0.035 | 0.8 × 0.4 × 0.35 |
| SM_GR_Small_PaperClip | 0.03 × 0.007 | 0.3 × 0.07 |
| SM_GR_Small_BottleCap | 지름 0.03 × 0.007 | 0.3 × 0.07 |
| SM_GR_Small_LegoBrick | 0.032 × 0.016 × 0.011 | 0.32 × 0.16 × 0.11 |
| SM_GR_Small_TapeRoll | 지름 0.08 × 0.02 | 0.8 × 0.2 |
| SM_GR_Small_Apple | 지름 0.08 | 0.8 |
| SM_GR_Small_AlarmClock | 0.1 × 0.05 × 0.12 | 1.0 × 0.5 × 1.2 |
| SM_GR_Small_TissueBox | 0.24 × 0.12 × 0.13 | 2.4 × 1.2 × 1.3 |

### 서랍 (따로 빼낼 수 있음)

책상(`SM_GR_Desk_Drawer1~3`)과 협탁(`SM_GR_Nightstand_Drawer1~2`)의 서랍은 같은 FBX 안의 **따로 된 자식 오브젝트**다. (번호는 아래부터)
- 씬에서 서랍 오브젝트를 가구 앞쪽으로 옮기면 빠진다. 서랍은 위가 열린 상자(바닥·옆판·뒤판·앞판·손잡이)이고 충돌체가 따로 붙는다.
- 빌더로 다시 만들 때 빼 둘 거리는 `GiantRoomBuilder`의 `DeskDrawerPull` / `NightstandDrawerPull`(m, 아래 서랍부터)로 정한다. 기본은 모두 닫힘.
- 서랍 칸 높이(맵 크기): 책상 바닥면 0.55 / 2.85 / 5.05m, 협탁 0.95 / 3.25m. 서랍 깊이: 책상 5.2m, 협탁 3.7m.

### 빌더가 기본 도형으로 만드는 것 (변신 안 됨)

바닥·천장·벽·창·걸레받이·콘센트, 스탠드 전선·플러그, 책갈피 끈·술, 조명 전구. (책은 모두 SM_GR_Book)

## 다시 만들기

1. Blender 모델: `"E:\New Folder\blender.exe" -b --factory-startup --python Assets/_Project/Art/MapKit/Source~/generate_giantroom_props.py -- Assets/_Project/Art/MapKit/Models/GiantRoom Assets/_Project/Art/MapKit/Source~/GiantRoomProps.blend Assets/_Project/Art/MapKit/Source~`
2. Unity 메뉴 `GameSettingTools/맵 생성/거대한 방 (Map_GiantRoom)` (씬을 덮어쓰므로 직접 고친 내용은 사라진다)
