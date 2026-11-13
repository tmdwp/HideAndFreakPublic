# 도시 길거리 맵 (Map_CityStreet)

밤의 도심 사거리. 64 × 40m, 바닥 높이 0(차도) / 0.15m(보도·블록).
메뉴: `GameSettingTools/맵 생성/도시 길거리 (Map_CityStreet)` → `Assets/_Project/Scenes/Custom/Map_CityStreet.unity`
빌더: `Assets/_Project/Editor/CityStreetBuilder.cs`, 미리보기: `Temp/CityStreet/*.png`, 크기 보고서: `Temp/CityStreet/sizes.txt`

## 구역

| 구역 | 범위 | 내용 |
|---|---|---|
| 큰길 | z −4~4 (동서) | 2차선 차도, 중앙선, 횡단보도, 버려진 차·오토바이, 양 끝 공사 차단 |
| 골목길 | x −4~4 (남북) | 2차선 차도, 횡단보도, 양 끝 공사 차단 |
| 보도 | 차도 둘레 4m | 가로등, 신호등, 소화전, 우체통, 휴지통, 벤치, 버스 정류장, 자전거, 화분 |
| 북서 블록 | x −32~−8, z 8~20 | 닫힌 상가(셔터) · 뒷골목(쓰레기통·에어컨 실외기·쓰레기봉투) · 카페(들어갈 수 있음) |
| 북동 블록 | x 8~32, z 8~20 | 편의점(들어갈 수 있음) · 닫힌 상가 |
| 남서 블록 | x −32~−8, z −20~−8 | 주차장(승용차·주차 정산소·차막이) |
| 남동 블록 | x 8~32, z −20~−8 | 공사장(가림막·자재·드럼통·고깔) |

마네킹은 북쪽 블록(카페·편의점·뒷골목)에서, 가드는 남쪽 블록(주차장·공사장)에서 나온다.

## 외곽과 하늘

- 외곽 4벽(x ±32, z ±20)은 **투명**(충돌체만)이다. 벽 너머로 하늘(skybox)의 밤 도시 파노라마가 보인다.
- 하늘 그림: `Assets/_Project/Art/MapKit/Textures/T_City_NightSkyline.png` (4096×2048, 360° 위도·경도), 머티리얼 `Materials/CityStreet/M_City_Skybox.mat`(Skybox/Panoramic). 생성 스크립트 `Source~/generate_city_skyline.py`.
- 영역 밖 거리(`Map/Outskirts`, 보이기만 하고 충돌체 없음): 네 도로가 맵 중심에서 100m까지 이어지고(차도·보도·중앙선·가로등·주차된 차), 양옆과 모서리에 불 켜진 건물(높이 7~24m)이 늘어선다. 맵 최적화·NavMesh·변신 사물에서 빠지며 정적 배칭으로 묶는다.
- 먼 거리 안개(선형 30~115m, 어두운 남색)가 거리 끝을 덮어 하늘 그림의 빌딩 실루엣과 이어지게 한다. (하늘에는 안개가 걸리지 않음, 사냥 시간 시야 효과는 이 값을 되돌린다)
- 카페 주방·편의점 창고 뒷벽, 뒷골목 끝 벽돌담, 주차장 뒤·옆 담, 공사장 남·동 가림막으로 블록 바깥쪽을 막았다.

## 변신 규칙

- **벽은 변신 대상이 아니다.** 건물 외벽·가게 안 벽·담장·공사 가림막은 `TransformableProp`을 붙이지 않는다. (벽 변신 불가는 모든 맵 공통 규칙)
- 변신 크기 제한(3m)보다 큰 것(승용차·버스 정류장·가로등·신호등)은 장식으로만 둔다.
- 나머지 소품은 모두 변신 사물로 등록한다.

## 오브젝트 모델 목록

Blender 생성 스크립트: `Assets/_Project/Art/MapKit/Source~/generate_city_props.py` → `Assets/_Project/Art/MapKit/Models/City/SM_City_*.fbx`
(미터 단위, 피벗은 바닥 중앙, 앞면 +Z. 발광 부분은 머티리얼 `M_City_Glow*` 이름으로 찾아 맵 발광 머티리얼로 바꿔 끼운다.)

| # | 모델 | 크기(가로×깊이×높이 m) | 변신 | 쓰는 곳 |
|---|---|---|---|---|
| 1 | SM_City_StreetLamp | 0.4 × 1.4 × 5.2 | ✕ (높음) | 보도 가로등 (빛은 맵 조명) |
| 2 | SM_City_TrafficLight | 0.4 × 2.6 × 4.4 | ✕ (높음) | 사거리 네 모서리 |
| 3 | SM_City_FireHydrant | 0.45 × 0.45 × 0.8 | ○ | 보도 |
| 4 | SM_City_Mailbox | 0.5 × 0.45 × 1.3 | ○ | 보도 |
| 5 | SM_City_StreetBin | 0.55 × 0.55 × 1.0 | ○ | 보도·정류장 |
| 6 | SM_City_ParkBench | 1.6 × 0.6 × 0.85 | ○ | 보도·공사장 옆 |
| 7 | SM_City_BusStop | 3.6 × 1.5 × 2.6 | ✕ (큼) | 큰길 남쪽 보도 |
| 8 | SM_City_NewsBox | 0.5 × 0.45 × 1.1 | ○ | 정류장·편의점 앞 |
| 9 | SM_City_Bollard | 0.2 × 0.2 × 0.9 | ○ | 사거리 모서리 |
| 10 | SM_City_PlanterBox | 1.2 × 0.6 × 1.1 | ○ | 보도·카페 앞 |
| 11 | SM_City_Dumpster | 1.9 × 1.1 × 1.3 | ○ | 뒷골목·공사장 |
| 12 | SM_City_TrashBags | 1.0 × 0.8 × 0.6 | ○ | 뒷골목 |
| 13 | SM_City_ACUnit | 0.85 × 0.35 × 0.65 | ○ | 뒷골목 벽 아래 |
| 14 | SM_City_UtilityBox | 0.9 × 0.5 × 1.4 | ○ | 보도·주차장 |
| 15 | SM_City_Bicycle | 1.7 × 0.5 × 1.0 | ○ | 보도·카페 앞 |
| 16 | SM_City_Scooter | 1.8 × 0.6 × 1.15 | ○ | 편의점 앞·큰길 |
| 17 | SM_City_Sedan | 4.4 × 1.8 × 1.45 | ✕ (큼) | 주차장·큰길 (엄폐물) |
| 18 | SM_City_CafeTable | 0.7 × 0.7 × 0.75 | ○ | 카페 안·밖 |
| 19 | SM_City_CafeChair | 0.45 × 0.5 × 0.85 | ○ | 카페 안·밖 |
| 20 | SM_City_Parasol | 2.2 × 2.2 × 2.4 | ○ | 카페 앞 |
| 21 | SM_City_SignBoard | 0.55 × 0.5 × 0.95 | ○ | 카페·편의점 앞 입간판 |
| 22 | SM_City_PhoneBooth | 1.0 × 1.0 × 2.3 | ○ | 보도 |
| 23 | SM_City_Barricade | 1.6 × 0.5 × 1.05 | ○ | 도로 끝 차단·공사장 |
| 24 | SM_City_WaterBarrier | 1.4 × 0.45 × 0.8 | ○ | 도로 끝 차단·공사장 |
| 25 | SM_City_FridgeCase | 1.2 × 0.75 × 2.0 | ○ | 편의점 냉장고 |
| 26 | SM_City_ShopShelf | 1.5 × 0.6 × 1.5 | ○ | 편의점 진열대 |
| 27 | SM_City_Counter | 1.8 × 0.7 × 1.0 | ○ | 편의점·카페 계산대 |
| 28 | SM_City_CrateStack | 0.6 × 0.45 × 0.75 | ○ | 편의점·카페 뒤 (플라스틱 상자 더미) |
| 29 | SM_City_ParkingBooth | 1.6 × 1.6 × 2.5 | ○ | 주차장 정산소 |
| 30 | SM_City_RoadSign | 0.7 × 0.15 × 2.8 | ○ | 보도 표지판 |
| 31 | SM_City_ConstructionFence | 2.0 × 0.5 × 2.0 | ✕ (벽) | 공사장 가림막 |
| 32 | SM_City_WheelStop | 1.6 × 0.3 × 0.12 | ✕ (바닥) | 주차장 차막이 |

함께 쓰는 기존 모델: MapKit `SM_TrafficCone`, `SM_Barrel`, `SM_Pallet`, `SM_Crate_1m`, `SM_Box_Cardboard(_Stack)`, `SM_Pipe_Straight_2m`, `SM_Sandbag_Wall`, 지하철 소품 `SM_Sub_Planter`, `SM_Sub_WetFloorSign`, Modern Vending Machine.

## 구조물 (빌더가 기본 도형으로 만든다, 변신 안 됨)

차도·보도 판, 연석, 차선·횡단보도 칠, 맨홀, 건물 외벽(창문 발광·셔터·차양·간판), 가게 안 벽·천장, 뒷골목 담장, 주차장 담·선, 공사장 가림막.

## 다시 만들기

1. Blender 모델: `"E:\New Folder\blender.exe" -b --factory-startup --python Assets/_Project/Art/MapKit/Source~/generate_city_props.py -- Assets/_Project/Art/MapKit/Models/City Assets/_Project/Art/MapKit/Source~/CityProps.blend Assets/_Project/Art/MapKit/Source~`
2. 하늘 그림: `"E:\New Folder\blender.exe" -b --factory-startup --python Assets/_Project/Art/MapKit/Source~/generate_city_skyline.py -- Assets/_Project/Art/MapKit/Textures/T_City_NightSkyline.png`. Unity 메뉴 `GameSettingTools/맵 생성/도시 길거리 (Map_CityStreet)` (씬을 덮어쓰므로 직접 고친 내용은 사라진다)
