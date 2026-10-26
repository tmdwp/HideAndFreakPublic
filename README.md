# Hide&Freak

(개발 코드 이름: 가드 vs 마네킹 / GuardVsMannequin)

시설을 지키는 **가드**와 시설을 탈출하려는 **마네킹**의 비대칭 멀티플레이 게임입니다.
모티브: 메챠 카멜레온 + 데드 바이 데이라이트

- 인원: 2~10명 (가드는 최대 인원의 절반)
- 마네킹은 가짜 마네킹 사이에 섞이거나 맵의 사물로 변신해 숨고, 전원 장치를 파괴해 **사냥 시간**을 여는 것이 목표입니다.
- 가드는 소총과 수류탄(맞은 마네킹의 위치를 외곽선으로 드러내고 느리게 함)으로 마네킹을 찾아 제거합니다.
- 사냥 시간에는 조명이 꺼지거나 깜빡이고, 마네킹이 괴물(Ghoul)로 변해 공수가 뒤바뀝니다.

## 공개 범위
이 저장소(HideAndFreakPublic)는 공개용입니다. 직접 만든 코드·에셋(`Assets/_Project`, `Docs`, `UiDefaults`, 프로젝트 설정)과 공개 라이선스 라이브러리(FishNet, Steamworks.NET)만 들어 있습니다. 외부 에셋을 포함한 개발은 비공개 저장소에서 진행합니다.

이 저장소에는 재배포할 수 없는 외부 에셋이 **빠져 있어 그대로는 실행되지 않습니다.** 빠진 에셋은 다음과 같습니다.
- 유료 캐릭터 이동 에셋 Easy Character Movement 2(ECM2)
  - 이동 코드는 인터페이스(`ICharacterMotor`)로 분리되어 있어 ECM2가 없어도 게임 코드는 컴파일됩니다. 다만 아바타가 움직이지 않습니다.
  - ECM2를 직접 설치하면 `GVM_ECM2` 기호가 자동으로 켜지고(`Ecm2DefineSync`), 이동 어셈블리(`Assets/_Project/Scripts/MovementEcm2`)가 컴파일되어 동작합니다.
- 에셋 스토어의 맵·캐릭터·무기·사운드·이펙트 팩
- Standard Assets, DOTween
- Mixamo 감정 표현 애니메이션(`Assets/_Project/Animation/*.fbx`)
- Ghoul 모델·텍스처(`Assets/_Project/Art/Skins/Ghoul.fbx`, `Ghoul_Scribble.png`)

사용한 에셋 목록과 위치는 [Docs/AssetLicenses.md](Docs/AssetLicenses.md)에 있습니다. 실행하려면 각자 구입·임포트해야 합니다.

## 개발 환경
| 항목 | 내용 |
|---|---|
| 엔진 | Unity 6000.4.2f1, URP (Forward+) |
| 네트워크 | FishNet 4.7.3 (개발: Tugboat / 출시 예정: FishySteamworks) |
| 캐릭터 | Easy Character Movement 2 (ECM2) |
| 입력 | Input System |
| AI | AI Navigation (맵별로 미리 구운 NavMesh) |
| Steam | Steamworks.NET (개발 중 AppID 480) |
| 모델링 | Blender 5.2 (MapKit 생성 스크립트) |

## 진행 상황
자세한 계획과 구현 메모는 [Docs/DevelopmentPlan.md](Docs/DevelopmentPlan.md)를 참고하세요.

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 네트워크 기반 (접속, 역할 배정, 이동, 상태 머신) | ✅ 검증 완료 |
| 1 | 핵심 루프 (변신, 사격, 체력, 전원 장치, 승패, HUD) | ✅ 검증 완료 |
| 2 | 속이기와 탐지 (감정 표현, 가짜 마네킹 AI, 소음, 벽타기, 변신 회전) | ✅ 검증 완료 |
| 3 | 사냥 시간 (조명 소등, 손전등, 마네킹 1인칭·공격·제한 시야) | ✅ 검증 완료 |
| 4 | 관전 모드, 저체력 시야, 결과 화면 | ✅ 검증 완료 |
| 5 | Steam 로비 / 룸 씬 (방 만들기·코드·암호·초대, 준비, 호스트 설정) | 🧪 Steam 2PC 테스트 대기 |
| 6 | 설정(언어·키·감도·음량·그래픽), ESC 메뉴, 크레딧, HUD, 다국어(한/영), 데이터 파일, 캐릭터 외형, 맵 | 🧪 테스트 중 |
| 7-A | 게임플레이 개편: 시작 준비 3초, 가드 무작위 스폰·출발 보호, 수류탄(외곽선·감속), 공용 외곽선 | 🧪 테스트 중 |
| 7-B | 모드 지원: 모드 SDK(외형·애니메이션·맵·변신 사물·맵별 스탯), 같은 모드끼리만 입장 | 🧪 2PC 검증(B9) 남음 |

최근 추가된 것
- **사냥 시간 Ghoul 외형**: 사냥 시간이 되면 모든 PC에서 마네킹이 Ghoul로 바뀌고, 오른팔로 할퀴어 공격합니다.
- **가드 외형·무기 교체**: SWAT 외형에 소총을 들고, 양손 조준 IK로 두 손이 총을 잡습니다.
- **판마다 변신 사물 배치 섞기**: 크기가 비슷한 사물끼리 자리를 바꾸고, 조금씩 옮기거나 돌립니다.
- **MapKit**: Blender로 만든 맵 제작용 구조물·소품 모델 39개입니다.
- **새 맵 "지하 연구소"(Map_Laboratory), "환승역"(Map_TransferStation)**: 아래 맵 목록 참고.

## 맵
| 번호 | 씬 | 내용 |
|---|---|---|
| 0 | Map_Asylum | 버려진 정신병원 (Abandoned Asylum 데모 씬에서 생성) |
| 1 | Map_Industrial | 공장 지대, 실외 (RPG/FPS Industrial 데모 씬에서 생성) |
| 2 | Map_Hospital | 공포 병원 (Horror Hospital Pack 데모 씬에서 생성) |
| 3 | Map_AlchemistHouse | 연금술사의 집 (Alchemist House 데모 씬에서 생성) |
| 4 | Map_Laboratory | 지하 연구소. MapKit + 에셋 팩 소품으로 직접 설계했습니다. 6m 높이 중앙 실험실을 8개 구역이 둘러싸고 '#' 모양 복도로 이어집니다. (`Scenes/Custom`) |
| 5 | Map_TransferStation | 환승역. 지하 2호선(B2 승강장·B1 대합실)과 지상 1호선(승강장·선로 위 다리형 대합실)이 환승 계단으로 이어진 4층 구조입니다. (`Scenes/Custom`, 메뉴 **맵 생성 → 환승역**) |

- 데모 씬 맵: **GameSettingTools → 맵 생성** 메뉴로 다시 만듭니다.
- 직접 만든 맵: `Assets/_Project/Scenes/Custom`에 두면 맵 목록 뒤에 자동으로 붙습니다. 맵 제작 도우미의 **맵 목록 갱신**을 누르세요.
- `Game_Test`는 게임에 넣지 않는 시험용 씬입니다(모드 SDK 시험 원본). **맵 생성 → 지하 연구소 (Game_Test)** 가 이 씬에 연구소 맵을 새로 만듭니다.

## 테스트 방법
1. Unity에서 프로젝트를 엽니다. (이 저장소에서는 빠진 외부 에셋을 먼저 임포트해야 합니다)
2. 메뉴 **GameSettingTools → 테스트 환경 생성**을 실행합니다.
   - 캐릭터·무기 외형, 프리팹, 로비/룸/게임 시스템 씬, 테스트 씬(Game_Test)이 만들어집니다.
   - 빌드 순서(Lobby → Room → Game_Systems → 맵 씬)도 설정됩니다.
3. **`Lobby` 씬에서 Play** 하거나 빌드를 실행합니다. (맵 씬에는 NetworkManager가 없어 단독 실행되지 않습니다)
4. 접속합니다.
   - 한 PC 테스트: 한쪽은 **직접 방 만들기**, 다른 쪽은 **IP로 참가**(비우면 localhost)를 누릅니다.
   - Steam 테스트: PC 2대(Steam 계정 2개)에서 호스트가 **방 만들기**를 누르고, 게스트가 **방 코드**를 입력하거나 친구 초대를 수락합니다.
5. 룸에서 게스트는 **준비**, 호스트는 설정을 조정한 뒤 **게임 시작**을 누릅니다. 결과 화면이 끝나면 룸으로 돌아옵니다.

> 게임 버전(`Player Settings Version` + `GameVersion.Protocol`)이 다르면 접속이 거부됩니다. 네트워크로 주고받는 내용이 바뀌면 Protocol을 올리세요.

## 조작
기본 키입니다. 로비의 **설정** 또는 게임 중 **ESC → 설정**에서 바꿀 수 있습니다.

| 키 | 가드 | 마네킹 |
|---|---|---|
| WASD / 마우스 | 이동 / 시점 (1인칭) | 이동 / 시점 (3인칭) |
| 좌클릭 | 사격 | 전원 장치 파괴 (누르고 있기) / 사냥 시간: 공격 |
| 우클릭 | 수류탄 | 변신 / 해제 |
| Shift | 달리기 (스태미나 소모) | 달리기 (스태미나 소모) |
| Space | 점프 | 점프 |
| Q / E | – | 변신 중 좌우 회전 |
| 1~6 | – | 감정 표현 |
| E | 손전등 (사냥 시간) | – |
| ESC | 메뉴 (계속하기 / 설정 / 방 나가기) | 메뉴 (계속하기 / 설정 / 방 나가기) |
| ← / → | 관전 대상 전환 (사망 후) | 관전 대상 전환 (사망 후) |

## 폴더 구조
```
Assets/_Project/          게임 코드와 에셋
    Scripts/                  게임 코드 (Runtime 어셈블리)
    Content/                  모드와 함께 쓰는 콘텐츠 정의 (맵 정보, 변신 사물, 외형 — FishNet·ECM2 무참조)
    Editor/, ContentEditor/   에디터 도구 (테스트 환경·맵 생성, 맵 제작 도우미, 최적화, 모드 SDK)
    Art/                      머티리얼, 캐릭터 외형, 무기, 셰이더
    Art/MapKit/               Blender로 만든 맵 제작용 모델 (Models: FBX / Materials / Source~: 생성 스크립트·블렌드 원본)
    Scenes/                   로비·룸·게임 시스템·맵 씬 (Custom: 직접 만든 맵)
    ModSdk/                   모드 SDK 템플릿·설정
    Resources/                메모장·엑셀로 편집하는 데이터 파일
        Localization/strings.csv        화면 문장 (열 = key, ko, en … — 열을 추가하면 언어가 늘어남)
        Credits/credits.<언어>.txt      크레딧
        Data/maps.json                  내장 맵 목록 (맵별 설정·인원)
        Data/default_game_settings.json 게임 설정 기본값
        Data/game_settings.csv          룸 설정 항목의 순서·조정 단위·범위
        Data/game_tuning.csv            밸런스 수치 (이동 배율, 스태미나, 시야, 쿨타임 등 — id,value,설명)
UiDefaults/               UI 프리팹 초기화 기준본
Docs/                     개발 계획서, 모드 제작 가이드, UI·맵 가이드, 에셋 라이선스
Assets/FishNet, com.rlabrecque.steamworks.net   공개 라이선스 라이브러리
Assets/ECM2 및 그 밖의 에셋 팩                  외부 에셋 (이 저장소에는 없음)
```

## 문서
- [Docs/DevelopmentPlan.md](Docs/DevelopmentPlan.md): 개발 계획과 구현 메모
- [Docs/UiAndMapGuide.md](Docs/UiAndMapGuide.md): UI 수정, 맵 제작 도우미 사용법
- [Docs/ModdingGuide.md](Docs/ModdingGuide.md): 모드 제작 가이드
- [Docs/AssetLicenses.md](Docs/AssetLicenses.md): 사용 에셋과 라이선스

## 서드파티 라이선스
- **ECM2와 에셋 스토어 에셋은 Unity Asset Store EULA**를 따르며, 공개 저장소에 올릴 수 없습니다. 그래서 이 저장소에서는 제외했습니다.
- FishNet(FishNet License), FishySteamworks(MIT), Steamworks.NET(MIT)은 각 라이선스를 따릅니다.
- UI 폰트 NeoDunggeunmo Pro는 해당 폰트의 라이선스를 따릅니다.
