# 가드 vs 마네킹 — 개발 계획서

> 원본 기획서: `가드 vs 마네킹.txt`
> 이 문서는 **개발 순서와 구현 기준**을 정의합니다. 작업은 반드시 Phase 순서대로 진행하고, 각 Phase의 **완료 기준**을 만족한 뒤 다음 Phase로 넘어갑니다.

---

## 0. 개발 원칙

### 0.1 환경
| 항목 | 값 |
|---|---|
| Unity | 6000.4.2f1 |
| 렌더 파이프라인 | URP (**Forward+ 렌더러**) |
| 네트워크 | FishNet (개발 중: **Tugboat** / 출시: **FishySteamworks**) |
| 캐릭터 컨트롤러 | ECM2 |
| 입력 | Input System |
| AI | AI Navigation (NavMesh) |
| Steam | Steamworks.NET (개발 중 AppID `480`) |

### 0.2 코딩 규칙
- 변수명·필드명은 **camelCase** (private 필드는 `_camelCase` 없이 `camelCase` 그대로 사용, 상수는 `PascalCase`)
- 모든 public/protected/private **메서드에 XML 주석(`/// <summary>`)** 작성
- **SOLID 원칙 준수**
  - **S**: 한 클래스는 한 가지 책임 (예: `GunTool`은 사격만, 소음은 `NoiseEmitter`가 담당)
  - **O**: 도구·상호작용 대상은 인터페이스(`ITool`, `IInteractable`)로 확장
  - **L**: 파생 클래스가 기반 계약을 깨지 않도록 (예: 모든 `ITool`은 쿨타임 규칙을 동일하게 따름)
  - **I**: 작은 인터페이스로 분리 (`IDamageable`, `INoiseSource`, `ITransformable`)
  - **D**: 게임플레이 로직은 구체 클래스가 아닌 인터페이스/ScriptableObject 설정에 의존
  - **적용 기준** (2026-10-02 점검 후 추가)
    - 역할(가드/마네킹)에 따른 `if`/`switch`를 여기저기 쓰지 않는다. 역할별 값(프리팹, 스탯, 표시 이름, 안내 문구)은 `RoleDefinition`/`RoleStats` 한곳에서 꺼낸다.
    - 여러 클래스에 같은 코드가 생기면(쿨타임 판정, 조준 원점 검증, 버튼 활성 조건 등) 공용 타입으로 뺀다. 기본값 숫자도 한곳에만 둔다: 호스트 설정 기본값은 `DefaultGameSettings`, 밸런스 조정 수치는 `GameTuning`(game_tuning.csv), 바꾸면 구조가 달라지는 값만 `GameConstants`.
    - UI는 "화면 관리 + 영역별 컴포넌트"로 나누고, 영역끼리는 인터페이스(`ILobbyNavigator`, `IRoomNotice`, `IHudSection` 등)로만 통신한다.
    - 판정 함수는 값만 반환하고 상태를 바꾸지 않는다. (예: 승패 판정은 결과를 반환하고 기록은 호출한 쪽이 한다)
    - 화면 목록·부모 화면처럼 늘어날 수 있는 구성은 코드 분기 대신 인스펙터 데이터로 받는다.
    - `GetComponentInParent` 등으로 찾은 의존성은 null을 확인하고, 없으면 원인을 로그로 남긴다(`UiLookup`).
    - 예외: 씬을 넘나드는 서비스의 정적 접근(`SessionServices`, `GameServices`, `RoomState.Instance`)은 인터페이스를 거치는 조건으로 허용한다.
  - 작업을 마치면 바꾼 코드를 위 원칙으로 점검하고, 어긴 곳이 있으면 보고에 함께 적는다.
- **서버 권한 원칙**: 판정·상태 변경은 서버(`[Server]`, `[ServerRpc]`)에서만. 클라이언트는 입력 전송과 연출만 담당
- 하드코딩된 수치 금지 → 모든 호스트 설정값은 `GameSettings`에서 읽음

### 0.3 폴더 구조
```
Assets/_Project/
├─ Scripts/
│  ├─ Core/          # GameStateMachine, GameSettings, 이벤트 버스, 공통 인터페이스
│  ├─ Network/       # 접속, 트랜스포트 전환, Steam 로비
│  ├─ Player/        # 공통 플레이어, 역할, 체력, 입력
│  │  ├─ Guard/
│  │  └─ Mannequin/
│  ├─ Tools/         # 총, 수류탄, 손전등
│  ├─ Props/         # 변신 가능 사물(TransformableProp), 외형 복사(PropAppearance), 번호 조회(PropRegistry)
│  ├─ Objectives/    # 전원 장치
│  ├─ PropPhysics/   # 총에 맞은 변신 사물 물리(서버 시뮬레이션·동기화)
│  ├─ Map/           # 스폰 지점·가드 스폰 선택, 조명 제어
│  ├─ Audio/         # 소음 시스템
│  ├─ Cameras/       # UnityEngine.Camera와 이름 충돌을 피하기 위해 복수형 사용
│  └─ UI/
├─ ScriptableObjects/
├─ Prefabs/
├─ Scenes/           # Lobby, Room, Game_<맵이름>
├─ Art/  Audio/  VFX/
```
- 네임스페이스: `GuardVsMannequin.<폴더명>`

### 0.4 게임 상태 흐름
```
Waiting ─▶ Preparation ─▶ Playing ─▶ Hunting ─▶ Ended
(접속 대기)  (가드 대기)     (탐색)      (사냥 시간)   (결과)
                              │                     ▲
                              └── 가드 승리 조건 ────┘
```

---

## 1. 확정된 게임 규칙 (기획서 보완 사항)

기획서의 모호한 부분에 대한 확정 사항입니다. 구현 시 이 표를 우선합니다.

| # | 항목 | 확정 규칙 |
|---|---|---|
| 1 | **공동 전원 장치 파괴 속도** | 참여 인원 `n`일 때 **총 속도 = 1.4^(n−1)** (1인 = 1.0, 2인 = 1.4, 3인 = 1.96, 4인 = 2.744). 초당 진행도 = `1.4^(n−1) / 파괴 소요 시간`. 개인 기여분 = 총 속도 / n. 한 명이라도 상호작용이 끊기면 **그 사람은 이탈**하고, **참여자가 0명이 되면 진행도 초기화** |
| 2 | **목표 수행 횟수 0회** | `Playing` 단계 진입 후 **룸 설정 `huntDelay`(기본 35초) 뒤 자동으로 사냥 시간** 시작 (가드 보호 목적). HUD의 목표 칸에 **가드 보호 시간과 사냥 시간 시작까지 남은 시간**을 표시한다 (`HuntTiming`) |
| 3 | **판 시작** | 스폰 직후 바로 탐색 시작, 이동·공격·변신 제한 없음. 대신 **시작 후 3초(`MatchOpeningTime`) 동안 모든 플레이어 시야가 검은 안개로 줄고, 마네킹은 전원 장치와 상호작용할 수 없음**. 가드는 마네킹 스폰 지점에서 먼 **무작위 위치**에서 시작. (예전 3초 이동 불가·가드 10초 도구 잠금을 대체) |
| 4 | **사냥 시간 중 가드 무기** | 사냥 시간에도 **총 사용 가능** (발사 간격·탄창·장전 동일). 마네킹 체력 배율은 **호스트 설정값 `huntHealthMultiplier`** (기본값 10) |
| 5 | **변신 불가 사물** | 천장, 바닥, **조명 기구**, **전원 장치**, **전원 장치 주변 구조물** |
| 6 | **조명 방식** | **실시간 조명** + URP **Forward+** 렌더러. 전구는 Emission 머티리얼과 Light 컴포넌트를 함께 끄고 켬 |

---

## 2. 핵심 데이터 정의

### 2.1 `GameSettings` (호스트 설정, 네트워크 동기화)
| 필드 | 타입 | 기본값 | 범위/비고 |
|---|---|---|---|
| `mapId` | int | 0 | |
| `preparationTime` | float | 20 | 15~30초 (가드 대기 시간) |
| `playTime` | float | 180 | 120~240초 (제한 시간) |
| `huntTime` | float | 60 | 사냥 시간 |
| `guardCount` | int | 1 | 1 ~ 전체 인원 / 2 |
| `objectiveCount` | int | 3 | 0 ~ (마네킹 수 + 2) |
| `generatorCount` | int | 5 | 반드시 `objectiveCount`보다 많아야 함 |
| `generatorDestroyTime` | float | 10 | 1인 기준 파괴 소요 시간 |
| `gunCooldown` | float | 1 | 발사 간격(초). 탄창 안에서 다음 발까지 |
| `gunMagazineSize` | int | 4 | 탄창 크기(1~10발). 다 쓰면 자동 장전 (Phase 8) |
| `gunReloadTime` | float | 2 | 발당 장전 시간(0.5~5초, 장전 시간 = 채울 탄 수 × 이 값) (Phase 8) |
| `detectorCooldown` | float | 10 | |
| `guardMaxHealth` | int | 3 | |
| `mannequinMaxHealth` | int | 1 | |
| `huntHealthMultiplier` | float | 10 | 사냥 시간 마네킹 체력 배율 |
| `mannequinAttackRange` | float | 3 | 구울(사냥 시간) 공격 사거리(m). 룸 UI에 표시하지 않음 |
| `guardMoveSpeed` | float | 2.2 | 가드 이동 속도(m/s). 룸 UI에 표시하지 않음 |
| `mannequinMoveSpeed` | float | 2.4 | 마네킹 기본 이동 속도(m/s). 사냥 시간(구울)에는 이 값 대신 game_tuning.csv의 `GhoulMoveSpeed`(기본 2.46). 룸 UI에 표시하지 않음 |

- 기본값은 `Resources/Data/default_game_settings.json`에, 룸에서 조정하는 단위·범위·표시 순서는 `Resources/Data/game_settings.csv`에 둡니다. 런타임 값은 `SyncVar`로 동기화하는 구조체로 관리합니다.
- 값 검증(범위 제한)은 `GameSettingsValidator`가 담당합니다 (SRP).

### 2.2 밸런스 조정 수치 (`GameTuning`, `Resources/Data/game_tuning.csv`)
- 개발 중에 다듬는 고정 수치(사냥 시간 이동 배율, 달리기·스태미나, 점프, 변신·상호작용 거리, 쿨타임, 사냥 시야, 가드 스폰·출발 보호, 수류탄·손전등, 가짜 마네킹 수, 결과 화면 시간 등)는 모두 이 파일 한 곳에서 `id,value,설명` 형식으로 고친다. 값과 설명이 전부 파일에 있으므로 목록은 파일을 본다.
- 빌드 후에도 `GameData/Data/game_tuning.csv`로 복사되어 고칠 수 있다(`DataFiles`). 파일에 없는 항목·숫자가 아닌 값은 코드 기본값, 모르는 id는 경고 후 무시.
- 값이 다른 PC끼리는 판정이 어긋나므로 파일 값의 지문을 게임 버전 끝(`-t xxxxxxxx`)에 붙여 접속을 막는다(`GameVersion`).
- 항목 추가: `GameTuning`에 속성 한 줄(이름 = id, 코드 기본값) + csv에 한 줄.

### 2.3 고정 상수 (`GameConstants`)
바꾸면 구조가 달라지는 값만 둔다: `MinPlayers` 2, `MaxPlayers` 10, `Damage` 1(기본 피해량), `GunRange` 100m(맵 천장보다 커야 함), `EmoteCount` 6.

### 2.4 주요 인터페이스
```csharp
public interface IDamageable { void TakeDamage(int amount, NetworkObject attacker); }
public interface IInteractable { bool CanInteract(PlayerRole role); void BeginInteract(NetworkConnection conn); void EndInteract(NetworkConnection conn); }
public interface ITool { bool IsReady { get; } void Use(); }
public interface INoiseSource { void EmitNoise(NoiseType type); }
public interface ITransformable { int PropId { get; } }
```

---

## 3. 개발 Phase

각 Phase는 **작업 목록 → 완료 기준** 순서로 구성됩니다. 체크박스를 채워가며 진행합니다.

### Phase 0 — 네트워크 기반

**목표:** 두 개의 클라이언트가 접속해 각자의 역할로 이동할 수 있다.

- [x] `_Project` 폴더 구조 생성, 어셈블리 정의(asmdef) 구성
- [x] URP 렌더러를 **Forward+**로 변경 (`PC_Renderer.asset`)
- [x] 테스트용 `Game_Test` 씬: 바닥, 벽, 대기실, 사물 몇 개
- [x] `NetworkManager` + Tugboat 설정, 테스트용 호스트/참가 버튼 (임시 UI)
- [x] `PlayerRole` (Guard / Mannequin / Spectator) 및 서버에서 역할 배정 (`RoleAssigner`: 가드는 최대 인원 / 2)
- [x] 역할별 플레이어 프리팹 스폰 (`PlayerSpawner`: 가드는 **대기실 스폰 지점**, 마네킹은 맵 스폰 지점)
- [x] ECM2 기반 이동: 가드 1인칭(속도 2), 마네킹 3인칭(속도 1.5)
- [x] `PlayerInputReader`: Input System 액션을 게임 입력 이벤트로 변환 (로컬 소유자만)
- [x] `GameStateMachine` (서버 권한, 상태 SyncVar) + 상태별 타이머
- [x] `GameSettings` 구조체 및 기본 프리셋 (하드코딩 값으로 시작)

**상태:** ✅ 검증 완료 (2026-09-24, 에디터 호스트 + 빌드 게스트)

**완료 기준:** 에디터 + 빌드 2개로 접속 → 역할이 배정되고 각자 올바른 시점으로 이동 → 상태가 `Preparation → Playing`으로 넘어가며 모든 클라이언트에 표시됨.

**구현 메모**
- 테스트 환경은 메뉴 `GameSettingTools > 테스트 환경 생성`(`TestEnvironmentSetupTool`)으로 생성한다. 다시 실행하면 프리팹과 씬을 새로 만든다. (Phase 1부터 Phase 0 툴을 대체)
- 이동은 **소유자 권한(client-authoritative) NetworkTransform**으로 동기화한다. 원격 아바타는 ECM2 시뮬레이션을 끈다. 지인용 게임이라 예측(prediction)보다 단순함을 우선하며, 사격·공격·체력 등 **판정은 계속 서버 권한**으로 처리한다.
- 게임 상태 규칙은 `IGameStateHandler` 구현체로 분리되어 있다. Phase 1 이후에는 `GameStateMachine.RegisterHandler`로 핸들러를 교체해 승패 조건 등을 추가한다.
- 입력은 `IPlayerInput`으로 추상화되어 있다. 액션 이름은 `GameInput.inputactions`의 Player 맵을 따른다.
- ESC로 커서 잠금을 전환한다. (임시 HUD 조작용이며, Phase 6에서 설정 메뉴로 대체)

---

### Phase 1 — 핵심 루프 (최소 플레이 가능 버전) ⭐

**목표:** 숨고, 쏘고, 전원 장치를 파괴하고, 승패가 난다.

#### 1-A. 가드 대기실
- [x] `WaitingRoomDoor` (NetworkBehaviour): `Preparation` 동안 닫힘, `Playing` 진입 시 서버가 열고 클라이언트에 동기화
- [x] 문이 닫혀 있는 동안 물리적으로 통과 불가 (콜라이더)

#### 1-B. 변신 시스템
- [x] `PropDatabase` (ScriptableObject): PropId ↔ 메시/머티리얼/콜라이더 크기 매핑 → (2026-10-05) 맵 물체 번호 + 외형 복사 방식으로 교체 (Phase 7-A 구현 결과 참고)
- [x] `TransformableProp` 컴포넌트: 변신 가능한 사물에 부착, PropId 보유
- [x] 변신 불가 대상 처리: 천장, 바닥, 조명 기구, 전원 장치, 전원 장치 주변 구조물에는 `TransformableProp`을 **붙이지 않음** (에디터 검증 툴로 확인)
- [x] 벽 변신: 벽에서는 **고정 크기 벽 조각** PropId로 변신
- [x] `MannequinTransformer`: 우클릭 → 조준한 사물 레이캐스트 → `ServerRpc(propId)` → 서버 검증 → `SyncVar<int> currentPropId` → 모든 클라이언트에서 외형 교체
- [x] 변신 중: 이동만 가능 (상호작용·감정 표현 입력 차단)
- [x] 변신 해제 쿨타임 5초 (`Preparation` 중에는 0초)
- [x] 여러 마네킹이 같은 사물로 변신 가능

#### 1-C. 가드 사격
- [x] `GunTool : ITool`: 좌클릭 → `ServerRpc(origin, direction)` → 서버 히트스캔
- [x] 히트스캔 사거리 > 맵 천장 높이 (맵마다 `MapInfo.ceilingHeight`로 검증)
- [x] 쿨타임 `gunCooldown` (탐지기와 **공유하지 않음**)
- [x] 명중 시 `IDamageable.TakeDamage`
- [x] 사냥 시간에도 사용 가능

#### 1-D. 체력·사망
- [x] `Health` (서버 권한, SyncVar): 최대 체력은 역할과 `GameSettings`에 따라 결정
- [x] 사망 시 플레이어 오브젝트 제거 → Spectator 역할로 전환 (관전은 Phase 4에서 구현, 여기서는 자유 카메라 임시 처리)

#### 1-E. 전원 장치
- [x] `Generator : IInteractable` (서버 권한)
  - 참여자 목록 관리, 매 틱마다 `progress += 1.4^(n−1) / generatorDestroyTime * deltaTime`
  - 참여자가 0명이 되면 `progress = 0`
  - `progress >= 1` → 파괴 상태, 이후 상호작용 불가
- [x] 마네킹 좌클릭 유지 = 상호작용 (변신 중에는 불가)
- [x] 진행도 SyncVar → 월드 공간 진행도 바 (마네킹에게만 표시)
- [x] `GeneratorSpawner`: 맵의 후보 위치 중 `generatorCount`개 무작위 배치
- [x] `ObjectiveTracker`: 파괴 횟수 카운트, `objectiveCount` 도달 시 사냥 시간 요청

#### 1-F. 승패 판정 (`WinConditionEvaluator`)
- [x] 가드 승리: ① 마네킹 전멸 ② `Playing` 제한 시간 종료 시 목표 미달성 ③ 사냥 시간 종료 시 가드 1명 이상 생존
- [x] 마네킹 승리: 사냥 시간 중 모든 가드 제거
- [x] 목표 수행 횟수 0회: `Playing` 진입 20초 후 사냥 시간 시작

#### 1-G. 최소 HUD
- [x] 남은 시간, 남은 마네킹 수, 달성 횟수 / 목표 횟수 (전원), 목표 안내 (마네킹만), 내 체력

**상태:** ✅ 검증 완료 (2026-09-24)

**완료 기준:** 가드 1명 + 마네킹 1~2명으로 한 판이 시작부터 승패 판정까지 오류 없이 진행된다. → **지인 테스트 플레이 진행**

**구현 메모**
- 대기실 문(`WaitingRoomDoor`)은 네트워크 오브젝트가 아니다. 이미 동기화된 게임 상태를 보고 각 피어가 같은 위치로 움직인다.
- 아바타 프리팹은 씬 오브젝트를 직렬화 참조할 수 없으므로, `GameServices.State`(`IGameStateProvider`)로 게임 상태를 조회한다.
- 승패와 사냥 시간 진입 조건은 `MatchRules`(`IMatchRules`)에 모여 있다. 상태 핸들러는 이 인터페이스에만 의존한다.
- 피해 가능 여부는 `DamageRules`가 판정한다. (가드→마네킹: 탐색/사냥, 마네킹→가드: 사냥 시간만)
- 변신 시 충돌체는 사물 경계에 맞춘 **캡슐로 근사**한다. (반지름 0.15~1.0m) 판정이 어색하면 Phase 2 이후 박스 충돌체로 교체를 검토한다.
- 전원 장치 상호작용은 **탐색(Playing) 단계에서만** 가능하다. (가드가 대응할 수 없는 준비 시간에 파괴되는 것을 방지)
- 사망 시 아바타를 제거하고 씬 카메라로 전환한다. 관전 모드는 Phase 4에서 구현한다.
- HUD는 IMGUI 임시 구현(`GameHud`)이며, 정식 UI는 Phase 6에서 교체한다.
- 변신 불가 대상 검증: 맵 제작 도우미의 `맵 검사`(전원 장치·천장/바닥·전원 장치 주변 구조물이 변신 사물이면 경고)

---

### Phase 2 — 속이기와 탐지

**목표:** 가짜 마네킹과 소음으로 심리전이 성립한다.

- [x] **감정 표현** 1~6번 키: `ServerRpc(emoteId)` → `ObserversRpc` 애니메이션 재생 (변신 중 불가)
- [x] **가짜 마네킹 AI** (서버 전용 로직, NetworkAnimator로 동기화)
  - 실제 마네킹과 **같은 프리팹 외형과 애니메이션** 사용
  - 행동 루프: 일정 시간 동안 무작위 방향 NavMesh 이동 또는 감정 표현
  - 게임 시작 시 무작위 수만큼 생성, 일정 간격으로 수 감소
  - 남은 시간 20초 또는 사냥 시간 진입 시 **전부 소멸** (실제 마네킹의 변신은 유지)
  - 가드의 총에 맞으면 소멸 (피격 반응은 실제 마네킹과 동일하게)
- [x] **소음 시스템**
  - `NoiseEmitter : INoiseSource` + `NoiseProfile` ScriptableObject (클립, 거리, 볼륨)
  - 서버에서 발생 → `ObserversRpc`로 3D 사운드 재생
  - 대상: 총 발사, 탐지기에 걸린 마네킹, 변신 상태의 주기적 소음, 전원 장치 파괴
- [x] **탐지기** `DetectorTool : ITool`: 우클릭 → 서버에서 범위 내 마네킹(변신 여부 무관) 검색 → 각자 소음 발생. 쿨타임 `detectorCooldown`
- [x] **벽타기** (변신 상태): ECM2 중력 방향 변경으로 벽면 부착
- [x] **마네킹 3인칭 카메라**: 벽에 붙었을 때 회전 각도 제한

**상태:** ✅ 검증 완료 (2026-09-25)

**완료 기준:** 가짜 마네킹과 실제 마네킹을 외형·움직임만으로 구분하기 어렵고, 소음을 듣고 위치를 추적할 수 있다.

**구현 메모**
- 감정 표현은 애니메이션 에셋이 없어 **절차적 움직임**(`ProceduralEmoteView`: 점프/회전/흔들기/인사/떨기/웅크리기)으로 임시 구현했다. 에셋 도입 시 `IEmoteView`의 Animator 구현으로 교체한다. 실제/가짜 마네킹은 같은 `EmoteController` + 뷰를 사용한다.
- 가짜 마네킹은 NetworkAnimator 대신 **서버 권한 NetworkTransform + 감정 표현 RPC**로 동기화한다. 외형은 실제 마네킹과 같은 생성 함수로 만든다.
- 가짜 마네킹 수는 6~12 무작위(`GameConstants`). 탐색 단계 동안 **남은 시간 20초까지 고르게 줄어들도록** 한 마리씩 제거한다.
- NavMesh는 서버가 **준비 단계 첫 진입 시 런타임으로 굽는다**(`NavMeshSurface`, 맵 충돌체 기준). 이때 대기실 문이 닫혀 있어 대기실은 분리된다. 전원 장치는 `NavMeshObstacle`로 구멍을 낸다.
- 소음은 `NoiseLibrary`(ScriptableObject)로 관리한다. 클립이 비어 있으면 `ProceduralNoiseClips`의 **임시 합성음**을 사용하므로, 사운드 에셋은 라이브러리에 지정만 하면 교체된다.
- 탐지기는 `PlayerIdentity`를 가진 **실제 마네킹만** 소음을 내게 한다. (가짜 마네킹이 반응하면 탐지기가 의미 없어짐) 반경은 `GameConstants.DetectorRadius` = 10m.
- 변신 중 주기적 소음 간격은 8~14초 무작위.
- 벽타기: 캐릭터를 **회전시키지 않고** ECM2 Flying 모드(중력 없음)로 면을 따라 이동한다. 바닥→벽(이동 방향 앞 벽), 벽→천장(위로 이동 중 머리 위 천장), 천장→벽, 벽→바닥(아래로 이동 중 발밑 바닥) 순으로 전환하며, 붙은 면이 사라지면 떨어진다. 벽에서는 W가 위로 오르기다. 이동/카메라는 `IMovementPlaneProvider`로 현재 면을 조회한다.
- 변신 중에는 이동해도 몸 방향이 바뀌지 않고 **Q/E로만 좌우 회전**한다(`DisguiseRotationAbility`, 120°/초). 마네킹의 회전 모드는 이 컴포넌트만 변경한다. (E는 가드 손전등 키와 같지만 역할이 겹치지 않음)
- 천장이 개방된 맵은 **투명 천장(충돌체만)** 을 둔다. 벽을 타고 오른 마네킹이 맵 밖으로 나가지 않게 한다. (투명 천장 위 실루엣은 기획상 재미 요소로 유지)
- 벽에 붙었을 때 카메라는 벽을 바라보는 방향 기준 좌우 75°, 천장에서는 아래에서 올려다보는 각도로 제한한다.

---

### Phase 3 — 사냥 시간

**목표:** 조명이 꺼지고 공수가 뒤바뀐다.

- [x] `LightController` (맵 단위): 등록된 모든 `MapLight`(Light + Emission 렌더러)를 일괄 On/Off
- [x] 사냥 시간 진입 연출: **텍스트·알림 없이** 모든 전구가 꺼짐 + 전원 차단 효과음
- [x] 사냥 시간 동안 환경광, 스카이박스 조정 (URP Volume 전환)
- [x] 가드 손전등 `FlashlightTool`: E키 On/Off, 배터리 무제한, 상태 SyncVar (다른 플레이어에게도 보임)
- [x] 마네킹 사냥 시간 변화
  - 체력 = `mannequinMaxHealth × huntHealthMultiplier` (현재 비율 유지 또는 가득 채움 중 선택, 기본은 **가득 채움**)
  - 이동 속도 2.2
  - 카메라 **1인칭 전환**
  - 진입 시 강제 변신 해제, 이후 다시 변신 가능 (Phase 6에서 변경)
  - 좌클릭 = 공격 (`attackRange` 내 가드에게 서버 판정, 변신 중에는 불가)
- [x] 사냥 시간 타이머 → 종료 시 승패 판정

**상태:** ✅ 검증 완료 (2026-09-25)

**완료 기준:** 목표 달성 또는 0회 설정 시 사냥 시간이 발동하고, 양측 승리 조건이 모두 정상 동작한다.

**구현 메모**
- 조명 연출(`HuntingLightingController`)은 대기실 문과 같이 **동기화된 게임 상태만 보고** 각 피어가 똑같이 수행한다. 조명은 0~0.5초 사이로 어긋나게 꺼지고, 전원 차단음은 2D로 전원에게 재생된다.
- 환경광은 URP Volume 대신 `RenderSettings`(환경광 Flat, 반사 0, 노출 0 스카이박스)로 어둡게 한다. 결과 화면까지 어둡게 유지하고, 다음 판 대기/준비 단계에서 되돌린다.
- HUD는 기획대로 사냥 시간 진입을 **텍스트로 알리지 않는다.** (탐색/사냥 시간 모두 단계 이름을 숨김) 역할별 조작 안내만 바뀐다.
- 원격 가드의 손전등이 실제 시선 방향을 비추도록 상하 시선을 `PlayerViewSync`로 동기화한다. (0.1초 간격, 1° 이상 변할 때만. Phase 4에서 관전용 좌우 시선까지 포함하도록 일반화)
- 마네킹 사냥 모드(`MannequinHuntingMode`): 서버는 강제 변신 해제와 체력 배율 적용(가득 채움), 소유자는 1인칭 전환과 속도 2.2를 적용한다. 1인칭에서는 마우스 좌우가 몸을 회전시킨다.
- 마네킹 시야(`HuntVisionEffect`): **본인 화면에만** 개인 조명(반경 8m, 그림자 없음)과 검은 선형 안개(2~9m)를 적용해 가까운 주변만 보이게 한다. 빌드에서 안개 셰이더가 제거되지 않도록 Graphics 설정의 Fog 스트리핑을 Custom(Linear 유지)으로 둔다.
- 마네킹 공격(`MannequinAttackTool`): 쿨타임 1초, 반경 0.35m 구체 판정, 사거리는 호스트 설정 `mannequinAttackRange`.
- 탐색 단계 좌클릭(전원 장치 파괴)과 사냥 시간 좌클릭(공격)은 게임 상태로 구분한다.

---

### Phase 4 — 사망 후 경험과 결과

- [x] **관전 모드**: 좌우 화살표로 생존 플레이어 순환, 관전 대상 이름 표시, 대상 사망 시 자동 전환
- [x] **저체력 시야 효과**: 잔여 체력 비율에 따라 URP Volume Vignette 강도·어둡기 증가 (자신에게만)
- [x] **결과 화면**: 승패, 역할별 결과 → 일정 시간 후 자동 또는 버튼으로 룸 씬 이동 (룸 씬은 Phase 5, 지금은 대기 상태로 복귀)
- [x] 킬 로그는 **표시하지 않음**, 다른 플레이어 체력도 **표시하지 않음**

**상태:** ✅ 검증 완료 (2026-09-25)

**완료 기준:** 사망한 플레이어가 끝까지 게임을 관전하고, 결과 후 룸으로 돌아간다.

**구현 메모**
- 관전: 아바타가 없을 때(사망 또는 미참가) 켜지는 씬 카메라를 `SpectatorController`가 생존자의 시야로 옮긴다. 생존자 목록은 클라이언트에 스폰된 아바타(`ClientPlayerList`)이며, 사망하면 아바타가 제거되므로 자동으로 다음 대상으로 넘어간다.
- 대상 시야: `PlayerViewSync`가 모든 플레이어의 카메라 시선(Pitch, Yaw)을 0.1초 간격으로 동기화하고, `SpectatorViewPoint`가 역할에 맞게 1인칭(가드, 사냥 시간 마네킹) 또는 3인칭(마네킹) 카메라 위치를 계산한다. 사냥 시간에 마네킹을 관전하면 그 마네킹의 시야 효과(`IViewEffect`: 개인 조명 + 안개)를 관전자 화면에도 똑같이 적용한다.
- 저체력 효과: `LowHealthVignette`가 런타임에 전역 URP Volume(비네트 최대 0.6 + 노출 최대 −1.5EV)을 만든다. 플레이어 카메라의 Post Processing을 켜 둔다.
- 결과: 참가자 목록은 서버에만 보관하다가 **판이 끝날 때 공개**한다(`MatchResult.Participants`). 진행 중 사망 정보가 네트워크로 새지 않게 하기 위함이다. 결과 공개 시점에 아바타가 남아 있으면 생존, 사망·접속 종료는 탈락으로 표시한다.
- 결과 화면은 10초 뒤 자동으로 대기 상태로 돌아가며, 호스트는 "바로 다음 판 준비" 버튼으로 즉시 돌아갈 수 있다. 결과 화면에 들어서면 버튼을 누를 수 있게 커서 잠금이 풀린다.

---

### Phase 5 — Steam 연동과 로비/룸 씬

- [x] Steamworks.NET(2025.164.1), FishySteamworks(4.1.1) 설치 (중복 steam_api 플러그인 정리)
- [x] 트랜스포트 선택: Multipass(Tugboat + FishySteamworks). Steam 방은 두 트랜스포트로 서버를 열고, 직접 방(개발/LAN)은 Tugboat만 사용 (`TransportConnector`)
- [x] **로비 씬**: 방 만들기(Steam 로비 생성 + 호스트 시작), 방 코드 참가(코드 = 로비 ID), 친구 초대/친구 방 참가, 직접 접속(개발용)
- [x] **룸 씬**
  - 호스트: 시작 버튼 (전원 준비 + 인원 조건 충족 시 활성화)
  - 게스트: 준비 / 준비 해제
  - 게임 설정 UI (호스트만 편집, 전원 열람): 맵, 대기 시간, 제한 시간, 사냥 시간, 가드·마네킹 수, 목표 수행 횟수(0 ~ 마네킹 수 + 2), 도구 쿨타임, 체력, **사냥 시간 체력 배율**
- [x] 호스트 이탈 시 모든 게스트를 로비 씬으로 퇴출
- [x] 게임 종료 후 같은 방으로 룸 씬 복귀

**상태:** 🧪 한 PC(직접 방) 테스트 완료 (2026-09-25) / Steam 2PC 테스트 대기

**완료 기준:** 서로 다른 PC의 Steam 친구끼리 초대/코드로 접속해 설정을 바꾸고 한 판을 끝까지 진행한다.

**구현 메모**
- 씬 흐름: `Lobby`(시작, 오프라인) → 서버 시작 시 FishNet `DefaultScene`이 `Room`을 전역 로드 → 호스트가 시작하면 `RoomState`가 `Game_Test`를 전역 로드 → 결과 후 `Room`으로 복귀. 연결이 끊기면 `DefaultScene`이 `Lobby`를 불러온다.
- `Lobby` 씬의 **NetworkRoot**(NetworkManager, Multipass, DefaultScene, Steam/세션 서비스)는 DontDestroyOnLoad로 유지된다. 로비로 돌아왔을 때 생기는 중복은 NetworkManager가 제거하고, 서비스는 "먼저 등록된 것이 유지" 규칙으로 등록한다. **Play는 반드시 Lobby 씬에서** 시작한다. (게임 씬에는 NetworkManager가 없음)
- `SessionService`(`ISessionService`): Steam 로비와 연결을 묶는다. Steam 방 = 로비 생성 → 서버(Tugboat + Steam) 시작 → 로컬 클라이언트는 Tugboat localhost로 접속. 참가 = 로비 입장 → 로비 데이터의 호스트 SteamID로 FishySteamworks 접속. 게임 실행 중 초대 수락(`GameLobbyJoinRequested_t`)과 초대로 실행(`+connect_lobby`)을 모두 처리한다.
- `RoomState`: 전역 네트워크 오브젝트. 참가자(이름·준비·호스트), 게임 설정을 동기화한다. 설정은 `GameSettingDefinitions`(항목·단위·범위)로 조정하고 인원 변화에 맞춰 `GameSettingsValidator`로 다시 보정한다. 게임 중에는 Steam 로비 참가를 막는다.
- 게임 씬은 `IRoomInfo`/`ISceneFlow`(Core 인터페이스)로만 룸에 접근한다. `MatchSessionBridge`가 모든 플레이어의 씬 로드를 기다려(최대 15초) 판을 시작하고, 결과 후 룸으로 돌려보낸다.
- NetworkManager의 `ObserverManager`에 **SceneCondition**을 기본 조건으로 둔다. 씬 오브젝트가 그 씬을 불러온 클라이언트에게만 스폰되게 해, 게스트가 게임 씬 로딩 중에 스폰 정보를 받아 `GameSystems`를 찾지 못하는 문제(SceneId not found)를 막는다. 전역 오브젝트(`RoomState`)는 조건에서 제외된다.
- 닉네임: 각 클라이언트가 접속 후 자신의 Steam 이름을 서버에 알린다. Steam이 없으면 "Player N".
- UI는 **uGUI**(에디터 툴로 씬에 생성, 인스펙터에서 편집 가능). 기본 폰트에 한글이 없어 `OsFontApplier`가 실행 시 OS 한글 폰트(맑은 고딕)를 적용한다. (Phase 6에서 TMP + 한글 폰트 에셋으로 교체 검토)
- 한 PC에서는 같은 Steam 계정으로 호스트/게스트를 동시에 실행할 수 없으므로, 한 PC 테스트는 **직접 방**으로, Steam 테스트는 PC 2대 + Steam 계정 2개로 한다.

---

### Phase 6 — 마무리

- [x] 설정: 키 리바인딩(Input System Rebinding API, 저장/불러오기), 마우스 감도, 사운드(마스터/BGM/효과음)
- [x] 게임 중 ESC 메뉴 (계속하기 / 설정 / 방 나가기) — 룸과 게임 씬
- [x] 크레딧: 기획자, 개발자, 이용한 에셋 (`Docs/AssetLicenses.md` 기반)
- [x] HUD를 IMGUI 임시 구현에서 uGUI로 교체
- [x] 다국어 (한국어/영어, CSV 열 추가로 언어 확장) — 설정 화면에서 언어 선택
- [x] 룸 설정에 가드/마네킹 이동 속도 추가 (마네킹 사냥 시간 속도 = 기본 × 1.5)
- [x] 사람이 관리하기 쉬운 데이터를 텍스트 파일로 분리 (문장, 크레딧, 게임 설정 기본값·범위)
- [x] 사냥 시간에도 마네킹 변신 허용 (진입 시 강제 해제는 유지, 변신 중 공격 불가)
- [x] 보안 보강: Steam 방 Tugboat 루프백 바인딩, 접속 검증(`RoomAuthenticator`), 로비 공개 범위 설정, 닉네임 정리, 서버 이동 검증(`MovementValidator`)
- [x] 텍스트 데이터 파일을 빌드 후에도 수정 가능하게 (빌드 폴더 `GameData`, 없거나 깨지면 내장 원본 → 코드 기본값)
- [x] 캐릭터 외형: 마네킹 3종(더미·슬라임·유령, 맵마다 선택), 가드(Ethan + 더미 애니메이션)
- [x] 가드 리볼버(1인칭·3인칭)와 총구에서 나가는 총알·명중·총구 화염 이펙트
- [x] 맵 4종 자동 생성 도구 (Asylum, Industrial, Hospital, Alchemist House) — 생성 후 배치 확인·조정 필요
- [ ] 정식 맵 다듬기: 자동 배치된 스폰·전원 장치·출발 지점·조명 위치 조정 (천장 개방 맵은 투명 천장, 프롭 실루엣 문제는 의도된 재미로 유지)
- [ ] 밸런스 조정, 최적화 (실시간 조명 개수, 네트워크 전송량)
- [ ] (검토) TextMeshPro + 한글 폰트 에셋으로 UI 폰트 교체

**상태:** 설정·ESC 메뉴·크레딧·HUD·로비 패널 분리·다국어·이동 속도 검증 완료 (2026-09-26) / 데이터 파일 분리·사냥 시간 변신·보안 보강·외부 데이터 파일·캐릭터 외형·총·맵 4종 검증 전

**구현 메모**
- 설정 화면은 탭으로 나뉜다: **그래픽**(화면 모드: 전체 화면(창)/창 모드/최대화 창, 해상도(누르면 펼쳐지는 스크롤 목록), 언어) · **사운드**(음량) · **조작 키**(마우스 감도, 키 설정). 코드는 틀(`SettingsPanelView`: 탭·열기/닫기·모두 기본값)과 설정 묶음(`ISettingsSection`: `DisplaySettingsSection`, `LanguageSettingsSection`, `SoundSettingsSection`, `ControlSettingsSection`)으로 나뉘며, 새 설정은 묶음 컴포넌트를 페이지에 두기만 하면 자동으로 연결된다.
- 룸 게임 설정 범위 확대(2026-09-29): 대기 15~180초, 탐색 120~1800초, 사냥 30~900초, 체력 가드 1~100·마네킹 1~500, 사냥 체력 배율 1~100, 공격 범위 1~30m, 이동 속도 0.5~20. 피해량 설정 추가(가드 총·마네킹 공격, 1~500, 기본 1). `GameSettingId`에 새 항목은 끝에만 추가한다(UI 프리팹이 번호로 저장).
- 룸 게임 설정(맵 제외)은 -/+ 버튼 외에 값을 눌러 키보드로 입력할 수 있다(호스트만). 누르면 단위 없는 숫자로 바뀌고 Enter/다른 곳 클릭으로 적용되며, 서버(`RoomState.SetSettingValue`)가 항목 범위와 인원 조건으로 보정한다. 숫자가 아니면 원래 값으로 돌아간다.
- 넘칠 수 있는 목록은 스크롤: 룸의 참가자·게임 설정 목록, 크레딧, 결과 화면 참가자, 키 설정.
- 게임 이름: **Hide&Freak** (로비 제목·크레딧·창 제목). 코드 네임스페이스와 저장소 이름은 GuardVsMannequin/GuardVsMonster 유지. 창 제목(productName)이 바뀌면 PlayerPrefs 저장 위치도 바뀌어 개인 설정이 한 번 초기화된다. 화면 설정은 `DisplaySettings`가 `Screen.SetResolution`으로 바로 적용하고, 해상도 목록은 모니터 지원 해상도(가로×세로 중복 제거)다. 플레이어가 한 번이라도 고르면 다음 실행 때도 적용된다. (에디터에서는 적용하지 않음) "모두 기본값으로"는 언어와 화면 설정을 유지한다.
- 개인 설정(`GameOptions`: 감도 배율, 전체/배경 음악/효과음 음량, 화면 크기·모드)과 키 바인딩은 `OptionsStore`가 **PlayerPrefs**(이 PC)에 저장한다. 호스트가 정하는 게임 설정(`GameSettings`)과는 별개다.
- 키 바인딩: 입력 에셋 복제본을 만드는 곳(`PlayerInputReader`, `SpectatorController`, `PauseMenuView`, 설정 화면)마다 `OptionsStore.ApplyBindings`로 저장된 바인딩을 적용하고, 게임 중 바꾸면 `BindingsChanged`로 즉시 반영한다. 바꿀 수 있는 키 목록은 `KeyBindingCatalog`.
- 음량: Unity에는 AudioMixer 에셋을 코드로 만드는 공개 API가 없어, 지금은 **전체 = `AudioListener.volume`, 효과음/배경 음악 = `AudioVolumes` 배율**로 적용한다. 사운드 에셋 도입 시 AudioMixer 그룹으로 교체할 수 있도록 음량 조회를 `AudioVolumes` 한곳에 모았다. (배경 음악은 아직 없음)
- ESC: 기존의 "커서 잠금 전환"을 메뉴로 교체했다(입력 액션 `ToggleCursor` → `Menu`). 커서 잠금은 `GameCursor`가 관리하며, 잠겨 있을 때만 이동·시점·클릭 입력을 받는다. 키 설정 중에는 ESC가 키 입력 취소로 쓰이므로 메뉴가 반응하지 않는다.
- 크레딧 내용은 `Resources/Credits/credits.<언어 코드>.txt` 텍스트 파일에서 편집한다. (기획·개발: 정승제) 현재 언어 파일이 없으면 ko 파일을 보여준다.
- **텍스트 데이터 파일** (모두 `Assets/_Project/Resources/` 아래, 메모장·엑셀로 편집. UTF-8로 저장)

  | 파일 | 내용 |
  |---|---|
  | `Localization/strings.csv` | 화면의 모든 문장. 열 = `key, ko, en, …`. `#` 줄은 메모, 값의 쉼표는 큰따옴표로 감싸고 줄바꿈은 `\n` |
  | `Credits/credits.ko.txt`, `credits.en.txt` | 언어별 크레딧 |
  | `Data/default_game_settings.json` | 방을 만들 때의 게임 설정 기본값 |
  | `Data/game_settings.csv` | 룸 설정 항목의 표시 순서, 조정 단위(step), 범위(min~max), 단위 문구 key |

- **다국어**: `Loc.Get(key)` / `Loc.Format(key, 값…)`로 문장을 얻는다. 고정 문구는 `LocalizedText` 컴포넌트(에디터 툴이 key로 만든 텍스트에 자동 부착), 상황에 따라 바뀌는 문구는 각 화면 코드가 채운다. 언어를 바꾸면 `Loc.LanguageChanged`로 즉시 갱신된다. 첫 실행 언어는 시스템 언어(한국어면 ko, 그 외 en)이며, 선택한 언어는 `GameOptions.languageCode`로 저장된다.
  - 새 언어 추가: `strings.csv`에 열 추가(머리글 = 언어 코드, `language.name` 줄에 그 언어로 쓴 이름) → 설정 화면 언어 목록에 자동 표시. 크레딧은 `credits.<코드>.txt` 추가. 번역이 비어 있는 칸은 ko로 표시된다.
  - 한국어·영어 외에 한자/가나 등을 쓰는 언어는 `OsFontApplier`의 폰트가 글자를 지원하는지 확인해야 한다.
- **이동 속도**: `MoveSpeedController`(가드/마네킹 프리팹)가 동기화된 이번 판 설정으로 `maxWalkSpeed`/`maxFlySpeed`(벽타기)를 맞추고, 마네킹은 사냥 시간에 1.5배로 바꾼다. 가짜 마네킹(NavMeshAgent)도 마네킹 기본 속도를 따른다.
- **사냥 시간 변신**: `MannequinTransformer.CanTransformIn`에 `Hunting`을 추가했다. 진입 순간의 강제 해제(`MannequinHuntingMode`)는 그대로 두어 사냥 시간 시작 시 모두 본모습이 드러난다. 변신 중에는 탐색 단계의 상호작용처럼 공격도 막는다(`MannequinAttackTool`). 1인칭에서 사물 메시가 시야를 가리지 않도록 본인 화면에서는 변신 외형도 그림자만 그린다(`PropDisguiseView.SetDisguiseShadowsOnly`). 해제 쿨타임 5초는 동일하다.
- **로비 화면**: 메인(방 만들기 / 참가 / 설정 / 크레딧 / 종료) → 방 만들기(스팀 방 만들기[암호] / 직접 방 만들기) · 참가(스팀 방 입장[방 코드·암호] / IP로 참가). 뒤로 버튼·ESC로 이전 화면. 요소는 Layout Group 없이 고정 위치·크기(`EditorUiFactory.Place`)로 배치해 프리팹에서 자유롭게 옮길 수 있다.
- **방 암호** (Steam 방): 호스트가 정한 암호를 참가자가 접속 요청(`JoinRequestBroadcast`: 버전+암호)으로 보내고 `RoomAuthenticator`가 확인한다. 로비 데이터에는 `HasPassword` 표시만 적고 암호는 적지 않는다. 초대로 들어온 방에 암호가 있으면 방 코드를 채운 입장 화면을 열어 암호를 받는다(`ISessionService.PasswordRequired`). 직접 방은 암호 없음.
- **버전 확인** (`GameVersion` = Player Settings의 Version + 네트워크 규약 번호 `Protocol`): 클라이언트는 접속 즉시 버전을 보내고(`JoinRequestBroadcast`), `RoomAuthenticator`가 호스트와 다르거나 10초 안에 보내지 않으면(옛 빌드) 거부한다. Steam 방은 로비 데이터 `Version`으로 입장 전에 먼저 걸러낸다. 거부 이유(버전 불일치·가득 참·게임 중·로비 미경유)는 `ConnectionRejectedBroadcast`로 보내 로비에 안내한다. **빌드를 배포할 때 Player Settings의 Version을 올리고**, 네트워크로 주고받는 것을 바꿨는데 버전을 올리지 않았다면 `GameVersion.Protocol`을 올린다.
- **보안** (호스트 = 서버인 P2P 구조라 호스트의 조작과 위치 정보를 이용한 월핵은 막을 수 없다. 게스트 쪽 부정행위와 무단 접속을 줄이는 것이 목표)
  - Steam 방에서는 Tugboat를 `127.0.0.1`/`::1`에만 연다(`TransportConnector`). 호스트 자신의 로컬 접속용이며, 방 코드 없이 IP로 들어오는 경로를 막는다. 직접 방은 인스펙터의 바인드 주소를 그대로 쓴다.
  - `RoomAuthenticator`(FishNet Authenticator, `SessionService`가 실행 시 등록): 판 진행 중 접속, 인원 초과, Steam 방의 비로비 SteamID·외부 IP 접속을 거부한다. 로비 멤버 목록 갱신이 늦을 수 있어 최대 5초 기다린다. `RoomState`는 **인증을 통과한 연결만** 참가자로 등록한다.
  - 로비 공개 범위는 `SteamLobbyService.lobbyType`(기본 FriendsOnly). Public은 코드를 아는 누구나 들어올 수 있고 AppID 480에서는 로비 검색에 보일 수 있다. **정식 AppID로 교체 전까지 FriendsOnly 권장.**
  - 닉네임은 서버에서 `<`, `>`, 제어 문자를 제거한다(리치 텍스트 태그로 UI를 깨뜨리는 것 방지).
  - `MovementValidator`(가드·마네킹 프리팹): 소유자 권한 이동을 서버가 검증한다. 최대 속도 × 1.3으로 이동 예산을 쌓고(최대 1.5초분 + 1m), 수평 이동·상승이 예산을 넘으면 마지막 정상 위치로 되돌린다(`TargetRpc`). 낙하는 검사하지 않는다. (Phase 7-A: 대기실 검사 → 시작 준비 중 이동 금지 검사, 달리기 예산·수류탄 감속 반영)
- **빌드 후 편집 가능한 데이터 파일**: `DataFiles`가 ① 빌드 폴더 `<게임>_Data/StreamingAssets/GameData/…` → ② 내장 원본(`Resources`) → ③ 코드 기본값 순서로 읽는다. 파일이 없거나, UTF-8이 아니거나(깨진 글자 포함), 해석에 실패하면 다음 순서로 넘어간다. 원본은 계속 `Assets/_Project/Resources`에서 편집하며, 빌드가 끝나면 `DataFilesBuildProcessor`가 `Localization`, `Credits`, `Data` 폴더와 안내문(README.txt)을 빌드 폴더로 복사한다. (Windows/Linux 빌드)
- **캐릭터 외형**: `CharacterSkin`(모델 + 애니메이터) 에셋을 `CharacterSkinView`가 아바타에 붙인다. 맵의 `MapInfo`에 마네킹/가드 외형을 지정하면 그 맵에서만 바뀌고(가짜 마네킹 포함), 비우면 프리팹 기본 외형(마네킹=더미, 가드=Ethan)을 쓴다. 애니메이션은 위치 변화로 속도를 계산해 `Speed`, `MoveX`, `MoveZ` 중 컨트롤러에 있는 파라미터만 설정한다. 몸체 렌더러는 `CharacterSkinView.BodyRenderers`로 변신 숨김·1인칭 그림자 처리에 쓰인다.
  - 외형·무기 모델은 메뉴 `GameSettingTools > 캐릭터·무기 에셋 생성`(테스트 환경 생성에 포함)이 만든다: 원본에서 스크립트·충돌체를 빼고, 발을 원점에 맞추고 키를 맞추고(사람 1.8m, 슬라임 1.3m, 유령 1.6m, 사냥 시간 Ghoul 2.4m), 머티리얼을 URP Lit으로 변환해 `Assets/_Project/Art/Skins`에 저장. 휴머노이드(더미·Ethan)는 Kevin Iglesias 8방향 걷기·달리기 2D 블렌드 컨트롤러를 공유한다.
  - 사냥 시간 외형 (2026-10-07): 사냥 시간이 되면 모든 PC에서 `MannequinHuntingMode`가 `CharacterSkinView.UseHuntSkin()`을 불러 마네킹 외형을 바꾼다. 맵 `MapInfo.mannequinHuntSkin`(모드 맵용)이 있으면 그것을, 없으면 Mannequin 프리팹의 `huntSkin`(= `Skin_Ghoul`)을 쓴다. 교체 시 이전 몸체의 표시 여부·그림자 설정을 이어받고, 1인칭 그림자 처리보다 먼저 실행된다. Ghoul(Generic)은 `GhoulSkinBuilder`가 임포트·머티리얼(`M_Ghoul`)·컨트롤러(`AC_Ghoul`: 기본·이동 `Hunch_Reach`, `Attack` 트리거 → `Attack_Swipe`)를 준비하고 '캐릭터·무기 에셋 생성'이 외형으로 만든다.
  - 새 외형 추가: `Create > GameSettingTools > Character Skin`으로 에셋을 만들고 모델 프리팹(발 원점, 정면 +Z)과 컨트롤러를 지정 → 맵의 `MapInfo`에 넣는다. 정면이 다르면 `yawOffset`으로 보정.
- **가드 총**: `GuardWeaponView`가 소유자에게는 카메라 앞 1인칭 리볼버, 다른 플레이어에게는 오른손 위치에서 조준 방향을 향하는 3인칭 리볼버를 보여준다(휴머노이드는 `HandAimIK`로 오른손을 조준 방향으로 뻗음). 판정은 그대로 서버 히트스캔이고, `ShotTracerView`가 보는 사람 기준 총구에서 총알 이펙트(`BulletEffect`)를 끝점까지 날리고 명중·총구 화염 이펙트를 재생한다. (MasterStylizedProjectiles의 OrangeGunShot)
- **맵 자동 생성** (`GameSettingTools > 맵 생성`, `MapCatalog`의 순서 = 맵 번호): 원본 데모 씬을 가져와 정리(프리팹 연결 해제, 카메라·UI·에셋 스크립트 제거, 물리 고정) → 머티리얼 URP 변환(`UrpMaterialConverter`, 원본 에셋 머티리얼을 직접 바꿈) → 충돌체 없는 메시에 MeshCollider → 크기·이름 규칙으로 변신 사물/벽 조각 지정(`TransformableProp`, 벽 조건이어도 정점 100개 초과·변신 가능 크기면 가구로 보고 그대로 복사) → 임시 NavMesh로 플레이 가능한 바닥(실내 맵은 천장 있는 곳, 가장 넓게 이어진 영역)을 분석해 마네킹 스폰 10·전원 장치 후보 12·가드 출발 지점 5를 고르게 배치 → 조명을 실시간 MapLight로 바꾸고 부족하면 추가 → 투명 경계·천장, 맵 아래 닫힌 대기실 → 게임 시스템·HUD → 빌드 설정과 RoomState 맵 목록 등록.
  - (Phase 7-A에서 대기실·출발 지점을 없애고 가드 스폰 후보 + `GuardSpawnSelector`로 바꿨다.)
  - 빌드 설정에 없는 맵을 고르면 `RoomState`가 테스트 맵으로 대신한다.
- **UI 프리팹과 맵 제작 도구**: 사용법은 `Docs/UiAndMapGuide.md`. UI는 `Prefabs/UI`의 프리팹(설정·크레딧 패널은 중첩 프리팹)으로 관리되어 씬 생성 도구를 다시 실행해도 직접 수정한 내용이 유지된다(`UiPrefabLibrary`). 맵은 "맵 제작 도우미" 창(`MapMakerWindow`)에서 새 맵 뼈대 생성 → 등록(`PropRegistrar` 공용) → 지점 배치 → 경계 → 검사(`MapValidator`) → 최적화 → 맵 목록 갱신(`MapRegistrySync`: 빌드 설정·RoomState·strings.csv·game_settings.csv) 순서로 만든다. 직접 만든 맵은 `Scenes/Custom`에 두면 자동 생성 맵 뒤 번호로 붙는다.
- **맵 크기**: 걸을 수 있는 바닥이 800㎡보다 좁은 맵은 생성 시 환경 전체를 키운다(최대 2.5배, 조명 범위도 함께). `MapCatalog`의 `scale`에 값을 넣으면 그 배율로 고정된다(0 = 자동).
- **전원 장치 배치**: 생성 시 후보 12개를 서로 최대한 멀리 배치하고, 매 판 `GeneratorSpawner`가 후보 중 서로 먼 것을 고른다(가장 먼 상위 3개 중 무작위). 편집은 맵 제작 도우미(`GameSettingTools > 맵 제작 > 맵 제작 도우미`)의 지점 배치 버튼(씬 뷰 중앙에 전원 장치 후보·마네킹 스폰·가드 출발 지점 추가, 전원 장치 후보 넓게 다시 배치)나 씬에서 직접 옮기기·복제로 한다. 후보는 씬 뷰에 빨간 상자와 이름표로 보인다.
- **캐릭터 애니메이션 규칙** (`CharacterAnimatorParameters`): 외형 컨트롤러에 `Speed`/`MoveX`/`MoveZ`(Float), `Attack`/`Shoot`/`Hit`(Trigger), `Emote1`~`Emote6`(Trigger) 또는 `Emote`(Trigger)+`EmoteId`(Int), `Interact`(Bool, 전원 장치 등과 상호작용하는 동안 켜짐)를 만들면 게임이 자동으로 사용한다. 없는 파라미터는 건너뛰며, 감정 표현 애니메이션이 없으면 아무 동작도 하지 않는다(`AnimatorEmoteView`). 임시 절차적 움직임(`ProceduralEmoteView`)은 2026-11-03 삭제했다(구울 외형에서 감정 표현 키를 누르면 점프·회전이 나오던 문제).
- **마네킹 감정 표현·상호작용 애니메이션** (2026-10-03): '캐릭터·무기 에셋 생성'(`MannequinAnimatorBuilder`)이 `Assets/_Project/Animation`의 Mixamo FBX를 휴머노이드로 임포트하고 마네킹 전용 컨트롤러 `Art/Skins/AC_MannequinHumanoid`를 만들어 더미 외형에 지정한다. 가드는 계속 `AC_HumanoidLocomotion`을 쓴다.
  - 번호: 1 Salute, 2 Thoughtful Head Shake, 3 Talking On Phone, 4 Situps, 5 Backflip(전신, Base Layer의 `Emotes` 하위 상태 머신 — 클립이 끝나거나 0.3m/s 넘게 움직이면 이동 애니메이션으로 복귀) / 6 Working On Device(상체 전용, `UpperBody` 레이어 + 상체 마스크 `AM_UpperBody`).
  - 전원 장치 상호작용: 서버가 `MannequinInteractor`의 동기화 값(`IsInteracting`, 대상이 참여자를 빼면 꺼짐)을 정하고, 모든 클라이언트의 `InteractAnimationView`가 `Interact`(Bool)로 옮겨 Working On Device를 상체로 반복 재생한다.
  - 클립은 제자리 재생(회전·높이는 포즈에 굽고 수평 이동은 분리)으로 맞춘다. 다른 클립을 쓰려면 `MannequinAnimatorBuilder.Emotes` 목록의 파일 이름을 바꾸고 메뉴를 다시 실행한다. (컨트롤러를 직접 수정하면 메뉴 실행 시 덮어써진다)
- **리볼버 위치**: 모두 매 프레임 적용되므로 Play 중에 맞춘다.
  - 1인칭 총(카메라 기준, 외형과 무관): Guard 프리팹의 `GuardWeaponView`. Play 중 맞춘 뒤 Copy Component → 프리팹에 Paste Component Values.
  - 3인칭 총·조준 IK 손 위치(외형마다 다름): 외형 에셋 `Skin_*.asset`의 **Weapon Grip**. 에셋이라 Play 중 바꾼 값이 그대로 저장되고, '캐릭터·무기 에셋 생성'을 다시 실행해도 유지된다. 외형이 없는 가드는 `GuardWeaponView`의 예비 값(fallbackGrip)을 쓴다.
- **총알 이펙트**: `ShotTracerView`의 총알·명중·총구 화염 밝기와 크기 배율(기본 0.35~0.4 / 0.4~0.6)로 조정한다. 파티클 시작 색의 RGB·알파와 시작 크기에 곱한다.
- **변신 사물 외곽선**: 마네킹 본인 화면에서 조준 중인 변신 가능 사물(변신 판정과 같은 조건)에 `GuardVsMannequin/PropOutline` 셰이더로 화면 기준 일정한 두께의 외곽선을 그린다(`PropHighlighter`). 색·두께는 `Art/Materials/M_PropOutline`에서 조정. 변신 사물은 외곽선이 원래 메시를 써야 하므로 정적 배칭에서 제외한다.
- **NavMesh 미리 굽기**: 빌드에서는 읽기가 허용되지 않은 메시(에셋 팩 모델 대부분)를 실행 중에 NavMesh로 구울 수 없다(로그: "does not allow read access ... not in player"). 그래서 맵 최적화가 NavMesh를 에디터에서 구워 `Scenes/<씬 이름>/NavMesh-Map.asset`으로 저장하고, `FakeMannequinSpawner`는 미리 구운 데이터가 있으면 다시 굽지 않는다. (테스트 맵은 기본 도형이라 실행 중에 굽는다)
- 맵 최적화는 추가로 영역(Area) 조명 제거(URP 실시간 미지원), 맵 조명 그림자 끄기(그림자 지도 부족 방지, 방향광·손전등 제외), 음수 크기 상자 충돌체를 메시 충돌체로 교체한다.
- `MovementValidator`의 이동 예산은 실제 경과 시간(`realtimeSinceStartup`)으로 쌓는다. `Time.deltaTime`은 프레임 멈춤 시 0.33초로 잘려, 서버가 잠깐 멈춘 뒤 도착한 정상 이동을 부정 이동으로 오판했다(사냥 시간 시작 시 확인). 몰아 쓰기 한도 3초. 서버 프레임 간격이 0.5초를 넘으면(에디터 일시정지·로딩) 그동안의 이동은 검사하지 않고 받아들인다.
- 외형 애니메이터의 애니메이션 이벤트는 기본으로 끈다(`CharacterSkin.fireAnimationEvents`). 에셋 팩 클립의 이벤트가 외형 생성 때 제거된 팩 스크립트를 불러 "has no receiver" 오류가 났다(슬라임 공격 `AlertObservers`).
- **맵 최적화** (맵 제작 도우미의 `현재 맵 최적화`, 맵 빌더 실행 시 자동 실행): 투명 경계 밖(실내 3m·실외 12m 여유)의 환경 오브젝트와 꺼진 오브젝트를 지우고, 남은 환경에 Occludee·정적 배칭 플래그(1m 이상 불투명 메시는 Occluder)를 준 뒤, 플레이 영역을 덮는 Occlusion Area로 오클루전 컬링을 굽는다. 수동으로 옮긴 스폰·전원 장치 배치는 건드리지 않는다.
- **빌드 최적화 설정** (`GameSettingTools > 빌드 최적화 설정 적용`): 셰이더 변형이 4천 개 넘게 나와 첫 빌드가 1시간 이상 걸리던 문제로, PC URP 에셋의 라이트 쿠키·라이트 레이어·LOD 크로스페이드·반사 프로브 블렌딩/박스 투영·부드러운 그림자를 끄고 캐스케이드를 2개로 줄이며, SSAO를 끄고, Windows 그래픽 API를 Direct3D11만 쓰게 한다. (손전등용 추가 조명 그림자는 유지. Mobile 품질 레벨은 원래 PC 빌드에서 제외되어 있음)
- 새로 추가된 외부 에셋 중 Unity 6에서 컴파일되지 않던 옛 스크립트(Standard Assets `Utility` 3개와 에디터 스크립트 2개, Flooded_Grounds PostProcessing 에디터 2개)는 제거된 API만 대체 API로 바꿨다. (파일마다 "GuardVsMannequin 프로젝트 수정" 주석)
- 인스펙터 툴팁·개발용 로그는 한국어로 유지한다. (플레이어에게 보이지 않음)
- HUD(`GameHud`)는 같은 표시 로직을 uGUI Text/패널로 옮겼다. UI는 모두 에디터 툴(`SharedUiBuilder`, `MenuSceneBuilder`)로 씬에 생성되어 인스펙터에서 편집할 수 있다.
- **UI 코드 분리** (2026-10-02): 화면 코드를 "화면 관리 + 영역별 컴포넌트"로 나눴다(`Scripts/UI/Lobby`, `Room`, `Hud`). 영역 컴포넌트는 모두 캔버스 오브젝트에 붙는다(화면 패널에 붙이면 꺼진 동안 실행되지 않아 초대 암호 요청 같은 이벤트를 놓치므로). 로비 메뉴는 `ILobbyNavigator`(= `LobbyView`)로만 화면을 바꾸고, 룸의 일시 안내는 `IRoomNotice`(= `RoomFooterView`, 2초 표시)로 띄운다. HUD 영역은 `IHudSection`을 구현하고 `GameHud`가 만든 `HudContext`(게임 상태·로컬 플레이어)를 받는다. 맵 씬이 덮어쓰는 게임 상태 연결 5개는 `GameHud`에 남겼다. 기존 프리팹의 연결은 YAML에서 새 컴포넌트로 옮겨 다시 연결할 필요가 없다.
- **SOLID 점검 후 정리** (2026-10-02)
  - 로비 화면 목록은 `LobbyView`의 인스펙터 목록(화면, 패널, 부모 화면)으로 정한다. `ILobbyNavigator`(이동)와 `ILobbyOverlays`(설정·크레딧)를 나눴다. 메뉴 버튼 조건은 `SessionButtonRules`, 부모 쪽 의존성 찾기는 `UiLookup`(없으면 오류 로그), 메뉴 화면 커서 풀기는 `CursorUnlockOnStart`.
  - HUD는 게임 상태를 읽기 전용 `IGameStateProvider`로, 룸 복귀를 `IMatchControl`(`GameStateMachine` 구현)로만 쓴다. 로컬 플레이어 추적은 `LocalPlayerTracker`.
  - `UiPrefabLibrary`(프리팹 가져오기·만들기) / `UiPrefabDefaults`(기준본 저장·복원) / `UiPrefabMenus`(메뉴)로 나눴다.
  - **역할 정의**: 역할별 값(이름·안내 문구 key, 승리 진영, 사냥 시간 속도 배율, 설정에서 체력·속도·피해량 고르기)은 `RoleDefinitions`/`RoleStats` 한곳에 있다. `PlayerSpawner`의 아바타 프리팹은 역할별 목록(`rolePrefabs`), 맵 외형은 `MapInfo.GetSkin(role)`. Phase 7의 맵별 스탯은 `RoleStats`를 고르는 곳에 맵 값을 덧씌우면 된다.
  - 설정 기본값은 `GameServices.CurrentSettingsOrDefault`(없으면 `DefaultGameSettings.Cached`) 한곳에서 읽는다. 코드 곳곳의 `?? 2f`, `?? 10f` 같은 기본값을 없앴다.
  - 도구 공통: 쿨타임 `ToolCooldown`(로컬/서버, 허용 오차 0.15초), 조준 원점 검증 `AimRaycaster.SanitizeOrigin`.
  - `MannequinTransformer`는 입력·서버 검증·동기화만 하고, 조준한 사물 찾기는 `PropTargetFinder`, 번호로 사물 찾기는 `PropRegistry`, 외형 복사·적용은 `PropAppearance`/`PropDisguiseView`가 맡는다. (2026-10-05 교체 전에는 `IPropDatabaseSource`)
  - `CharacterSkinView`는 외형 적용만 하고, 이동 애니메이션 값은 `LocomotionAnimatorDriver`, 파라미터 확인은 `AnimatorParameterSet`이 맡는다.
  - 승패 판정은 순수 함수 `MatchJudge`(값만 받아 `MatchVerdict` 반환)이고, `MatchRules`가 결과를 기록한다.
  - `FakeMannequinSpawner`는 씬 전체 검색 대신 `SpawnPointRegistry`를 쓴다. 게임 시작 실패 문구는 `strings.csv` key로 바꿨다.

---

### Phase 7 — 게임플레이 개편 + 모드 지원 (계획)

Phase 7은 **A. 게임플레이 개편**(7-A1~7-A5)을 먼저 하고 **B. 모드 지원**(7-1~7-7)을 이어서 한다. A에서 `GameSettings` 항목(가드 대기 시간 삭제, 탐지기 → 수류탄)이 바뀌므로, B의 `map.json` 설정·스탯 항목은 A가 끝난 형식을 기준으로 정한다.

#### A. 게임플레이 개편 (2026-10-03 기획)

**목표:** 가드 대기 시간을 없애 판 시작을 빠르게 하고, 가드 도구를 "탐지(소리)"에서 "수류탄(외곽선 표시 + 감속)"으로 바꾼다. 변신 사물 조준 외곽선이 보이지 않는 문제를 고치면서 외곽선 표시를 공용 기능으로 만든다.

**A-1. 가드 대기 시간 제거 + 가드 무작위 스폰**
- 현재: `Preparation` 상태(가드 대기 시간 15~180초, 기본 20초) 동안 가드가 대기실(`WaitingRoomArea`, `WaitingRoomDoor`)에 갇히고, 탐색 단계가 시작되면 `GuardReleaseTeleporter`가 `GuardReleasePoint`로 옮긴다.
- 변경: 가드도 처음부터 맵 안의 **무작위 위치**에 스폰한다. 대기실·문·대기실 이동 검증·순간이동을 없앤다.
- 상태 흐름은 유지한다: `GameState.Preparation`을 지우면 네트워크 형식(enum 값)과 여러 구독자(`PlayerSpawner`, `FakeMannequinSpawner`, `GeneratorSpawner`, `MatchResult`, `MannequinTransformer`, HUD)를 함께 고쳐야 한다. 그래서 **`Preparation`은 "판 시작 준비" 단계로 남기고, 시간을 호스트 설정이 아닌 고정값 `GameConstants.MatchStartDelay`(권장 3초, 시작 카운트다운 표시)로 바꾼다.** 스폰·역할 배정·가짜 마네킹·전원 장치 배치는 지금처럼 이 단계에 진입할 때 한다. 이 3초 동안은 모두 이동할 수 없다(로딩이 늦은 플레이어와 시작을 맞춤).
- 가드 스폰 위치 규칙 (`GuardSpawnSelector`, 서버):
  1. 후보 = 맵의 가드 스폰 후보 지점(지금의 `GuardReleasePoint` 5개를 `SpawnPoint(Guard)`로 바꿔 맵 안에 둠) + 미리 구운 NavMesh 위 무작위 지점(`NavMesh.SamplePosition`).
  2. 모든 마네킹 스폰 지점에서 **최소 거리**(권장 15m, `GameConstants.GuardSpawnMinDistance`) 이상 떨어진 후보만 쓴다. 맵이 작아 조건을 만족하는 후보가 없으면 가장 먼 후보를 쓴다.
  3. 가드가 여러 명이면 서로 일정 거리(권장 8m) 이상 떨어지게 고른다.
  4. 시점은 맵 중심 쪽을 향하게 한다.
- **검토: 마네킹이 숨을 시간이 없어짐.** 지금은 대기 시간 20초 동안 마네킹이 자리를 잡는다. 대기 시간이 없으면 시작하자마자 가드가 돌아다닌다. 보완책으로 **"가드 출발 보호" — 시작 후 `GameConstants.GuardToolLockTime`(권장 10초) 동안 가드는 이동만 할 수 있고 총·수류탄은 쓸 수 없다**(`DamageRules.CanUseGuardTools`에 경과 시간 조건 추가, HUD에 남은 시간 표시). 이동이 가능하므로 가드는 그동안 맵을 둘러볼 수 있다. 밸런스 테스트 후 값을 조정한다.
- 목표 0회 판의 사냥 시간 진입 시점(`ZeroObjectiveHuntDelay` 20초)은 지금도 탐색 단계 시작부터 센다. 대기 시간이 없어지면 실제 판 시작 후 약 23초에 사냥 시간이 된다. 가드 출발 보호 10초를 빼면 가드가 공격할 수 있는 시간이 13초뿐이다. **→ 30~40초로 늘리고 룸 설정 항목으로 바꾸는 것을 검토**한다.
- 지우거나 바꿀 것

  | 대상 | 처리 |
  |---|---|
  | `GameSettings.preparationTime`, `GameSettingId.PreparationTime`, `game_settings.csv`·`default_game_settings.json`·`strings.csv`의 해당 줄, `GameSettingsValidator.Min/MaxPreparationTime` | 삭제 |
  | `PreparationStateHandler` | 설정 대신 `MatchStartDelay` 사용 |
  | `WaitingRoomArea`, `WaitingRoomDoor`, `GuardReleaseTeleporter`, `GuardReleasePoint` | 삭제(`GuardReleasePoint` → 가드 `SpawnPoint`로 교체) |
  | `MovementValidator`의 대기실 검사 | 삭제. 시작 준비 3초 동안 이동 금지는 클라이언트 입력 차단 + 서버 검증으로 처리 |
  | `MapSceneBuilder`(대기실 생성, `GuardReleaseCount`, `WaitingRoomSpawnCount`), `MapEditTools`, `MapMakerWindow`, `MapValidator`, `TestEnvironmentSetupTool` | 대기실 생성·검사 대신 가드 스폰 후보 배치·검사 |
  | 기존 맵 씬 5개 | 메뉴 "맵 편집/가드 대기실 제거 (전체 맵)"로 대기실·문·출발 지점을 지우고 가드 스폰 지점으로 바꾸는 이전 도구를 만든다 |
  | `MannequinTransformer`의 대기 시간 중 해제 쿨타임 0초 | 시작 준비 단계에는 변신 불가로 변경 |
  | HUD `hud.phase.preparation` | "시작 준비 3, 2, 1" 카운트다운 |

**A-2. 탐지기 제거 → 수류탄 추가**
- 현재 탐지기(`DetectorTool`, 우클릭): 반경 10m 안의 실제 마네킹이 소리(`MannequinDetected`)를 낸다. 쿨타임은 `detectorCooldown`.
- 변경: 같은 우클릭 칸에 **수류탄(`GrenadeTool`)**을 넣는다. 던지면 포물선으로 날아가 **닿는 순간 터지고**(가드 조작을 단순하게 하기 위해 시한 신관은 쓰지 않음), **반경 2m** 안의 **실제 마네킹**(변신 여부 무관)에게 아래 효과를 준다. 가짜 마네킹과 가드는 영향을 받지 않는다. 피해는 없다.
  - **외곽선 표시**: 맞은 마네킹(변신 중이면 변신한 사물)에 밝은 외곽선을 `GrenadeRevealDuration`(권장 4초) 동안 그린다. **가드에게만** 보이고 **벽 너머로도 보인다**. 맞은 마네킹 본인에게는 화면 가장자리 효과와 안내 문구로 알린다.
  - **감속**: 이동 속도를 `GrenadeSlowMultiplier`(권장 0.5배)로 `GrenadeSlowDuration`(권장 3초) 동안 낮춘다. 사냥 시간 1.5배와 곱해서 적용한다.
- 판정 (서버 권한):
  1. 클라이언트는 `ServerRpc(조준 원점, 방향)`만 보낸다. 원점은 `AimRaycaster.SanitizeOrigin`으로 검증한다. 쿨타임은 `ToolCooldown`을 쓴다.
  2. 서버가 고정 초기 속도·중력으로 궤적을 **작은 간격으로 SphereCast**해 충돌 지점을 정한다(최대 비행 시간 3초, 넘으면 그 위치에서 폭발). 궤적이 결정적이라 수류탄을 NetworkObject로 만들지 않아도 된다.
  3. 서버가 `ObserversRpc(시작점, 초기 속도, 폭발 지점, 비행 시간)`를 보내면 모든 클라이언트가 같은 궤적으로 수류탄 모델을 날리고 폭발 효과를 재생한다. 효과는 서버가 비행 시간 뒤에 적용한다.
  4. 폭발 지점 기준 `OverlapSphere(2m)`로 `PlayerIdentity(Mannequin)`을 찾는다(탐지기의 `FindMannequinsInRange`를 재사용). **벽 너머 판정은 막는다**: 폭발 지점에서 대상까지 환경(변신 사물·플레이어 제외)에 막히면 제외한다.
- 상태 동기화: 마네킹에 `StatusEffects` 컴포넌트를 만들고 서버가 `SyncVar`로 "외곽선 표시 끝 시각", "감속 끝 시각"(서버 Tick 기준)을 기록한다. 다시 맞으면 끝 시각을 갱신한다(중첩 없음).
  - 감속: 마네킹 이동은 소유자 권한이므로 소유자의 `MoveSpeedController`가 배율을 적용한다. 부정 이용(감속 무시)을 막기 위해 서버의 `MovementValidator`도 감속 중에는 최대 속도를 낮춘 값으로 검사한다(감속이 끝나는 순간의 지연을 고려해 0.3초 여유).
  - 외곽선: 가드 클라이언트의 `RevealOutlineView`가 `StatusEffects`를 보고 공용 외곽선 기능(A-3)으로 그린다.
- 사용 가능한 단계: 총과 같이 탐색 단계·사냥 시간(`DamageRules.CanUseGuardTools`). 사냥 시간에는 가드가 불리하므로(사냥 시간 1:1 교전 시 가드 패배), 감속이 도주·반격 수단이 된다.
- 설정 변경: `detectorCooldown` → `grenadeCooldown`(룸 설정, 기본 15초, 범위 5~60초). 반경 2m·효과 시간·감속 배율은 `GameConstants`의 고정값으로 시작하고, 밸런스가 정해지면 필요한 것만 룸 설정으로 올린다. (Phase 7-B의 맵별 스탯 항목에도 `grenadeCooldown`을 넣는다.)
  - 주의: `JsonUtility`는 없는 키를 0으로 남긴다. 빌드 폴더의 수정된 `default_game_settings.json`에 `grenadeCooldown`이 없으면 쿨타임 0이 된다. `DefaultGameSettings`가 **값이 0 이하인 항목은 코드 기본값으로 채우도록** 한다.
- 지우거나 바꿀 것: `DetectorTool`, `DetectorPulseView`, `GameConstants.DetectorRadius`, `NoiseType.DetectorPing`·`MannequinDetected`(번호 1·2는 다시 쓰지 않음 — `GrenadeExplosion`은 새 번호), `tool.detector` 문구, Guard 프리팹의 도구 목록·HUD 쿨타임 아이콘, 4.4/4.5 체크리스트의 "탐지기" 항목 → "수류탄 모델, 폭발 이펙트, 던지는 소리, 폭발음".
- 3인칭에서 수류탄을 던지는 애니메이션은 애니메이션 규칙(`CharacterAnimatorParameters`)에 `Throw`(Trigger)를 추가해 있으면 쓰고, 없으면 건너뛴다.

**A-3. 변신 사물 조준 외곽선이 보이지 않는 문제 + 공용 외곽선 기능**
- 증상: 마네킹이 변신 가능한 사물을 조준해도 외곽선(`PropHighlighter`)이 보이지 않는다.
- 확인한 것: `Mannequin.prefab`에 `PropHighlighter`와 `M_PropOutline`이 연결되어 있다. 변신 사물은 정적 배칭에서 빠져 있다(`PropRegistrar`, `MapOptimizer`). 대상 찾기는 변신과 같은 `PropTargetFinder`를 쓴다(변신이 되면 대상 찾기는 정상).
- 원인 후보 (가능성 높은 순서. 7-A1 시작 시 Frame Debugger로 확인)
  1. **Depth Priming**: `PC_Renderer`는 Forward+ + Depth Priming **Auto**이고 `PC_RPAsset`은 Depth Texture를 요구한다. 깊이 사전 패스가 켜지면 불투명 물체는 사전 패스가 기록한 깊이와 **같을 때만**(ZTest Equal) 그려진다. `PropOutline` 셰이더에는 `DepthOnly` 패스가 없어 사전 패스에 기록되지 않으므로 외곽선이 전혀 그려지지 않는다. 확인 방법: `PC_Renderer`의 Depth Priming Mode를 Disabled로 바꿔 보이는지 본다.
  2. **두께와 각진 메시**: 외곽선은 메시를 법선 방향으로 1.5픽셀 부풀린 뒷면이다. 상자처럼 면마다 법선이 나뉜 메시는 모서리가 끊기고, 1.5픽셀은 3인칭 거리에서 거의 보이지 않는다.
  3. **메시가 자식에 있는 사물**: `PropHighlighter`는 `TransformableProp`과 같은 오브젝트의 `MeshFilter`만 쓴다. (현재 등록 도구는 렌더러 오브젝트에 붙이므로 해당 없을 가능성이 높음)
- 해결: 셰이더를 고치는 대신 **URP Renderer Feature 기반 공용 외곽선**으로 교체한다. 수류탄의 외곽선 표시(스킨 메시 몸체 + 변신 사물, 벽 너머)도 같은 기능을 쓴다.
  - `OutlineRendererFeature`(PC_Renderer에 추가): `RenderingLayerMask`의 "Outline" 레이어가 켜진 렌더러를 ① 스텐실에 기록하고 ② 화면 공간에서 스텐실 가장자리를 넓혀 외곽선을 그린다. 메시 법선과 무관하게 일정한 두께로 그려지고, `SkinnedMeshRenderer`에도 그대로 적용된다. Depth Priming의 영향도 받지 않는다(불투명 패스 이후 별도 패스).
  - 외곽선 종류 2가지: **조준 강조**(노란색, 가려지면 안 보임) / **수류탄 표시**(빨간색, 벽 너머 보임 — 스텐실 기록 시 ZTest Always). 종류별로 Rendering Layer를 나눈다.
  - `IOutlineService`(`Show(Renderer[] 대상, OutlineKind)` / `Hide`): 렌더러의 `renderingLayerMask` 비트만 켜고 끈다. 사본 오브젝트를 만들지 않는다. `PropHighlighter`는 조준 대상만 정하고 표시는 이 서비스에 맡긴다. `RevealOutlineView`는 마네킹 몸체(`CharacterSkinView.BodyRenderers`)나 변신 외형(`PropDisguiseView`)의 렌더러를 넘긴다.
  - GPU Resident Drawer(PC_RPAsset에서 켜져 있음)와 `renderingLayerMask` 변경이 함께 동작하는지 확인한다.
  - 지금의 `PropOutline` 셰이더·`M_PropOutline`은 교체 후 삭제한다. 색·두께는 Renderer Feature 설정에서 조정한다.

**A-4. 판 소요 시간 영향 (참고)**
- 가드 대기 시간 20초 → 시작 준비 3초로 줄어 판마다 약 17초 줄어든다. 목표 0회 판은 사냥 시간 진입 시점을 늘리면(A-1 검토) 그만큼 다시 늘어난다.
- 탐지기(10m 소리)보다 수류탄(2m, 위치 표시)은 범위가 좁고 확실하다. 마네킹을 찾는 시간에 주는 영향은 테스트로 확인한다.

**구현 결과 (2026-10-04, Unity 확인 전)** — 계획과 달라진 점
- 고정 수치는 `GameConstants` 대신 `GameTuning`(`game_tuning.csv`)에 둔다: `MatchStartDelay` 3초, `GuardSpawnMinDistance` 15m, `GuardSpawnSeparation` 8m, `GuardToolLockTime` 10초, `GrenadeRadius` 2m, `GrenadeRevealDuration` 4초, `GrenadeSlowDuration` 3초, `GrenadeSlowMultiplier` 0.5, `GrenadeThrowSpeed` 14, `GrenadeMaxFlightTime` 3초.
- 목표 0회 판의 보호 시간은 룸 설정 `huntDelay`(기본 35초, 10~120초)로 승격. `GameSettingId` 1번(예전 PreparationTime)·9번(예전 DetectorCooldown)은 번호를 유지한 채 이름만 `HuntDelay`·`GrenadeCooldown`으로 바꿔 룸 UI 줄을 재사용한다.
- 외곽선은 Rendering Layer 대신 `OutlineRegistry`에 등록된 렌더러를 `DrawRenderer`로 직접 마스크에 그린다. 오클루전 컬링으로 가려진 대상도 그려지고 렌더러 설정을 바꾸지 않는다. (GPU Resident Drawer 영향 없음)
- 수류탄 외곽선은 "상대 진영의 살아 있는 플레이어"에게만 보인다(`RoleDefinition.Side` 비교). 맞은 마네킹에게는 HUD 하단 문구로 알린다. (화면 가장자리 효과는 보류)
- 출발 보호 남은 시간은 HUD 도구 쿨타임 칸에 표시한다(총·수류탄의 `CooldownRemaining`).
- 가드 스폰 보강(2026-10-05): 후보(가드 스폰 지점 + NavMesh 무작위 60개)를 `SpawnCandidateFilter`로 먼저 거른다 — 마네킹 스폰 지점까지 NavMesh 경로가 이어진 곳(투명 천장·지붕 위, 경계 밖, 막힌 방 제외), 서 있을 공간이 있는 곳만. `GuardSpawnSelector`는 거리 조건에 더해 **마네킹 스폰 지점이 직접 보이지 않는 곳**(`GuardSpawnSightDistance` 40m 안, 벽·사물이 가리면 안 보임)을 고른다. 조건을 다 만족하는 곳이 없으면 안 보이는 곳 → 먼 곳 순으로 고른다. 최소 거리 기본값 15 → 20m(가짜 마네킹 흩뿌리기 4m 포함).
- **변신 외형 복사(2026-10-05)**: 예전에는 맵 생성 때 저장한 사물 목록(`PropDatabase_<맵>`)의 번호를 동기화해, 맵을 고친 뒤에는 크기·머티리얼이 어긋나고 벽 조건에 걸린 가구(책장·옷장)가 상자로 변신했다. 이제는 다음과 같이 동작한다.
  - 번호: `TransformableProp`마다 씬 안 번호를 매긴다(`PropIndexer`: 씬 저장 때 + 빌드·플레이 모드 씬 처리 때, 계층 순서라 결정적). 서버는 번호만 동기화한다(`MannequinTransformer.currentPropIndex`). **Protocol p4 → p5.**
  - 외형: 각 PC가 번호로 사물을 찾아(`PropRegistry`) 메시·머티리얼·실제 크기·기울기를 자식 조각까지 그대로 복사한다(`PropAppearance`, 하위 LOD·다른 변신 사물의 자식 제외). 좌우 방향은 마네킹이 바라보는 방향을 따른다.
  - 벽: (2026-11-09 변경) 벽은 변신 대상이 아니다. `TransformableProp.isWall`이 켜진 예전 등록은 `CanDisguiseAs`가 거짓이라 변신·섞기·튕겨나가기에서 빠지고, 벽 조각 외형·'벽으로 등록' 메뉴는 삭제했다.
  - 제한: 가장 긴 변이 `DisguiseMaxSize`(3m)를 넘으면 변신 불가. 서버는 사물 경계까지의 거리도 검증한다(변신 사거리 + 1.5m).
  - 도구: 맵 제작 도우미의 "변신 사물로 등록"은 **선택한 오브젝트(부모) 단위**로 자식 조각까지 합친다. 맵 검사는 충돌체·메시 없음(오류), 너무 큼·파티클/조명/스킨 메시 포함·정적 배칭(경고)을 알린다(`PropCopyChecks`). 맵 최적화는 변신 사물의 자식 조각도 정적 배칭에서 뺀다.
  - 이전(완료, 2026-10-05): 일회용 메뉴로 기존 맵 5개의 벽 조각 여부를 옮기고(가구로 보이는 것은 복사로 전환) 번호를 매긴 뒤, 이전 도구와 `PropDatabase`·`PropDefinition`·에셋 `ScriptableObjects/PropDatabase_*`를 삭제했다.
- 사냥 시간 밝기(2026-10-05): 맵 환경광은 `HuntingLightingController`의 색조 × `game_tuning.csv`의 `HuntAmbientBrightness`, 손전등 밝기는 `FlashlightIntensity`.
- 기존 에셋 이전(완료, 2026-10-04): 일회용 메뉴로 외곽선 기능 설치(PC·Mobile 렌더러), 탐지기 → 수류탄, 마네킹 효과 컴포넌트, 맵 5개 대기실 제거, 룸 설정 줄 이름을 바꾼 뒤, 이전 도구와 예전 대기실·탐지기 스크립트, `PropOutline` 셰이더를 삭제했다.

**구현 단계**
- [x] 7-A1. 공용 외곽선(`OutlineRendererFeature`, `IOutlineService`)으로 `PropHighlighter` 교체 → 변신 사물 조준 외곽선이 보이는지 확인. (원인 확인을 위해 먼저 Depth Priming 끄기로 시험)
- [x] 7-A2. 가드 대기 시간 제거: `preparationTime` 삭제, `Preparation` = 고정 3초 시작 준비, 대기실 관련 코드·맵 오브젝트 정리, 기존 맵 이전 메뉴.
- [x] 7-A3. 가드 무작위 스폰(`GuardSpawnSelector`) + 가드 출발 보호(도구 잠금).
- [x] 7-A4. 수류탄: `GrenadeTool`(서버 궤적 판정, 관찰자 연출), `StatusEffects`(외곽선 표시·감속 동기화), `MoveSpeedController`·`MovementValidator` 감속 반영, `RevealOutlineView`, 설정 `grenadeCooldown`, 탐지기 삭제.
- [ ] 7-A5. 문구·HUD·에셋(수류탄 모델·이펙트·사운드) 정리, `AssetLicenses.md` 기록, 밸런스 테스트(출발 보호 시간, 수류탄 쿨타임, 목표 0회 사냥 진입 시점). → `GameSettings` 구조와 RPC가 바뀌므로 **`GameVersion.Protocol` +1**, Player Settings Version은 마이너 버전 올림.

**검토가 필요한 위험**
- `GameSettings` 필드 삭제·이름 변경으로 이전 버전 설정 파일(빌드 폴더의 `default_game_settings.json`)과 호환되지 않는다. 남은 `preparationTime`·`detectorCooldown` 키는 무시되고 없는 키는 코드 기본값을 쓰도록 한다.
- 가드가 마네킹과 같은 시간에 스폰되므로, 가짜 마네킹 생성 위치(마네킹 스폰 지점 주변)와 가드 스폰 위치가 가까우면 시작하자마자 진짜·가짜가 구분될 수 있다. 가드 스폰 최소 거리는 가짜 마네킹 흩뿌리기 반경(4m)을 포함해 계산한다.
- 벽 너머 외곽선은 정보량이 크다. 표시 시간(4초)과 쿨타임(15초)을 테스트로 맞춘다.

#### B. 모드 지원

**목표:** 플레이어가 만든 캐릭터 외형·맵·변신 사물·맵별 스탯을 게임 수정 없이 추가하고, 같은 모드를 가진 플레이어끼리 함께 플레이한다.

**확정한 방향 (2026-10-02)**
- 형식: **Unity 모드 SDK** — 제작자는 같은 Unity 버전(6000.4.2f1) + URP에서 SDK 패키지로 작업하고, 도구가 **AssetBundle + `mod.json`**을 내보낸다. 모드에는 **코드를 넣을 수 없다**(AssetBundle은 스크립트를 담지 못함 — 보안상 장점). 모드 안의 컴포넌트는 게임에 이미 있는 스크립트(`MapInfo`, `SpawnPoint` 등)만 쓸 수 있다.
- 동기화: (2026-11-04 변경) **입장은 자유, 시작할 때 고른 맵의 모드만 확인** — 아래 "모드 확인 방식 변경" 참고. 배포는 수동(폴더 복사), Steam 창작마당은 정식 AppID 이후.
- 스탯: **맵별 스탯** — 맵 모드가 그 맵의 캐릭터 스탯 기본값과 최소·최대 범위를 정한다. 호스트가 맵을 고르면 룸 설정의 기본값·범위가 그 맵 값으로 바뀐다.

**추가 결정 (2026-10-05)** — 7-B 착수 전 점검에서 바꾼 것
- `GuardVsMannequin.Runtime`은 FishNet과 **ECM2(유료 에셋)**를 참조하므로 SDK에 DLL로 넣을 수 없다(재배포 라이선스). → 모드에 들어가는 컴포넌트만 **`GuardVsMannequin.Content`**(FishNet·ECM2 무참조) 어셈블리로 분리하고, SDK에는 이 어셈블리의 소스만 넣는다.
- 맵 씬에 게임 시스템(씬 네트워크 오브젝트)이 있으면 모드 제작자 PC의 FishNet이 SceneId를 정하게 되어 위험하다. → 게임 시스템·HUD·씬 카메라·사냥 시간 조명 제어를 공용 **`Game_Systems`** 씬으로 옮기고, 판을 시작하면 "맵 씬 + Game_Systems"를 함께 불러온다(맵 씬이 활성 씬). 맵 씬에는 네트워크 오브젝트가 없다.
- 모드 폴더: 실행 파일 옆 `Mods/` + 사용자 폴더 `persistentDataPath/Mods/` 둘 다 검색한다.

**모드 구조 (`Mods/<모드 ID>/`)**

| 파일 | 내용 |
|---|---|
| `mod.json` | `ModManifest`: `id`, `name`, `version`, `author`, `description`, `gameVersion`(최소 게임 버전), `unityVersion`, `dependencies`, `assetBundle`·`sceneBundle`(파일 이름), `contentHash`(번들 지문), `maps`(맵 목록 — 맵마다 `id`·`scene`·`nameKey`·`name`·`minPlayers`·`maxPlayers`·`settings`[{id, value, min, max}]) |
| `<id>.assets.bundle` (선택) | 공유 에셋 번들 (외형·사물 프리팹 등, 다른 모드가 의존성으로 쓸 수 있음) |
| `<id>.scenes.bundle` (맵 모드) | 맵 씬 번들 (씬이 쓰는 모델·머티리얼은 여기 함께 들어감) |
| `strings.csv` (선택) | 맵 이름 번역. 열 형식은 게임의 `strings.csv`와 같고, key는 `mod.<id>.` 접두사만 받는다 |

(2026-10-05 구현 시 바뀐 점: 맵 정보는 별도 `map.json` 대신 `mod.json`의 `maps`에 적는다. 내장 맵은 `Resources/Data/maps.json`(같은 `MapManifest` 형식)에 적는다. 씬 번들과 에셋 번들은 Unity 규칙상 나눠야 한다.)

**지원 항목별 설계**
1. **캐릭터 외형·애니메이션**: 번들 안의 `CharacterSkin` 에셋(모델 + 애니메이터 컨트롤러 + 무기 손잡이 값)을 그대로 쓴다. 애니메이션 파라미터 규칙(`CharacterAnimatorParameters`)이 이미 "있는 것만 쓰는" 방식이라 모드 컨트롤러에 그대로 적용된다. SDK에 휴머노이드 8방향 이동 컨트롤러 템플릿과 외형 검사 도구(발 원점·키·정면·파라미터)를 넣는다. 외형은 지금처럼 맵의 `MapInfo`가 고르며, 다른 모드의 외형을 ID로 참조할 수 있다(`mod.json` 의존성).
2. **맵 설정**: 맵 씬을 번들에 넣고, SDK의 "맵 제작 도우미"(`MapMakerWindow`, `MapValidator`)를 그대로 쓴다. `map.json`에 플레이 인원(최소·최대), 가드 수·전원 장치 수·목표 횟수·시간의 기본값과 최소·최대를 적는다. 게임의 `game_settings.csv` 범위와 겹치는 구간만 허용한다(전원 장치 수 최대 ≤ 맵의 후보 수는 검사 도구가 확인).
3. **변신 가능 물체** (2026-10-05 변경): 맵 제작자는 변신시킬 물체(여러 조각이면 부모)에 `TransformableProp`만 붙인다. 외형은 게임 중에 그 물체에서 그대로 복사하고, 번호는 "모드 내보내기"(씬 저장·빌드 처리와 같은 `PropIndexer`)가 매긴다. 같은 모드만 입장하므로 모든 PC의 번호가 같다. 사물 팩 모드(`type: props`)는 `TransformableProp`이 붙은 **프리팹 묶음**이고, 맵 모드가 그 프리팹을 자기 씬에 배치한다. 검사 도구(`PropCopyChecks`)가 너무 큰 물체, 복사되지 않는 요소(파티클·조명·스킨 메시), 충돌체 없음을 알린다.
4. **캐릭터 스탯 (맵별)**: `map.json`의 `stats`에 역할별 체력·이동 속도·피해량·공격 범위·도구 쿨타임의 기본값과 최소·최대를 적는다. 없는 항목은 게임 기본값(`default_game_settings.json`, `game_settings.csv`)을 쓴다. 서버의 `GameSettingsValidator`가 "게임 범위 ∩ 맵 범위"로 보정한다.

**구현 단계** (2026-10-05 수정. 진행: 단계마다 컴파일 확인 후 로컬 커밋, B0·B1 뒤 Unity 확인 1회, 나머지는 끝까지 진행 후 확인 체크리스트)
- [x] B0. 콘텐츠 어셈블리 분리: `MapInfo`, `SpawnPoint`, `GeneratorSpawnPoint`, `MapLight`, `TransformableProp`, `PropAppearance`, `PropRegistry`, `CharacterSkin`, `PlayerRole`, `GameConstants`, `GameTuning`, `DataFiles`, `CsvParser` → `Assets/_Project/Content` (`GuardVsMannequin.Content`, 참조 없음). .meta GUID를 유지해 씬·프리팹 연결은 그대로다.
- [x] B1. 게임 시스템 씬 분리: `GameSystemsSceneBuilder`가 `Scenes/Game_Systems.unity`를 만든다. `RoomState`가 맵 씬과 함께 불러오고 맵 씬을 활성 씬으로 정한다. 시스템 쪽은 맵 오브젝트를 늦게 찾는다(`SpawnPointRegistry`, `HuntingLightingController`, `FakeMannequinSpawner`의 NavMesh). 씬 카메라의 대기 화면 위치는 `MapInfo.overviewPoint`. 기존 맵은 메뉴 "Phase 7/게임 시스템 씬 분리 (전체 맵)"(`GameSystemsSceneMigration`, 일회용)로 이전한다.
- [x] B2. 콘텐츠 등록소 (7-1): `IContentSource`(내장 / 모드) → `ContentRegistry`가 맵 목록을 문자열 ID로 관리한다. 내장 맵도 `map.json`(Resources/Data/Maps) 형식으로 등록한다.
- [x] B3. 맵 번호 → 등록소 목록 (7-2): `RoomState.gameSceneNames`와 `GameSettingId.Map` 범위·이름 표시를 등록소의 정렬된 목록으로 바꾼다. 같은 모드 세트가 보장되므로 네트워크에서는 목록 번호를 쓴다.
- [x] B4. 맵별 설정·스탯 범위 (7-3): `map.json`의 기본값·범위 → 맵을 바꾸면 서버가 기본값을 적용하고 "게임 범위 ∩ 맵 범위"로 보정, 룸 UI 표시 범위도 갱신.
- [x] B5. 모드 로더 (7-4): 두 `Mods` 폴더 검색, `mod.json` 검사(형식·게임 버전·Unity 버전·의존성), 외형 번들은 시작 때, 맵 번들은 FishNet 씬 처리기 확장으로 씬을 불러오기 직전에 로드하고 판이 끝나면 해제. 실패한 모드는 건너뛰고 로비에 안내.
- [x] B6. 모드 일치 확인 (7-5): 켜진 모드 목록 해시를 접속 요청·Steam 로비 데이터에 넣고, 불일치 시 필요한 모드 목록과 함께 거부. → `GameVersion.Protocol` +1
  - 2026-10-20 변경: 해시가 다르면 서버가 `ModSetRequiredBroadcast`로 호스트 모드 구성을 보내고, 접속자는 `ModService.TryMatchHost`로 이번 접속 동안만 그 구성으로 켜고 끈 뒤 참가 요청을 다시 보낸다(Steam 방은 로비 데이터로 입장 전에 맞춤). 호스트가 켜지 않은 모드를 켠 접속자도 들어갈 수 있고, 호스트 모드가 없거나 다를 때만 거부. 방을 나가면 `ClearSessionMods`로 원래 설정 복귀. → `GameVersion.Protocol` +1
- [x] B7. 모드 SDK (7-6): 콘텐츠 어셈블리 소스 + 맵 제작 도우미·검사 도구 + "모드 내보내기"(번들 빌드 + `mod.json`)를 담은 `.unitypackage` 생성 메뉴. 문서 `Docs/ModdingGuide.md`.
- [x] B8. 로비 "모드" 화면 (7-7): 설치된 모드 목록, 켜기/끄기, 오류 표시.
- [ ] B9. 예제 모드로 전체 검증: 내장 맵 하나를 모드로 내보내 2PC에서 플레이.

**모드 확인 방식 변경 (2026-11-04)**
- 이유: 입장 때 전체 모드를 비교하면 호스트가 켜 둔 모드 때문에 내장 맵을 할 때도 입장이 막히고, 공개방에서 사람이 모드 구성별로 갈린다.
- 입장: `RoomAuthenticator`와 Steam 로비는 모드를 확인하지 않는다.
- 맵 정보: `GameSettings.mapId`는 호스트 목록의 번호일 뿐이므로 게스트는 쓰지 않는다. `RoomState.selectedMap`(`RoomMapInfo`: 맵 ID, 호스트 언어 이름, `RequiredMod[]`, 확인 차수)을 동기화한다.
- 필요한 모드: `ModService.BuildRequirements` = 맵의 모드 + 의존 모드 전부. 내장 맵은 비어 있어 확인 없이 시작한다.
- 사전 확인: 맵이 바뀌면 게스트가 `ModService.UseRequiredMods`로 폴더를 다시 읽고 비교(ID·버전·`contentHash`·번들 크기)한 뒤 `ReportMapModsServerRpc`로 답한다. 없으면 참가자 목록에 `[모드 없음]`, 게스트 화면 위쪽에 안내 버튼이 뜬다.
- 시작: 호스트가 시작을 누르면 확인 차수를 올려 다시 확인시키고 `StartModCheck`가 최대 5초 기다린다. 전원 있으면 시작, 없거나 답이 없으면 중단하고 호스트에게 이름을, 해당 게스트에게 `RoomModRequiredPopup`(링크·다시 확인)을 보여 준다.
- 판 동안 게스트는 필요한 모드만 켠다(`sessionModIds`). 씬 이름으로 맵을 찾으므로 나만 가진 모드의 씬 이름이 겹치는 문제를 막는다. 저장된 켜기/끄기 설정은 바꾸지 않는다.
- 링크: `mod.json`의 `url`(SDK `ModProject.downloadUrl`). 호스트가 바꿀 수 있는 값이라 받는 쪽이 `ModLinkPolicy`로 검사한다(https, `github.com`, `steamcommunity.com`의 창작마당 경로만, 사용자 정보·포트·비ASCII 금지). 전체 주소를 보여 주고 확인을 받은 뒤 연다. 이름은 `DisplayTextSanitizer`로 정리하고 리치 텍스트를 끈다.
- 시작 방해: 모드 없는 게스트가 있으면 호스트는 맵을 바꾸거나, Phase 10 추방을 쓴다.
- 보안 점검 후 보강(2026-11-04): 씬을 불러올 수 없으면 FishNet 씬 대기열이 멈추지 않게 건너뛰고 대기열이 끝난 뒤 방을 나간다(`ModSceneProcessor`). `mod.json` 번들 이름은 경로 없는 파일 이름만 허용(`ModScanner`). 방 암호를 5번 연속 틀리면 그 주소를 60초 막는다(`PasswordAttemptLimiter`). 게스트도 받은 플레이어 이름을 정리한다.
- 보강(2026-11-05):
  - 컴포넌트 허용 목록 `ModComponentPolicy`(Content, SDK 내보내기와 게임이 같은 규칙). 게임은 빌드에 없는 씬을 불러오면 `ModSceneGuard`로 씬 전체와 맵이 지정한 외형 모델을 검사하고, 걸리면 씬을 끈 뒤 대기열이 끝나고 방을 나간다(`notice.modRejected`). 통과한 씬의 애니메이터와 맵 외형은 애니메이션 이벤트를 끈다.
  - 번들 지문 `ModContentHash`: `ModScanner`가 실제 파일로 다시 계산해 mod.json과 비교한다(파일 크기·수정 시각이 같으면 캐시). 서명이 아니므로 파일과 mod.json을 함께 고치면 통과한다.
  - `ModLinkPolicy`: GitHub는 저장소 첫 화면과 릴리스 화면만 연다. 파일 바로 받기, 계정 기능 화면, 쿼리·조각이 붙은 주소는 막는다.
  - 의존 모드·공유 에셋 번들을 불러오지 못하면 이제 맵 불러오기 실패로 처리한다(`ModBundleCache`).
- 수정(2026-11-06, ModTest 프로젝트 시험에서 발견): `MapPropSetup`이 `ModSceneGuard`보다 먼저 `PropBody`(Runtime)를 붙여 변신 사물이 있는 모든 모드 맵이 거부되던 문제 → `MapPropSetup`이 준비 전에 검사를 부르고 검사는 씬당 한 번만 한다. 켜진 모드끼리 맵 씬 이름이 겹치면 다른 모드의 번들이 열리던 문제 → `ModService.ResolveDuplicateScenes`가 뒤 모드를 오류로 둔다.
- 변경(2026-11-06): 맵 이름 번역을 `ModProject.MapSetting.names`에서 관리한다. `ModStringsFile`이 내보낼 때 `strings.csv`를 만들고 키도 정한다. `ModProject.strings`와 예제의 `strings.csv`는 삭제. 게임이 읽는 형식은 그대로다.
- 수정(2026-11-06): SDK 프로젝트의 Graphics 셰이더 변형 제거 설정이 게임과 달라 번들에 `DOTS_INSTANCING_ON`(GPU Resident Drawer)과 안개 변형이 빠지던 문제. `ModSdkGraphicsExporter`가 게임 값을 `game_shader_stripping.txt`에 적고, `ModSdkSettings.ApplyGameGraphics`가 적용하며, 내보내기 전 확인이 값을 비교한다.
- 남은 위험: 씬 내용은 활성화된 뒤에야 볼 수 있어, 허용되지 않은 컴포넌트의 Awake·OnEnable은 검사 전에 실행된다(이후 Start·Update는 막음). 허용 사이트의 저장소 내용 자체는 보증하지 못한다(자체 AppID 후 창작마당만 허용 검토). 호스트가 보낸 지문·링크는 받는 쪽 규칙으로만 믿는다.

**구현 결과 (2026-10-05, Unity 확인 전)**
- 등록소: `ContentRegistry`(내장 `BuiltInContentSource` → 켜진 모드 `ModContentSource`, 모드 ID 순). 맵 번호 = 목록 번호. 룸의 맵 이름·범위는 등록소에서 읽는다(`GameSettingDefinitions.Get(Map)`). `game_settings.csv`의 Map 줄 범위는 더 이상 쓰지 않는다.
- 맵별 설정: `MapSettingRules` — 맵을 바꾸면 기본값 적용, 설정 보정은 `GetEffective`(게임 범위 ∩ 맵 범위) → `GameSettingsValidator` 순. 맵 인원 범위 밖이면 시작 불가(`room.reason.mapPlayerCount`).
- 모드 로더: `ModService`(시작 시 1회 + 로비에서 켜고 끌 때) → `ModScanner`(검사) · `ModPreferences`(끈 목록, PlayerPrefs) · `Loc.AddStrings`. 맵 번들은 `ModSceneProcessor`(FishNet `DefaultSceneProcessor` 확장, `SessionService`가 설치)가 씬을 불러오기 직전에 `ModBundleCache`로 로드하고, 모드 맵 씬이 없어지면 해제한다.
- 모드 일치: 2026-11-04 아래 "모드 확인 방식 변경"으로 교체. (`ModSetHash`, `modHash`, `ModMismatch`, 로비 데이터 `Mods`/`ModList`, `LobbyModMismatchPopup` 삭제)
- SDK: `GuardVsMannequin.Content.Editor`(Content만 참조) — `ModMapTools`(GameSettingTools > 모드 제작 > 맵 도구), `ModMapValidator`, `ModProject`(Create > GameSettingTools > Mod Project), `ModExporter`(GameSettingTools > 모드 제작 > 모드 내보내기 → `ModExports/<id>/`), 공용 `PropRegistrar`·`PropIndexer`·`PropCopyChecks`. 패키지는 게임 메뉴 "GameSettingTools/모드/모드 SDK 패키지 만들기"(`Builds/ModSDK/`). 안내서 `Docs/ModdingGuide.md`.
- 로비: 메인 화면 오른쪽 위 "모드" 버튼 → `LobbyModsMenu`. 왼쪽 아래 버전 표시(`VersionLabel`). 기존 로비 프리팹은 "UI > 빠진 UI 영역·키 줄 추가"로 더한다.
- 에디터 GI 계산 끄기: `SceneLightingSetup`(공용 `Lighting_RealtimeOnly.lighting`). 모든 씬 생성 도구와 맵 최적화가 적용한다. (기존 씬 일괄 적용 메뉴는 적용을 마쳐 삭제)

**검토가 필요한 위험**
- (B1로 해소) 맵 씬에 FishNet 씬 오브젝트가 없으므로 번들 씬의 SceneId 문제가 없다. 단, 번들 씬을 FishNet `SceneManager`가 이름으로 불러오려면 모든 PC에서 번들이 먼저 로드되어 있어야 한다(B5의 씬 처리기 확장).
- 셰이더: 모드 머티리얼의 셰이더는 번들에 함께 들어가며 검사 도구가 제한하지 않는다(2026-11-06 확인, 안내는 가이드 3.13). 번들에 셰이더가 중복으로 들어가 크기·변형이 늘 수 있다. 번들 셰이더는 제작 프로젝트의 그래픽 API로 컴파일되므로 `game_graphics_apis.txt`로 게임 값에 맞춘다. 빌드에서 제외된 셰이더 변형은 모드에서 분홍색으로 보일 수 있어, 필요한 변형 목록을 `BuildOptimizationSettings`와 맞춘다.
- AssetBundle은 Unity 버전·빌드 대상(Windows)이 같아야 한다. 게임 Unity 버전을 올리면 모드도 다시 빌드해야 하므로 `mod.json`의 Unity 버전으로 걸러낸다.
- 신뢰할 수 없는 번들 로드는 코드 실행은 없지만 비정상 파일로 인한 크래시 가능성이 있다. 직접 설치한 모드만 로드한다(호스트에게서 자동 다운로드하지 않음).

### 테스트 방 (2026-10-20)

로비 "방 만들기 → 직접 방 만들기 → 테스트 방 만들기"로 만든다. (`ISessionService.HostTestRoom`, 직접 접속 방 + 테스트 표시)
- 혼자 시작: `RoomState.CanStartGame`·`GameStateMachine.TryStartMatch`가 테스트 방이면 인원 조건을 보지 않는다. 테스트 표시는 서버만 알고 동기화하지 않는다(`IRoomInfo.IsTestRoom`).
- 승패 없음: `MatchRules`가 테스트 방이면 단계를 넘기지 않는다. (사냥 시간은 테스트 메뉴로만 켜고 끔)
- 게임 중 테스트 메뉴(`HudTestRoomPanel`, 호스트 전용, F2): 가드/마네킹 전환(그 자리 재스폰), 마네킹/구울 전환(사냥 시간 켜고 끄기, 끌 때 마네킹 재스폰), 1인칭/3인칭(F3, `PerspectiveSwitcher` + `IPerspectiveCamera`/`IPerspectiveListener`), 게임 설정 변경(`GameStateMachine.OverrideSettings`, 맵 제외 전 항목).
- 서버 조작은 `ITestRoomActions`(`TestRoomActions`, `PlayerSpawner`가 테스트 방에서만 `GameServices.TestRoom`에 등록). 호스트가 서버라 RPC 없이 직접 호출한다.
- 기존 프리팹은 "UI > 빠진 UI 영역·키 줄 추가"로 로비의 직접 방 만들기 화면과 HUD 테스트 메뉴를 더한다.

### 위험 요소 점검 목록 (2026-10-20, 0.5.0-p7 기준)

프로젝트 전체(Scripts 210개·Content 16개·Editor 44개 파일)를 점검해 문제가 생길 수 있는 부분을 모았다. 우선순위: 🔴 출시 전 필수 / 🟠 곧 처리 / 🟡 여유 있을 때.

**배포·저장소**
- [ ] 🔴 Steam AppID가 480(Spacewar, 공용 테스트 ID)이다. 출시 전에 자체 AppID를 받아 `steam_appid.txt`를 바꾸고, 로비 공개 범위 안내(`SteamLobbyService`)도 고친다. (공용 ID라 로비 목록이 다른 게임과 섞이고, 초대·오버레이가 불안정할 수 있음)
- [x] 🔴 (2026-10-26 처리: 저장소 tmdwp/HideAndFreak를 비공개로 전환하고 `feature/phase6` 푸시, 공개용은 별도 저장소 HideAndFreakPublic에 유료 에셋을 뺀 `public` 브랜치로 올림) 공개 저장소의 `feature/phase6` 원격 기록에 유료 에셋(ECM2 등)이 올라가 있었다.
- [x] 🟠 (2026-10-21 처리: 이동 코드를 인터페이스로 분리) `public` 브랜치는 ECM2를 빼지만 Runtime 어셈블리가 ECM2를 참조하므로 그대로는 컴파일되지 않는다.
  - Runtime·Editor 어셈블리는 ECM2를 참조하지 않는다. 게임 코드는 `ICharacterMotor`(속도·캡슐·카메라·순간이동·소유자 설정)만 쓴다.
  - ECM2 구현은 `Scripts/MovementEcm2`(`GuardVsMannequin.Movement.ECM2`: `PlayerCharacter`, `GuardCharacter`, `MannequinCharacter`, `WallClimbAbility`, `DisguiseRotationAbility`, `PlayerMovementDriver`, `CameraAimProvider`, `PendingLookYaw`)와 `Editor/MovementEcm2`(`Ecm2AvatarMovementRig`)로 옮겼다. 파일 GUID는 그대로라 프리팹 연결은 유지된다.
  - 두 이동 어셈블리는 `GVM_ECM2` 기호가 있을 때만 컴파일된다. `Ecm2DefineSync`가 ECM2 유무를 보고 Standalone 기호를 맞춘다. **public 브랜치의 ProjectSettings에는 `GVM_ECM2`를 넣지 않는다.** (ECM2 없이 기호가 있으면 이동 어셈블리 컴파일 오류로 동기화 코드가 실행되지 못함)
  - 프리팹 생성 도구는 `IAvatarMovementRig`(`AvatarMovementRigs`)로 이동 컴포넌트를 붙이며, 이동 구현이 없으면 아바타 프리팹을 만들지 않고 오류를 남긴다.
  - public 브랜치(ECM2 없음)는 컴파일되지만 아바타가 움직이지 않는다. ECM2를 설치하면 기호가 켜져 동작한다.
- [ ] 🔴 (출시 전 처리, 2026-10-20 결정) 회사 이름이 아직 정해지지 않았다(현재 임시 `HornRabbit`). 회사 이름은 `Application.persistentDataPath`와 PlayerPrefs 위치를 정하므로, 바뀔 때마다 기존 테스터의 설정·키 설정·끈 모드 목록·사용자 폴더 모드(`AppData/LocalLow/<회사>/<제품>/Mods`)가 초기화된 것처럼 보인다. 출시 전에 회사 이름을 확정하고, 필요하면 첫 실행 때 예전 경로에서 옮기는 처리를 넣거나 배포 공지에 적는다. (그 전까지 테스트 빌드에서는 초기화될 수 있음을 감안)

**네트워크·보안**
- [x] 🟠 (2026-10-20 처리: `AimRaycaster.IsValidAim`, 시선은 `PlayerViewSync`에서 거부) 조준 RPC(`GunTool`·`GrenadeTool`·`MannequinAttackTool`)가 받은 방향 벡터가 0이거나 NaN인지 검사하지 않는다. NaN이 들어오면 사격 궤적·수류탄 위치가 NaN이 되어 모든 클라이언트에서 경고·오동작이 날 수 있다. `direction`이 유한하고 길이가 0이 아닌지 확인해 거부한다. (`PlayerViewSync.SetViewServerRpc`의 시선 값도 같음)
- [x] 🟡 (2026-10-21 처리: `GameTuning.FlashlightToggleCooldown` 0.2초, 소유자·서버 양쪽. `ToolCooldown`의 서버 허용 오차를 쿨타임의 절반 이하로 제한해 짧은 쿨타임도 서버에서 지켜지게 함) 손전등 켜기/끄기 RPC(`FlashlightTool.ToggleServerRpc`)에 간격 제한이 없어 연타하면 동기화·스위치 소리가 계속 난다.
- [x] 🟡 (2026-10-21 처리: `MovementValidator`가 마지막 정상 위치와 지금 위치의 몸 중심을 잇는 선으로 맵 충돌체 통과를 검사. 리지드바디가 붙은 충돌체·트리거·자기 몸은 제외. 한쪽 면만 있는 메시 벽을 뒷면에서 뚫는 경우는 잡지 못함) 이동은 소유자 권한이고 `MovementValidator`는 이동 거리만 본다. 거리 예산 안에서 벽을 통과하는 이동은 막지 못한다.
- [x] 🟡 (2026-10-21 처리: 로비에는 모드 ID 목록(`ModService.ModSetIds`)만 넣고, 크기 제한을 넘으면 비움. 로비에서 미리 맞추지 못해도 접속은 진행하고, 서버가 전체 정보로 다시 맞춘 뒤 안 되면 부족한 모드와 함께 거절) Steam 로비 데이터 `ModList`는 켜진 모드 설명을 그대로 넣는다. 로비 값 한도(8KB)를 넘으면 잘려 모드 맞추기가 실패한다.

**게임 흐름**
- [x] 🟠 (2026-10-20 처리: 테스트 메뉴 "룸으로 돌아가기", `ITestRoomActions.ReturnToRoom`) 테스트 방은 판이 끝나지 않으므로 맵을 바꾸려면 방을 나가야 한다. 테스트 메뉴에 "룸으로 돌아가기"(`IMatchControl.ReturnToRoom`)를 넣는다.
- [ ] 🟡 테스트 방에 다른 사람이 IP로 들어오면 그 사람도 끝나지 않는 판에 갇힌다. 테스트 방은 접속을 막거나(`RoomAuthenticator`) 안내한다.
- [ ] 🟡 사냥 모습(구울)은 한 번 바뀌면 되돌리지 않는 설계라, 사냥 시간 → 탐색으로 돌아갈 때는 재스폰에 의존한다. 이후 단계 흐름을 바꿀 때 주의한다.

**외부 의존성**
- [ ] 🟠 FishNet에 로컬 수정이 있다(`SceneLookupData.cs`, "[Hide&Freak 수정]", 모드 맵 씬 이름). FishNet을 업데이트하면 사라진다. 빈 이름은 FishNet 씬 로딩 내부(`SceneManager` 서버 로딩 후 클라이언트에 보낼 씬 목록 생성)에서 만들어져 게임 코드에서 끼어들 수 없다. → 2026-10-20 `FishNetPatchCheck` 추가: 에디터 시작 때 수정이 빠졌으면 오류 로그와 알림 창, 빌드 전에는 빌드를 멈춘다. 남은 일: FishNet 쪽에 이슈·PR을 내서 원본에 반영되면 로컬 수정과 검사를 없앤다. 수정 없이 하려면 맵 씬을 FishNet 밖에서 불러오는 구조 변경이 필요한데(스폰 씬 이동·로딩 순서·활성 씬·맵 로딩 완료 RPC 등), 비용이 커서 **문제가 생길 때 검토**하기로 함(2026-10-20).
- [ ] 🟡 모드 번들은 Unity 버전·빌드 대상이 같아야 한다. 게임 Unity 버전(현재 6000.4.2f1)을 올리면 `UnityVersionRules` 허용 범위와 모드 SDK를 함께 갱신한다.

**성능·품질**
- [x] 🟡 (2026-11-12 처리: 남아 있던 테스트 방 패널은 0.2초 간격, 로딩 화면은 보이는 값이 바뀔 때만 갱신. 나머지는 이전에 `RefreshTimer`·변경 검사로 처리됨) HUD·룸 UI가 매 프레임 `Loc.Format`으로 문자열을 새로 만들었다.
- [x] 🟡 (2026-11-12 처리: `Tests/EditMode`에 `MatchJudgeTests`, `GameSettingsRulesTests`(설정 보정·`RoleAssigner`), `PropLayoutPlannerTests`, `UnityVersionRulesTests`, `StaminaAndOpeningTests`(`StaminaModel`·`MatchOpeningRules`) 추가. `ModSetComparer`는 `RequiredModComparer`로 바뀌어 기존 모드 테스트가 다룸) 순수 로직에 자동 테스트가 없었다. 앞으로 판정·보정 규칙을 바꾸면 같은 폴더에 테스트를 함께 고친다.
- [ ] 🟡 Unity가 업그레이드한 `ProjectSettings`(`DynamicsManager`·`Physics2DSettings` 형식 변경, 빌드 목록의 없는 씬 `Map_Map_Laboratory` 정리)를 커밋해 PC마다 설정이 달라지지 않게 한다.

### Phase 8 — 사물 물리 반응 + 총 탄창 (계획, 2026-10-21)

**목표:** 총에 맞은 변신 사물이 튕겨나가는 손맛을 넣는다. 변신한 마네킹도 **진짜 사물과 똑같이** 날아가게 해서 반응 차이로 들키지 않게 한다. 가짜 마네킹은 없앤다. 총은 쿨타임 대신 **4발 사격 후 5초 장전**으로 바꾼다.

**확정한 방향 (2026-10-21)**
- 가짜 마네킹(`FakeMannequin*`)을 제거한다.
- **총에 맞으면** 변신 사물과 변신한 마네킹이 튕겨나간다. **수류탄에는 물리력이 없다**(지금처럼 외곽선·감속만).
- 총: **탄창 4발, 다 쓰면 5초 장전.** 발사 간격은 지금의 `gunCooldown`을 그대로 쓴다.
- NavMesh는 판 시작 때만 쓰므로(가드 스폰 후보·전원 장치 배치) 사물이 움직여도 다시 굽지 않는다.
- 성능 목표: GTX 1050~1650급 PC에서 체감 차이가 없을 것. → 평소에는 키네마틱, 동시에 움직이는 수에 상한, 움직이는 사물만 번호로 묶어 동기화.

**규모 (맵 씬의 `TransformableProp` 수, 벽 조각 포함)**: AlchemistHouse 1266, Asylum 406, Laboratory 118, Industrial 60, Hospital 28. 연금술사 집은 선반 위 작은 사물이 촘촘해 연쇄 충돌 제한이 꼭 필요하다.

**8-1. 가짜 마네킹 제거**
- 지울 것: `FakeMannequin`, `FakeMannequinBrain`, `FakeMannequinSpawner`, `Prefabs/FakeMannequin.prefab`(+ FishNet 프리팹 목록), `TestEnvironmentSetupTool`의 프리팹 생성·`Game_Systems` 부착, `GameTuning`·`game_tuning.csv`의 `FakeMannequinMinCount/MaxCount/PurgeTime`, 0.3 폴더 구조의 `AI/` 줄, `Game_Systems.unity`의 컴포넌트(일회용 메뉴 또는 씬 재생성).
- **옮길 것 — 실행 중 NavMesh 굽기:** 미리 구운 NavMesh가 없을 때 서버에서 굽는 처리(`EnsureNavMesh`)가 지금은 `FakeMannequinSpawner`에만 있다. 가드 스폰 후보(`SpawnCandidateFilter`)가 NavMesh에 의존하므로, 이 처리를 `NavMeshPointSampler` 쪽(또는 새 `NavMeshProvider`)으로 옮겨 가드 스폰 선택 전에 부른다. 모드 맵에 미리 구운 NavMesh가 없는 경우도 지금처럼 동작해야 한다.
- 문구 정리: 수류탄 설명의 "가짜 마네킹은 영향 없음", 가드 스폰 최소 거리 설명의 "가짜 마네킹 흩뿌리기 4m 포함"(값은 유지).
- **검토: 사람 모습으로 걷는 마네킹의 엄폐가 사라진다.** 탐색 단계에서 걷는 마네킹은 곧바로 진짜로 판별된다. 출발 보호 10초 동안 변신을 마치는 흐름이 되므로 밸런스 테스트에서 확인한다. 부족하면 출발 보호 시간이나 변신 쿨타임을 조정한다.

**8-2. 변신 사물 물리 (서버 시뮬레이션)**
- 대상: `TransformableProp` 중 벽 조각(`IsWall`)이 아니고, 가장 긴 변이 `PropPhysicsMaxSize`(권장 2m) 이하인 사물. 큰 가구는 통로를 막을 수 있어 움직이지 않는다. (변신은 지금처럼 3m까지 가능)
- 질량: 겉모습 경계 부피 × `PropDensity`를 `PropMassMin~Max`로 자른다. 순수 계산은 `PropPhysicsRules`(값만 반환)에 둔다.
- 상태
  1. **평소 — 키네마틱.** 모든 PC에서 Rigidbody `isKinematic = true`. 물리 비용이 거의 없다.
  2. **깨어남 (서버만):** 총에 직접 맞은 사물, 또는 움직이는 사물에 `PropWakeImpulse` 이상의 세기로 부딪힌 사물을 다이나믹으로 바꾸고 충격을 준다(`AddForceAtPosition` — 맞은 위치에 따라 회전). 클라이언트는 계속 키네마틱이고 서버가 보낸 위치를 따라간다.
  3. **멈춤:** 속도가 `PropSettleSpeed` 이하로 `PropSettleTime` 동안 유지되면(또는 Rigidbody가 잠들면) 서버가 최종 위치를 한 번 보내고 다시 키네마틱으로 돌린다. 새 위치는 그대로 유지한다(판이 끝날 때까지).
  4. **상한:** 동시에 다이나믹인 사물이 `PropPhysicsMaxActive`(권장 40)를 넘으면 가장 오래 움직인 사물부터 그 자리에서 멈춘다.
  5. **맵 밖 추락:** 맵 경계(`MapInfo`) 아래 `PropKillDepth`보다 떨어지면 판 시작 때 위치로 되돌린다.
- 동기화: 사물별 NetworkObject를 쓰지 않는다. `Game_Systems`의 `PropPhysicsSync`(NetworkBehaviour, 서버)가 `PropSyncRate`(권장 20Hz)로 **움직이는 사물만** `번호(ushort) + 위치 + 압축 회전`을 묶어 보낸다(비신뢰 채널, 한 개 약 18바이트). 멈춤 위치는 신뢰 채널로 한 번 보낸다. 클라이언트는 `PropPoseInterpolator`가 약 0.1초 버퍼로 보간한다. 게임 중 난입은 막혀 있어 전체 상태 전송은 필요 없다.
- 움직이는 동안 충돌 레이어를 `MovingProp`으로 바꿔 **플레이어와 충돌하지 않게** 한다(클라이언트 위치 지연으로 인한 끼임·밀림 방지). 조준 광선·변신 대상 찾기는 이 레이어도 맞힌다.
- 충돌체: 다이나믹 Rigidbody에는 볼록(convex)이 아닌 `MeshCollider`를 쓸 수 없다. 에디터 도구 `PropColliderBaker`(ContentEditor — 모드 SDK에서도 쓰도록)가 사물마다 **상자/캡슐 근사 또는 미리 만든 볼록 메시**를 붙인다. 실행 중 볼록 메시 계산(로딩 지연)은 하지 않는다. `PropRegistrar`(변신 사물 등록)와 맵 최적화가 이 도구를 부르고, `MapValidator`·`ModMapValidator`가 "물리 대상인데 충돌체가 볼록이 아님"을 경고한다. 맵 생성 도구(`MapSceneBuilder`)가 이미 Rigidbody를 키네마틱으로 바꾸는 처리와 겹치지 않게 정리한다.
- 수류탄: 폭발은 사물을 깨우지 않는다. 궤적 판정은 서버가 하므로 옮겨진 사물에 맞아 폭발 지점이 바뀌어도 모든 PC가 같은 결과를 본다.

**8-3. 변신한 마네킹 날아가기 ("사물 모드")**
- 목표: 진짜 사물과 **같은 물리, 같은 시점, 같은 동기화 경로**. 서버가 직접 시뮬레이션한다.
- 흐름 (서버 권한)
  1. 총이 변신 중인 마네킹을 맞히면 지금처럼 피해를 주고, 같은 충격을 `DisguiseKnockback`에 전달한다. (사람 모습·구울은 날아가지 않는다)
  2. 서버가 대리 물체(`PropProxyPool`의 Rigidbody, 변신 외형 경계로 만든 충돌체, 사물과 같은 질량 규칙)를 마네킹 자리에 놓고 다이나믹으로 시뮬레이션한다. 대리 물체도 `PropPhysicsSync`로 같이 보낸다(번호 영역을 나눔).
  3. 그동안 마네킹은 입력·도구·변신 해제가 잠기고, 캐릭터 이동이 꺼진다. 모든 PC에서 아바타가 대리 물체 위치를 따라가고, 변신 외형(`PropDisguiseView`)은 대리 물체의 기울기·회전을 그대로 보여준다. 이동 검증(`MovementValidator`)은 잠시 멈춘다.
  4. 날아가는 동안 맞는 판정은 대리 물체 충돌체가 받고, `IDamageable`을 그 마네킹의 `PlayerHealth`로 넘긴다(캡슐 판정은 끈다 — 이중 피격 방지).
  5. 멈추면 서버가 `MovementValidator.ServerTeleport`로 그 자리에 옮기고 조작을 돌려준다. 넘어진 기울기는 SyncVar(`restTilt`)로 남겨 **움직이기 전까지 넘어진 모습을 유지**한다(움직이거나 돌면 바로 선다 — 들킬지 말지는 플레이어 선택).
  6. 안전장치: `DisguiseKnockbackMaxTime`(권장 4초)이 지나면 강제로 멈춘다. 날아가는 중에 죽으면 대리 물체를 거두고 평소 사망 처리로 넘어간다. 사냥 시간 진입·판 종료 때도 강제로 멈춘다.
- 소유자 카메라는 날아가는 동안 대리 물체를 따라간다(조작 잠금 표시는 HUD 하단 문구).

**8-4. 총 탄창·장전**
- 규칙: 탄창 `gunMagazineSize`(기본 4발), 발사 간격 `gunCooldown`(기존 설정, 0.5초 권장), 탄창이 비면 자동 장전, 한 발이라도 쓴 뒤 R을 누르면 바로 장전한다. 장전 시간은 채울 탄 수 × `gunReloadTime`(기본 2초)이다. HUD는 조준점 둘레 원형 게이지로 남은 탄을 보여준다(사격 시 시계 방향으로 일정하게 감소, 장전 시 반시계 방향으로 증가, 발사 간격·장전 중에는 색 변경).
- 두 값은 룸 설정(`GameSettings`, `game_settings.csv`, `default_game_settings.json`, `strings.csv`, `GameSettingsValidator`, 맵별 스탯 범위)에 추가한다. 권장 범위: 탄창 1~10발, 장전 1~15초. 빌드 폴더의 옛 json에 키가 없으면 0 → 코드 기본값으로 채우는 기존 처리(`DefaultGameSettings`)를 따른다.
- 구조
  - `GunMagazine`(순수 클래스, `ToolCooldown`처럼 소유자 예측 + 서버 검증): 남은 탄, 장전 끝 시각, `TryConsume`. 서버 판정이 최종이다.
  - 서버의 남은 탄·장전 끝 시각을 **소유자에게만** 동기화한다(SyncVar 읽기 권한 소유자). 서버가 사격을 거부하면 소유자 예측이 다음 동기화로 바로잡힌다.
  - HUD: `ITool`은 그대로 두고, 탄약 표시용 작은 인터페이스 `IAmmoSource`(`Ammo`, `MagazineSize`, `IsReloading`)를 추가한다. `HudBottomPanel`은 "총 3/4" / "장전 중 4.2초"로 표시한다. `CooldownRemaining`은 장전 중이면 장전 남은 시간을 돌려준다.
  - 연출: 장전 소리(`NoiseType.GunReload`, 새 번호), 애니메이션 규칙에 `Reload`(Trigger)를 추가해 있으면 쓰고 없으면 건너뛴다.
- 출발 보호·사냥 시간: 판 시작 때 탄창은 가득. 사냥 시간에도 같은 규칙. → 1장 확정 규칙 4번("쿨타임 동일")을 "탄창·장전 동일"로 고친다.

**8-5. 기존 기능과의 충돌 점검 (구현 시 같이 고칠 것)**

| 대상 | 문제 | 처리 |
|---|---|---|
| `PropAppearance` 지연 복사 (`TransformableProp.Appearance`) | 처음 쓸 때 복사하고 **기울기까지 복사**한다. 넘어진 뒤에 처음 복사한 PC는 기울어진 외형이 되어 PC마다 달라진다 | 섞기(`PropLayoutShuffler`)가 끝난 직후 모든 변신 사물의 외형을 한 번에 복사해 둔다 |
| `WallClimbAbility.IsClimbable` | `attachedRigidbody != null`이면 못 오른다 → Rigidbody가 붙은 사물은 키네마틱이어도 못 오르게 된다 | "다이나믹 Rigidbody만 제외"로 바꾼다 |
| `MovementValidator` 벽 통과 검사 | Rigidbody가 붙은 충돌체를 무시한다 → 사물을 통과하는 이동은 잡지 못한다 | 사물은 벽이 아니므로 그대로 둔다(알려진 한계로 기록) |
| `TransformableProp.CurrentWorldBounds` (변신 거리 검증) | 지금 위치 기준이라 문제없다. 서버 위치가 기준이고 클라이언트는 보간 지연만 있다 | 변신 거리 여유(+1.5m)로 흡수, 테스트로 확인 |
| `PropLayoutShuffler` | 맵 로딩 때만 동작 | 영향 없음. 섞기 뒤 판 시작 위치를 "추락 시 복귀 위치"로 저장 |
| 정적 배칭·오클루전 | 변신 사물은 이미 제외 | 영향 없음 |
| 조명 | GI를 쓰지 않음(실시간) | 영향 없음 |

**8-6. 조정 수치 (`game_tuning.csv` 추가)**

| id | 권장값 | 설명 |
|---|---|---|
| `PropPhysicsEnabled` | 1 | 0이면 물리 반응을 끈다 (문제 시 빠른 차단) |
| `PropPhysicsMaxSize` | 2 | 움직이는 사물의 최대 크기(가장 긴 변, m) |
| `PropPhysicsMaxActive` | 40 | 동시에 움직이는 사물 상한 (변신한 마네킹 대리 물체 포함) |
| `PropDensity` | 40 | 질량 = 경계 부피 × 밀도(kg/m³) |
| `PropMassMin` / `PropMassMax` | 1 / 40 | 질량 범위(kg) |
| `GunKnockback` | 12 | 총에 맞은 사물의 날아가는 세기. 충격량 = 값 × √질량, 속도 = 값 ÷ √질량 (1kg 12m/s, 10kg 3.8m/s, 40kg 1.9m/s) |
| `PropWakeImpulse` | 5 | 움직이는 사물이 다른 사물을 깨우는 최소 충격량 |

(2026-10-21 첫 플레이 확인 후 변경: 처음엔 충격량 6 N·s 고정 + 밀도 150·최대 60kg이라 대부분 사물이 60kg에 걸려 초속 0.1m로 거의 움직이지 않았다. 충격량을 √질량에 비례하게 바꾸고 질량 규칙을 낮췄다. `IImpactReceiver.ImpactMass`로 맞은 대상의 질량을 받는다.)
| `PropSettleSpeed` / `PropSettleTime` | 0.05 / 0.5 | 이 속도 이하로 이 시간 유지되면 멈춤 |
| `PropSyncRate` | 20 | 움직이는 사물 위치 전송 횟수(Hz) |
| `PropKillDepth` | 10 | 맵 경계 아래로 이만큼 떨어지면 시작 위치로 복귀(m) |
| `DisguiseKnockbackMaxTime` | 4 | 변신한 마네킹 사물 모드 최대 시간(초) |

**새 타입과 책임 (SOLID)**
- `Scripts/PropPhysics/` (`GuardVsMannequin.PropPhysics` — `UnityEngine.Physics`와 이름이 겹치지 않게): `PropPhysicsRules`(대상·질량·충격 계산, 값만 반환), `PropPhysicsService`(서버: 깨우기·상한·멈춤, `IPropPhysicsService`로 `GameServices`에 등록), `PropPhysicsSync`(전송·수신), `PropPoseInterpolator`(클라이언트 보간), `PropProxyPool`(마네킹 대리 물체).
- 충격 전달은 `IImpactReceiver.ReceiveImpact(point, impulse)`로 통일한다. `GunTool`은 맞은 대상의 `IDamageable`·`IImpactReceiver`만 찾고, 사물인지 마네킹인지 모른다(개방-폐쇄).
- `Player/Mannequin/DisguiseKnockback`: 사물 모드 진입·종료와 입력 잠금. 외형 기울기 표시는 `PropDisguiseView`에 "외형 회전 덮어쓰기" 한 가지만 추가한다.
- `Player/Guard/GunMagazine` + `IAmmoSource`: 탄창 규칙. `GunTool`은 사격 판정만 한다.
- `ContentEditor/PropColliderBaker`: 물리용 충돌체 생성(모드 SDK 포함).

**구현 단계** (앞선 미커밋 작업과 파일이 섞여 단계별 커밋 대신 Unity 확인 후 한꺼번에 커밋)
- [x] 8-1. 가짜 마네킹 제거 + NavMesh 굽기 이전.
- [x] 8-2a. `PropColliderBaker` + 맵 최적화·등록·검사 연결.
- [x] 8-2b. 서버 물리 + 동기화 + 외형 일괄 복사 + `WallClimbAbility`·`MovementValidator` 조건 수정. `GunTool`이 `IImpactReceiver`로 충격 전달.
- [x] 8-3. 변신한 마네킹 사물 모드(`DisguiseKnockback`, `KnockbackProxy`).
- [x] 8-4. 총 탄창·장전(룸 설정 2개, `GunMagazine`, HUD).
- [ ] 8-5. 테스트 방에서 확인 → 2PC 확인 → 수치 조정. RPC·`GameSettings`가 바뀌므로 **`GameVersion.Protocol` +1**, Player Settings Version은 마이너 버전 올림.

**구현 결과 (2026-10-21, Unity 확인 전, dotnet 컴파일 확인)** — 계획과 달라진 점
- 가짜 마네킹: 스크립트 3개·프리팹·`Game_Systems`의 컴포넌트·FishNet 프리팹 목록 항목·조정 수치 3개를 지웠다. 실행 중 NavMesh 굽기는 `Map/MapNavMesh.EnsureBuilt()`로 옮겨 가드 스폰 후보를 뽑기 직전(`SpawnPointRegistry`)에 부른다.
- 맵 씬 준비 순서는 `Map/MapPropSetup`(sceneLoaded) 한곳에서 정한다: 섞기(`PropLayoutShuffler.ShuffleForMatch`) → 모든 변신 사물 외형 복사 → 물리 대상에 `PropBody` 부착. 섞기의 자체 sceneLoaded 구독은 없앴다.
- 서비스 이름: `PropPhysicsService` 대신 `PropPhysicsSync`(NetworkBehaviour, `IPropPhysics`로 `GameServices.PropPhysics`에 등록) + 순수 진행 규칙 `PropPhysicsSimulation`. 클라이언트 보간은 `PropPoseInterpolator`(+ 공용 `PoseSnapshotBuffer`, `ServerClockEstimate`). 서버 시각은 FishNet Tick.
- 대상 규칙은 Content의 `PropPhysicsRules`(모드 SDK와 공유): 벽 조각·`PropPhysicsMaxSize` 초과·다른 변신 사물을 품거나 그 안에 든 사물·자식에 Rigidbody가 있는 사물은 제외. 충돌체가 모두 기본 도형이거나 볼록이어야 움직인다(아니면 움직이지 않고 맵 검사가 경고).
- 날아가는 사물과 플레이어의 충돌: 새 레이어 대신 **클라이언트에서 날아가는 동안 그 사물의 충돌체를 끈다**(멈춘 위치를 받으면 다시 켬). 서버에서는 그대로 부딪힌다.
- 연쇄: `ImpactRelay` — 날아가는 몸체(사물·마네킹 대리 물체)가 멈춘 사물이나 변신한 마네킹에 `PropWakeImpulse` 이상으로 부딪히면 같은 세기로 깨운다.
- 멈춘(키네마틱) 변신 사물은 `PropPhysicsRules.IsRestingProp`으로 맵 지형처럼 다룬다: 벽타기 가능, 이동 검증의 벽 통과 검사 대상.
- 사물 모드: 대리 물체는 서버에만 있는 `KnockbackProxy`(외형 경계 상자, 같은 질량 규칙, 자기 충돌체와 충돌 무시, 맞으면 피해·충격을 마네킹에 넘김). 자세는 `DisguiseKnockback`이 직접 비신뢰 RPC로 보낸다. 날아가는 동안 `ICharacterMotor.SetFrozen`(ECM2 MovementMode.None), `MovementValidator.SetSuspended`, 변신·해제 잠금(`IActionLock`). 멈추면 바닥 위치로 `ServerTeleport`하고 넘어진 모습은 수평 0.15m·2° 이상 움직이거나 돌 때까지 유지한다. 대리 물체는 동시에 움직이는 사물 상한에 넣지 않았다(플레이어 수만큼만 생김).
- 총: `GunMagazine`(소유자 예측 + 서버 검증, 서버가 거부하면 `TargetRpc`로 소유자 탄창을 맞춤), HUD는 `IAmmoSource`로 "총 3/4: 준비" / "장전 4.2초". 장전 애니메이션은 `Reload` 트리거가 있는 외형에서만. 장전 소리는 넣지 않았다(사운드 에셋 필요). `gunCooldown` 코드 기본값도 json과 같은 1초로 맞췄고 문구는 "총 발사 간격".
- 기존 에셋 적용: 메뉴 **"GameSettingTools/Phase 8/사물 물리 적용"**(`Phase8Migration`, 일회용 — 실행 완료 후 삭제) — Mannequin 프리팹에 `DisguiseKnockback` 추가(FishNet 네트워크 컴포넌트 목록 때문에 YAML 대신 Unity로), `Game_Systems`에 `PropPhysicsSync` 확인, 맵 목록 전체의 사물 충돌체 볼록화. 룸·HUD 테스트 메뉴의 새 설정 줄은 **"UI/빠진 UI 영역·키 줄 추가"**가 더한다(`MenuSceneBuilder.AddMissingSettingRows`).
- **`GameVersion.Protocol` +1 필요** (`GameSettings` 필드·RPC 추가).

**확인 체크리스트**
- 같은 사물을 쏘았을 때 두 PC에서 날아가는 궤적·멈춘 위치가 같은가.
- 변신한 마네킹과 진짜 사물을 나란히 놓고 쏘았을 때 반응(시점·거리·회전)을 눈으로 구별할 수 없는가.
- 연금술사 집 선반을 연속 사격해도 동시에 움직이는 수가 상한을 넘지 않고, 호스트 프레임이 유지되는가(Profiler: Physics.Simulate, 네트워크 송신량).
- 날아간 사물이 문·복도를 막아 이동이 불가능해지는 곳이 없는가.
- 날아가는 중 사망·사냥 시간 진입·판 종료 때 마네킹이 정상으로 돌아오는가.
- 탄창 4발 → 장전 5초가 HUD와 서버 판정에서 일치하는가(연타·지연 상황 포함).

**검토가 필요한 위험**
- **난사 전략:** 빗맞힘 벌칙이 없어 "날려서 확인"이 쉬운 전략이 될 수 있다. 탄창·장전으로 1차 완화하고, 부족하면 빗맞힘 벌칙을 검토한다.
- **사물이 혼자 움직이는 단서:** 움직이는 사물은 플레이어와 충돌하지 않지만, 멈춘(키네마틱) 사물은 마네킹이 밀 수 없다. 마네킹이 사물을 미는 기능은 넣지 않는다.
- **호스트 부담:** 서버 시뮬레이션과 렌더링을 함께 하므로 호스트 PC 기준으로 상한을 정한다.
- **모드 맵:** 기존 모드는 물리용 충돌체가 없다. 충돌체가 볼록이 아닌 사물은 물리 대상에서 빼고(움직이지 않음) 경고만 남겨 호환성을 지킨다.

### Phase 9 — 공개방 목록 (Steam 로비 검색) (계획, 2026-11-02)

**목표:** 친구 없이도 게임을 할 수 있게, 로비에서 열려 있는 공개방 목록을 보고 골라 들어가거나 "빠른 참가"로 바로 들어간다.

**확정한 방향 (2026-11-02)**
- 방 목록은 FishNet이 아니라 이미 쓰는 **Steam 로비**(`SteamMatchmaking.RequestLobbyList`)로 만든다. 서버·비용이 들지 않고, 고른 방에 들어가는 흐름(로비 입장 → `HostAddress`로 FishySteamworks 접속 → `RoomAuthenticator`)은 지금 그대로 쓴다.
- FishNet·RPC는 바뀌지 않는다. 로비 데이터 키만 늘어난다.

**사용자 확정 (2026-11-02)**
- 방 만들기 기본값은 **공개**. 스팀 방 만들기 화면의 토글로 친구 전용을 고를 수 있다. (지금의 인스펙터 고정값 `FriendsOnly`는 없앤다)
- 방 이름은 **호스트가 입력**한다. 최대 24자, 비우면 "<Steam 이름>의 방".
- **빠른 참가 버튼을 넣는다.**
- 게임 중인 방은 **목록에서 숨긴다.** 지금은 게임 중에 `SetJoinable(false)`라 Steam 검색에 나오지 않는다. 단, Phase 10 재접속을 넣으면 게임 중에도 로비 입장이 열려 있어야 하므로, 그때는 로비 데이터 `State`(`Room`/`InGame`)를 적고 검색 필터 `State = Room`으로 숨긴다(10-1 참고).

**9-1. 로비 데이터 정리 (`SteamLobbyService`)**
- 키 상수를 `SteamLobbyKeys`(정적 클래스)로 옮겨 방 만들기·검색이 함께 쓴다.
- 새 키

| 키 | 값 | 용도 |
|---|---|---|
| `Game` | 고정 문자열 `HideAndFreak` | **검색 필터.** 테스트용 AppID 480은 다른 게임 로비가 섞이므로 반드시 거른다 |
| `Name` | 방 이름 (UTF-8 24자 이내) | 목록 표시 |
| `Map` | 맵 ID(`builtin.map_forest`, 모드 맵 ID) | 목록 표시 (이름은 받은 쪽이 `ContentRegistry`로 찾고, 없으면 "모드 맵") |
| `MaxPlayers` | 룸 설정 최대 인원 | 목록 표시, 가득 찬 방 걸러내기 |
| `PingLoc` | 호스트의 Steam 핑 위치 (`SteamNetworkingUtils.GetLocalPingLocation`) | (선택) 받은 쪽이 `EstimatePingTimeFromLocalHost`로 예상 핑 표시 |

- 기존 키(`HostAddress`, `Version`, `HasPassword`)는 그대로 쓴다. (`Mods`, `ModList`는 2026-11-04 모드 확인 변경으로 삭제)
- 맵 모드 표시: 로비 `Map` 키의 맵 ID가 내 등록소(꺼 둔 모드 포함 설치 목록)에 있으면 "모드 있음"으로 본다. 버전·파일까지의 확인은 시작할 때 한다.
- 로비 종류: 방 만들기 때 받은 공개 여부로 `k_ELobbyTypePublic` / `k_ELobbyTypeFriendsOnly`를 고른다(인스펙터 고정값 제거). `CreateLobby(SteamRoomOptions)`로 암호 여부·공개 여부·이름을 한 번에 받는다.
- 인원 상한: 룸 설정의 최대 인원이 바뀌면 `SetLobbyMemberLimit`도 맞춘다(지금은 `maxMembers` 10 고정).
- 호스트 갱신: 룸에서 맵·최대 인원이 바뀌면 `ISessionService.UpdateRoomListing(mapId, maxPlayers)` → `SetLobbyData`. 호출은 `RoomState`의 설정 변경 처리(서버)에서 한다. 직접 접속·테스트 방이면 아무것도 하지 않는다.

**9-2. 방 검색 (`SteamRoomBrowser`, 새 파일)**
- `SteamLobbyService`(내 로비 관리)와 책임을 나눈다(SRP). 인터페이스 `IRoomBrowser`: `Refresh()`, `IsRefreshing`, `event ListUpdated(IReadOnlyList<RoomListing>)`, `event Failed(string key)`. UI는 인터페이스에만 의존한다(DIP).
- 검색 조건(`RequestLobbyList` 전에 설정)
  - `AddRequestLobbyListStringFilter("Game", "HideAndFreak", Equal)`
  - `AddRequestLobbyListStringFilter("Version", GameVersion.Current, Equal)` — 버전이 다른 방은 들어가도 거절되므로 처음부터 숨긴다
  - `AddRequestLobbyListFilterSlotsAvailable(1)` — 가득 찬 방 제외
  - `AddRequestLobbyListDistanceFilter(k_ELobbyDistanceFilterWorldwide)` — 초기 사용자 수가 적으므로 전 세계(사용자가 늘면 기본 거리로)
  - `AddRequestLobbyListResultCountFilter(50)`
- 결과(`LobbyMatchList_t`)마다 `GetLobbyData`·`GetNumLobbyMembers`·`GetLobbyMemberLimit`으로 `RoomListing`(값 구조체: 로비 ID, 이름, 맵 ID, 인원/최대, 암호 여부, 모드 지문, 예상 핑)을 만든다. 내가 들어가 있는 로비는 뺀다.
- 새로고침 간격 제한 3초(Steam 요청 남발 방지). Steam이 꺼져 있으면 `notice.steamNotRunning`.

**9-3. 목록 규칙 (`RoomListingRules`, 순수 클래스 — EditMode 테스트 대상)**
- 정렬: ① 고른 맵의 모드를 가진 방(내장 맵 포함) ② 암호 없는 방 ③ 인원이 많은 방(곧 시작할 방) ④ 예상 핑 낮은 순.
- 표시 필터(UI 토글): 암호 방 숨기기, 모드가 없는 맵의 방 숨기기.
- 빠른 참가 고르기: 암호 없음 + 고른 맵의 모드 있음 + 자리 있음 중 정렬 1순위. 없으면 "들어갈 수 있는 방이 없습니다 — 방을 만들까요?" 안내.

**9-4. 로비 UI**
- 새 화면 `LobbyScreen.Browse`(참가 화면에 "공개방 찾기" 버튼): `LobbyRoomBrowserView` + 줄 프리팹 `RoomListRowView`(이름 · 맵 · 인원 3/10 · 자물쇠 · 모드 없음 표시 · 핑). 새로고침, 빠른 참가, 필터 토글 2개, "방이 없습니다" 빈 화면.
- 줄을 누르면 지금의 `JoinSteamRoom(로비 ID, "")`를 부른다. 암호 방이면 기존 `PasswordRequired` 흐름(스팀 방 입장 화면에 방 코드를 채우고 암호 입력)을 그대로 탄다. 모드가 없는 맵의 방도 들어갈 수 있고, 시작할 때 설치 안내를 받는다.
- 스팀 방 만들기 화면: "공개방" 토글(기본 켜짐)과 방 이름 입력칸 추가.
- 룸 화면 방 코드 옆에 "공개 / 친구 전용" 표시.
- 기존 프리팹은 메뉴 **"UI > 빠진 UI 영역·키 줄 추가"**가 새 화면·토글·입력칸을 더한다(기존 방식). 문구는 `strings.csv`(ko/en).

**9-5. 영향받는 기존 코드**

| 대상 | 변경 |
|---|---|
| `ISessionService` / `SessionService` | `HostSteamRoom(SteamRoomOptions)`(암호·공개·이름), `UpdateRoomListing`, `RoomBrowser` 노출 |
| `SteamLobbyService` | 키 상수 이전, 로비 종류·인원 상한·새 데이터 기록, 인스펙터 `lobbyType` 제거 |
| `RoomState` | 맵·최대 인원 변경 시 `UpdateRoomListing` 호출 (`SetRoomJoinable`과 같은 방식) |
| `LobbyCreateMenu` | 공개 토글·방 이름 입력을 `SteamRoomOptions`로 넘김 |
| `LobbyJoinMenu` / `LobbyNavigation` | "공개방 찾기" 버튼, `LobbyScreen.Browse` 추가 |
| `UiPrefabDefaults`·`SharedUiBuilder` | 새 UI 영역 생성 |

**9-6. 위험과 한계**
- **AppID 480:** 공용 테스트 ID라 다른 게임 로비가 많다. `Game` 필터로 거르지만 결과 50개 한도를 다른 게임이 차지할 수 있어, 테스트 중에는 목록이 비어 보일 수 있다. **자체 AppID를 받으면 해결**된다(출시 필수 항목과 같음).
- **Steam 로비 인원 ≠ 실제 접속 인원:** 로비에 들어온 뒤 FishNet 접속 전이거나, 접속에 실패하고 로비만 남은 경우 숫자가 어긋날 수 있다. 접속 실패 시 로비를 나가는 기존 처리(`LeaveLobby`)를 확인하고, 필요하면 호스트가 실제 인원을 `Players` 키로 따로 적는다.
- **방 이름 악용:** 욕설 등. 1차는 길이 제한만 두고, 신고·필터는 이후 검토한다.
- **오래된 로비:** 호스트가 비정상 종료하면 Steam이 로비를 정리할 때까지 잠깐 남는다. 들어가면 접속 실패 안내가 나오므로 목록을 새로고침하도록 안내한다.
- **버전:** FishNet·RPC 변경이 없어 `GameVersion.Protocol`은 그대로다. Player Settings Version만 마이너 버전을 올린다. 옛 빌드의 방은 `Game` 키가 없어 목록에 나오지 않는다.

**구현 단계**
- [x] 9-1. 로비 데이터·로비 종류·인원 상한, `SteamRoomOptions`, `UpdateRoomListing`.
- [x] 9-2. `SteamRoomBrowser`(`IRoomBrowser`) + 9-3. `RoomListingRules`(+ EditMode 테스트).
- [x] 9-4. 로비 UI(공개방 찾기 화면, 방 만들기 토글·이름, 룸 표시), 문구, UI 생성 메뉴.

**구현하며 바뀐 점 (2026-11-04)**
- 룸 설정에는 최대 인원 항목이 없어, `MaxPlayers`는 고른 맵의 최대 인원(없으면 게임 최대)이다. 인원 상한은 지금 인원보다 낮추지 않는다.
- `HostAddress` 키를 없애고 `GetLobbyOwner`로 접속한다. (로비 데이터는 주인이 위조할 수 있음. Phase 10의 호스트 이전도 이 값을 쓴다)
- `Mods` 지문 대신 `hasMap`(맵 ID가 내 PC에 있는지, 꺼 둔 모드 포함)으로 정렬·거르기·빠른 참가를 한다.
- 공개 여부는 게스트도 볼 수 있게 `IsPublic` 키에 적는다. 목록 줄은 별도 프리팹 없이 `LobbyRoomBrowserView`가 템플릿 버튼으로 만든다.
- [ ] 9-5. 2PC(Steam 계정 2개) 확인: 공개방이 목록에 나오는가, 친구 전용 방은 안 나오는가, 암호·모드 다름·가득 참·게임 중 처리, 맵·인원 변경이 목록에 반영되는가, 빠른 참가.

### Phase 10 — 접속 안정성: 재접속, 호스트 이탈, 방장 추방 (계획, 2026-11-02)

**목표:** 공개방에서 모르는 사람과 할 때 생기는 문제를 막는다. 끊긴 사람은 진행 중인 판으로 돌아올 수 있고, 호스트가 나가도 방이 바로 깨지지 않으며, 방장은 방해하는 사람을 내보낼 수 있다.

**지금 상태 (코드 확인)**
- 게임 중 접속은 `RoomAuthenticator`가 무조건 거부한다(`ConnectionRejectReason.InGame`). Steam 로비도 게임 중에는 `SetJoinable(false)`다.
- 끊기면 FishNet이 그 사람의 아바타를 제거하고, `PlayerRegistry`가 생존 인원에서 뺀다. 가드가 모두 끊기면 `MatchJudge`가 곧바로 마네킹 승리로 끝낸다.
- 끊긴 게스트는 `SessionService.OnClientConnectionState`에서 Steam 로비를 나가고 "호스트 연결 끊김" 안내와 함께 로비로 돌아간다. 다시 들어올 정보(방 코드·암호)는 남지 않는다.
- 호스트가 나가면 서버가 멈추고 모든 게스트가 로비로 돌아간다. 방장 넘기기는 없다.
- 룸 참가자(`RoomMember`)는 표시 이름·준비·호스트 여부만 있다. 추방 기능은 없다.
- 판 도중 상태(옮겨진 사물 위치 등)를 새로 들어온 사람에게 보내는 처리는 없다. Phase 8 계획에 "게임 중 난입은 막혀 있어 전체 상태 전송은 필요 없다"고 적어 둔 부분을 이번에 바꾼다.

**사용자 확정 (2026-11-02)**
1. 재접속 유예 시간은 **60초**. 지나면 그 자리는 사라지고 사망으로 처리한다.
2. 유예 중인 사람은 승패 판정에서 **살아 있는 것으로 계산**한다. 가드 한 명이 잠깐 끊겼다고 판이 바로 끝나지 않게 한다.
3. 게임 중 호스트가 나가면 판은 끝나고 **새 방장 아래 룸으로 돌아간다.**
4. 추방된 사람은 **그 방에 다시 들어올 수 없다**(방이 닫힐 때까지).
5. **게임 중 추방을 허용**한다(ESC 메뉴의 플레이어 목록에서 확인 창 후).

**10-0. 플레이어 식별 키 (공통 기반)**
- 재접속·추방 모두 "같은 사람"을 알아봐야 한다. 연결 번호(`ClientId`)는 다시 접속하면 바뀌므로 쓰지 않는다.
- `PlayerKey`: Steam 방은 접속 주소의 **SteamID**(FishySteamworks가 주므로 위조할 수 없음). 직접 접속 방은 실행할 때마다 만드는 **임의 토큰**을 `JoinRequestBroadcast`에 담아 보낸다(같은 실행 안에서만 재접속 가능).
- 서버의 `RoomMembership`(새 클래스, 순수): `ClientId ↔ PlayerKey` 대응, 추방 목록, 재접속 대기 자리를 관리한다. `RoomState`와 `RoomAuthenticator`가 함께 쓴다.

**10-1. 재접속**
- 끊겼을 때 (서버)
  - 게임 중이면 `RejoinSlot`을 만든다: PlayerKey, 역할, 생존 여부, 체력, 마지막 위치·방향, 변신 상태(복사한 사물 번호·회전), 끊긴 시각.
  - 아바타는 FishNet이 지금처럼 제거한다. 저장은 제거 직전(`PlayerIdentity.ServerDespawned`)에 한다.
  - 유예 동안 `PlayerRegistry`는 그 자리를 "생존 대기"로 센다(정해야 할 것 2). 유예가 지나면 사망으로 처리하고 다시 판정한다.
  - 사냥 시간 진입, 판 종료 때는 대기 자리를 모두 정리한다.
- 다시 들어올 때
  - `RoomAuthenticator`: 게임 중이어도 **대기 자리가 있는 PlayerKey는 통과**시킨다(인원 검사도 그 자리를 이미 포함). 추방된 사람과 처음 오는 사람은 지금처럼 거부한다.
  - FishNet이 전역 씬(맵·게임 시스템)을 새 클라이언트에 자동으로 불러준다. 불러온 뒤 `PlayerSpawner`가 저장한 위치·체력·역할로 아바타를 만들고, 마네킹이면 같은 사물로 다시 변신시킨다. 사냥 시간이면 Ghoul 외형으로 만든다.
  - **판 상태 따라잡기:** 새로 들어온 사람에게 한 번 보낸다(`LateJoinSnapshot`, 대상 지정 메시지).
    - 시작 위치에서 옮겨진 변신 사물의 위치·회전 (`PropPhysicsSync`에 "전체 상태 보내기" 추가)
    - 전원 장치 진행도·파괴 여부, 사냥 시간 여부·깜빡이는 조명은 이미 SyncVar거나 위치 해시로 같아서 따로 보내지 않는다. 구현 때 하나씩 확인한다.
  - 로딩 대기(`MatchSessionBridge`의 로딩 보고)는 판 시작 때만 쓰므로, 판 도중 들어온 사람은 기다리지 않는다.
- 클라이언트
  - 게임 중에 예상치 못하게 끊기면 `LastSession`(로비 ID 또는 주소, 입력한 암호, 끊긴 시각)을 남긴다. 게임이 꺼진 경우에 대비해 PlayerPrefs에도 10분 동안 저장한다.
  - 로비 화면에 **"다시 참가"** 버튼과 남은 유예 시간을 보여 준다. 끊긴 직후 한 번은 자동으로 다시 시도한다.
  - Steam 방은 끊겨도 **Steam 로비를 바로 나가지 않는다**(로비 멤버 확인을 다시 통과하기 위함). 다시 참가를 포기하거나 유예가 지나면 나간다.
- Steam 로비와 Phase 9의 관계
  - 게임 중에도 로비 입장(`SetJoinable`)은 열어 둔다. 대신 로비 데이터 `State = InGame`을 적고, 방 목록 검색은 `State = Room`만 가져온다. 새로 오는 사람은 인증 단계에서 거부된다.

**10-2. 호스트 이탈 (방장 넘기기)**
- FishNet은 호스트가 곧 서버라, 판 도중에 서버를 다른 PC로 옮기는 것(진짜 호스트 이전)은 하지 않는다. 비용이 크고 판 상태 전체를 옮겨야 하기 때문이다. 대신 **"방은 살리고 판은 끝낸다"**.
- 룸 단계에서 호스트가 나갈 때 (정상 종료)
  1. 호스트가 다음 방장을 고른다(가장 먼저 들어온 게스트).
  2. 호스트가 `HostHandoverBroadcast`(새 방장 SteamID, 현재 룸 설정, 방 이름·공개 여부)를 보낸다. 이어서 `SteamMatchmaking.SetLobbyOwner`로 Steam 로비 주인을 넘긴 뒤 나간다.
  3. 새 방장은 같은 Steam 로비에서 서버를 시작하고, `HostAddress`를 자기 SteamID로 바꾼다. 받은 룸 설정을 적용하고, 암호는 자기가 입력했던 암호를 그대로 쓴다.
  4. 나머지 게스트는 로비 데이터 변경(`LobbyDataUpdate_t`)으로 새 `HostAddress`를 보고 자동으로 다시 접속한다. 준비 상태는 초기화된다.
- 게임 중에 호스트가 나갈 때
  - ESC → 방 나가기를 누르면 "나가면 이번 판이 끝납니다" 확인 창을 띄운다.
  - 호스트가 `MatchAbortedBroadcast(HostLeft)`를 보내고 위 1~4를 진행한다. 게스트는 "호스트가 나가 판이 끝났습니다" 안내를 본 뒤 새 방장의 룸으로 간다(정해야 할 것 3).
- 호스트가 갑자기 꺼졌을 때 (알림 없음)
  - Steam은 로비 주인이 나가면 남은 멤버 중 한 명에게 주인을 자동으로 넘긴다.
  - 게스트는 서버 연결이 끊겨도 Steam 로비에 15초 동안 남아 기다린다. 자기가 새 주인이 되면(`GetLobbyOwner == 나`) 마지막으로 받은 룸 설정으로 서버를 시작하고 `HostAddress`를 바꾼다. 나머지는 바뀐 주소로 다시 접속한다. 15초 안에 새 주소가 오지 않으면 로비로 돌아간다.
- 직접 접속 방·테스트 방은 넘기지 않는다. 지금처럼 방이 닫히고 안내가 나온다.

**10-3. 방장 추방**
- 룸 화면 참가자 줄(`RoomMemberListView`)에 호스트에게만 **"추방"** 버튼을 보인다. 확인 창을 띄운 뒤 실행한다.
- 게임 중에는 ESC 메뉴에 플레이어 목록을 두고 같은 버튼을 단다(정해야 할 것 5).
- 서버(호스트가 곧 서버라 RPC 없음): `IRoomModeration.Kick(clientId)`
  1. PlayerKey를 추방 목록에 넣는다.
  2. `ConnectionRejectedBroadcast(Kicked)`를 보낸 뒤 연결을 끊는다.
  3. 재접속 대기 자리를 만들지 않는다. 게임 중이면 사망으로 처리하고 판정한다.
- `RoomAuthenticator`: 추방 목록의 PlayerKey는 `Banned`로 거부한다.
- Steam 로비에서 다른 사람을 강제로 내보내는 API는 없다. 접속 거부가 실제 차단 역할을 하고, 추방된 사람은 끊길 때 스스로 로비를 나간다(지금 코드).
- 문구: `notice.rejected.kicked`("방장이 방에서 내보냈습니다"), `notice.rejected.banned`("이 방에는 다시 들어갈 수 없습니다").

**10-4. 영향받는 기존 코드**

| 대상 | 변경 |
|---|---|
| `RoomAuthenticator` | PlayerKey 확인, 재접속 대기 자리 통과, 추방 목록 거부. 거부 이유 `Kicked`·`Banned` 추가 |
| `JoinRequestBroadcast` | 직접 접속용 토큰 추가 → **Protocol +1** |
| `RoomState` | `RoomMembership` 사용, 끊김 때 재접속 자리 만들기, 추방·방장 넘기기 처리 |
| `PlayerSpawner` | 재접속한 사람을 저장 상태로 스폰 |
| `PlayerRegistry` / `MatchJudge` | 유예 중인 사람을 생존으로 계산, 유예가 끝나면 다시 판정 |
| `PropPhysicsSync` | 판 도중 들어온 사람에게 옮겨진 사물 위치를 한 번 보냄 |
| `SessionService` / `SteamLobbyService` | 끊김 때 로비 유지, `LastSession`, 다시 참가, 로비 주인 넘기기·자동 재접속, 로비 `State` 키 |
| `RoomMemberListView`, `PauseMenuView` | 추방 버튼, 게임 중 플레이어 목록, 호스트 나가기 확인 창 |
| 로비 UI | "다시 참가" 버튼과 남은 시간 |
| `strings.csv` | 새 안내 문구(ko/en) |

**10-5. 위험과 한계**
- **Steam 로비 주인 자동 이전 시점:** 호스트 PC가 갑자기 꺼지면 Steam이 주인이 나간 것을 알아채기까지 수십 초 걸릴 수 있다. 15초 기다림을 넘기면 로비로 돌아간다. 실제 시간은 2PC 테스트로 정한다.
- **새 방장의 PC 성능·회선:** 판 단위로만 넘기므로 새 방장이 느리면 다음 판부터 느려진다. 방장 넘기기 대상을 고르는 기준(먼저 들어온 사람)은 테스트 후 바꿀 수 있다.
- **변신한 마네킹의 재접속:** 끊긴 동안 아바타가 사라지므로 가드가 "사물이 없어졌다"는 단서를 얻을 수 있다. 1차는 이 한계를 받아들이고, 문제가 되면 끊긴 동안 변신 외형만 남겨 두는 방법을 검토한다.
- **유예 악용:** 질 것 같을 때 일부러 끊었다가 들어오는 행동. 다시 들어오면 저장한 위치·체력 그대로라 이득이 거의 없다. 유예 중 받은 피해는 없음(아바타가 없으므로).
- **버전:** 새 메시지와 요청 필드가 생기므로 **`GameVersion.Protocol` +1**, Player Settings Version은 마이너 버전을 올린다.

**사용자 확정 (2026-11-04)**
- 구현 범위: 직접 방으로 확인할 수 있는 10-0·10-3·10-1을 먼저 하고, 방장 넘기기(10-2)는 Steam 계정 2개로 시험할 수 있을 때 한다. 그때까지 호스트가 나가면 지금처럼 방이 닫힌다.
- 사냥 시간에도 재접속을 허용한다. (사냥 진입 때 대기 자리를 정리하지 않음. 위 10-1의 정리 문구는 판 종료 때만 적용)
- ESC 메뉴로 일부러 나가면 즉시 탈락하고 돌아올 수 없다. (`DeclareLeavingServerRpc`)
- 방장이 바뀌면 추방 목록은 초기화한다. (게스트에게 동기화하지 않음, 10-2의 `HostHandoverBroadcast`에도 넣지 않음)

**구현하며 바뀐 점 (2026-11-04)**
- `HostAddress`는 Phase 9에서 없어졌다. 10-2는 `GetLobbyOwner`로 새 방장을 찾도록 구현한다.
- 추방 UI는 참가자 줄마다 버튼을 다는 대신 `HostPlayerPanel` 창 하나를 룸(방 코드 줄 버튼)과 ESC 메뉴에서 함께 쓴다.
- 게임을 껐다 켠 뒤의 복귀(PlayerPrefs에 10분 저장)는 10-2와 함께 한다. Steam 계정으로만 시험할 수 있기 때문이다. 직접 방은 토큰이 실행마다 바뀌어 원래 불가능하다.
- 재접속한 게스트는 맵을 먼저 불러온 뒤 방 정보를 받으므로, 사물 섞기·몸체 준비를 방 정보가 올 때까지 미룬다(`MapPropSetup.RunPending`). 옮겨진 사물 위치는 게스트가 요청해서 받는다(`RequestSnapshotServerRpc`).
- 끊긴 것을 서버가 아직 모르면 새 접속이 이전 연결을 끊는다.
- 맵을 불러오는 동안은 유예 시간 대신 120초 불러오기 기한을 쓴다. 불러오다 다시 끊기면 처음 끊긴 시각으로 판단한다.
- 직접 방의 추방은 토큰 기준이라 게임을 다시 켜면 들어올 수 있다. (Steam 방은 SteamID 기준)
- 시험용: 에디터·개발 빌드의 게스트가 F9를 누르면 알림 없이 연결만 끊는다.
- 호스트가 나갈 때 게스트가 멈추던 문제(2026-11-05): 끊기면 `DefaultScene`이 동기 `LoadScene`으로 로비를 불러오는데, `allowSceneActivation`이 꺼진 로딩이 남아 있으면 교착될 수 있어 `ModSceneProcessor`가 끊길 때 풀어 준다. 수정 후 재현되지 않았지만, 멈춘 시점의 게스트가 로딩 중이었는지는 확인하지 못했다. 다시 나오면 메인 스레드 네이티브 스택을 확인한다.

**구현 순서 (작고 독립적인 것부터)**
- [x] 10-0. `PlayerKey`·`RoomMembership`(+ EditMode 테스트).
- [x] 10-3. 방장 추방(룸 → 게임 중).
- [ ] 10-2a. 룸 단계 방장 넘기기(정상 종료). (보류: Steam 계정 2개 필요)
- [x] 10-1. 재접속(대기 자리, 상태 저장·복원, 판 상태 따라잡기, 다시 참가 UI).
- [ ] 10-2b. 게임 중 호스트 이탈, 갑자기 꺼진 경우의 자동 방장 넘기기. (보류: Steam 계정 2개 필요)
- [ ] 10-6. 2PC 확인: 게스트 랜선 끊고 60초 안에 복귀, 유예 초과, 가드 끊김 때 판 유지, 정상·비정상 호스트 이탈, 추방 후 재입장 거부.
- Phase 9(공개방)와 함께 갈 경우: 로비 `State` 키와 게임 중 `SetJoinable` 유지는 10-1에서 바꾸므로, 9-1을 먼저 하고 10-1에서 맞춘다.

### Phase 11 — 캐릭터 화풍 통일 + 더 무서운 구울 (계획, 2026-11-02)

**목표:** 마네킹·가드·구울·총을 맵(MapKit)과 같은 로우폴리 화풍으로 맞춘다. 사냥 시간의 구울은 생김새·눈·움직임·소리로 확실히 무섭게 만든다.

**사용자 결정 (2026-11-02)**
- 화풍: **MapKit과 같은 로우폴리.** 단순한 형태 + 단색 머티리얼(텍스처 없음), 각진 면.
- 제작: **Blender 스크립트로 직접 생성**(MapKit·지하철 소품·덤불과 같은 방식, `Art/MapKit/Source~`의 공용 함수 재사용). 라이선스 문제가 없어 공개 저장소에도 넣을 수 있다.
- 구울 강화 방향: **실루엣·비율, 얼굴·발광, 움직임, 소리·연출 모두.**

**지금 외형 (교체 대상)**

| 역할 | 지금 | 원본 | 리그·애니메이션 |
|---|---|---|---|
| 마네킹 | 더미 | Kevin Iglesias `Human_BasicMotionsDummy_M` | Humanoid, Kevin 걷기·달리기 + Mixamo 감정 표현 6개 |
| 가드 | SWAT | Low Poly Character Series 1 `Character_SWAT_A_3` | Humanoid, Kevin 걷기·달리기, 조준 IK, 1인칭 팔다리 메시(`LimbMeshFilter`) |
| 가드 총 | Recon_M 소총 | 같은 SWAT 팩 | `Muzzle`·`Right Hand`·`Left Hand` 지점 |
| 구울 | Ghoul | 사용자 제공 `Ghoul.fbx`(낙서 텍스처) | Generic, Idle·Hunch_Reach + 코드로 만든 할퀴기(`GhoulSwipeClipBuilder`) |

**11-1. 공용 캐릭터 생성 기반 (`Art/Characters/Source~/character_common.py`, 새 파일)**
- 구현(2026-11-02): `character_common.py`(Humanoid 뼈대 22개·부위 도형·뼈별 100% 가중치·검사·세로 미리보기·FBX 내보내기), `generate_test_body.py` → `Art/Characters/Models/SK_TestBody.fbx`(1024 tris, 1.81m). Blender 미리보기로 정면(-Y→Unity +Z)·가중치(확인 자세) 확인. Unity 확인 메뉴 **"캐릭터 생성/로우폴리 몸 Humanoid 확인 (SK_TestBody)"**(`LowPolyCharacterCheck`): Humanoid 임포트, 필수 뼈·정면·좌우·키 검사(결과 `Temp/CharacterCheck/report.txt`), 시험용 외형 `Skin_TestBody`(마네킹 애니메이터) 생성. Unity 검사 통과(뼈 22개 연결, +Z 정면, 키 1.86m). 에디터에서 휴머노이드 클립을 입혀 사진을 찍는 방식(SampleAnimation·PlayableGraph·AnimationMode)은 기존 Kevin 더미에서도 근육 자세가 적용되지 않아 빼고, 애니메이션은 테스트 방에서 확인한다. FBX는 뼈대 때문에 `bake_space_transform`을 끄고 Unity 축 변환에 맡긴다.
- `mapkit_common.py`의 팔레트·머티리얼·FBX 내보내기를 그대로 쓴다. 머티리얼 이름 앞머리는 `M_Char_`.
- 몸은 부위별 단순 도형(각진 상자·원기둥·구)을 이어 붙여 만들고, 관절마다 뼈 하나에 100% 가중치를 준다(로우폴리라 자동 가중치보다 깔끔하고, 결과가 항상 같다).
- **Humanoid 호환 뼈대:** `Hips → Spine → Chest → UpperChest → Neck → Head`, 팔(`Shoulder·UpperArm·LowerArm·Hand`), 다리(`UpperLeg·LowerLeg·Foot·Toes`), 손가락은 엄지·나머지 묶음 2개만. Unity Humanoid 아바타 자동 매핑이 되는 이름을 쓴다. → **Kevin 걷기·달리기와 Mixamo 감정 표현을 그대로 재사용**한다.
- 크기 기준: 사람 1.8m, 발이 원점, 정면 +Z(Blender −Y). 기본 자세는 T자.
- 미리보기 PNG(정면·옆·뒤, 맵 소품과 나란히)를 남겨 화풍을 눈으로 맞춘다.

**11-2. 마네킹 (`generate_mannequin.py`)**
- 매장 마네킹 모습: 얼굴 없는 달걀형 머리, 둥근 관절(어깨·팔꿈치·무릎이 공 모양), 매끈한 몸통, 흰색·연회색 무광. 관절이 보이는 것이 "마네킹" 정체성이자 구울로 바뀔 때의 대비가 된다.
- 조금씩 다른 색 2~3개(흰색·베이지·연회색)를 같은 메시에 머티리얼만 바꿔 만들어, 나중에 플레이어마다 다르게 고를 여지를 둔다(1차는 한 가지만 사용).
- 구현(2026-11-02): `generate_mannequin.py` → `Models/SK_Mannequin.fbx`(1504 tris, 1.80m, 머티리얼 `M_Char_MannequinBody`·`M_Char_MannequinJoint`). 몸통은 앞뒤로 납작한 원기둥(골반·허리 관절 고리·가슴), 관절 공 7곳, 벙어리장갑 손, 신발 모양 발. `CharacterAssetBuilder`가 Kevin 더미 대신 이 모델을 Humanoid로 검사·임포트해 `Skin_Dummy`(표시 이름 Mannequin, 파일 이름은 기존 참조 유지)를 만든다. 새 메뉴 **"캐릭터 생성/마네킹 외형만 다시 만들기"**(가드·구울·총은 그대로).

**11-3. 가드 (`generate_guard.py`) + 총 (`generate_guard_rifle.py`)**
- 시설 경비원 모습: 남색 제복, 모자, 조끼, 허리띠의 손전등 집, 장화. 팔다리 굵기는 1인칭 팔다리 메시가 잘 보이게 마네킹보다 조금 굵게.
- 1인칭 팔다리 메시(`LimbMeshFilter`)는 뼈 가중치로 팔다리를 고르므로, 위 뼈 이름 규칙만 지키면 그대로 동작한다. 모델 Read/Write를 켠다.
- 총: 로우폴리 소총(상자·원기둥 조합, 각진 총열), `Muzzle`·`Right Hand`·`Left Hand` 빈 오브젝트를 Blender에서 함께 만든다. 손전등도 같은 화풍으로 만든다.
- 총·손 위치(`WeaponGrip`) 값은 새 모델에 맞춰 다시 조정한다(Play 중 에셋 값 조정 방식 그대로).
- 구현(2026-11-03): `generate_guard.py` → `SK_Guard.fbx`(1800 tris, 1.80m: 남색 제복·검은 조끼·배지·무전기·허리띠와 오른쪽 허리 손전등 집·모자·장갑·장화), `generate_guard_rifle.py` → `SM_GuardRifle.fbx`(176 tris, 0.97m, 원점 = 권총 손잡이, `Muzzle`·`Right Hand`·`Left Hand` 빈 오브젝트 포함). `CharacterAssetBuilder.CreateGun`은 총구 방향·손 위치 추정을 없애고 모델의 표시를 루트 바로 아래로 옮겨 쓴다. 새 메뉴 **"캐릭터 생성/가드 외형·총만 다시 만들기"**. 손전등은 손에 드는 모델이 없고(빛만 카메라에 붙음) 허리 손전등 집으로 표현했다.

**11-4. 구울 (`generate_ghoul.py`) — 생김새**
- **실루엣·비율:** 키 2.4m(지금과 같음)지만 늘 구부정해서 머리는 1.9m 정도. 마네킹 몸이 뒤틀린 모습 — 같은 둥근 관절에 팔이 무릎 아래까지 내려오고(사람의 약 1.5배), 손가락 대신 길고 날카로운 손톱 3개, 튀어나온 척추 마디, 비대칭 어깨, 뒤로 꺾인 발.
- **얼굴·발광:** 마네킹의 매끈한 얼굴이 세로로 찢어져 이빨이 드러난 입, 눈구멍 2개에서 빛나는 눈(발광 머티리얼 `M_Char_GhoulEye`, 어두운 사냥 시간에도 보임). 몸은 마네킹 흰색이 더러워진 회색 + 갈라진 틈의 검붉은 색.
- 리그는 **Generic**(긴 팔·꺾인 다리는 Humanoid 리타깃이 어색해짐). 애니메이션은 Blender에서 스크립트로 키를 찍어 만든다.

**11-5. 구울 — 움직임 (Blender 키 애니메이션)**

| 클립 | 내용 | 쓰는 곳 |
|---|---|---|
| `Ghoul_Idle` | 구부정하게 서서 숨 쉬듯 들썩이다가 가끔 머리를 홱 꺾음 | 멈춤 |
| `Ghoul_Stalk` | 긴 팔로 바닥을 짚으며 느리게 다가가는 걸음 | 걷기 |
| `Ghoul_Crawl` | 네 발로 빠르게 기어 달리기 | 달리기(Shift) |
| `Ghoul_Attack` | 두 팔을 치켜들었다가 휘두르는 할퀴기 (맞는 순간 = `attackHitDelay`) | 공격 |
| `Ghoul_Twitch` | 짧게 경련하듯 떠는 동작 (덧입히는 레이어, 무작위로 가끔) | 대기·걷기 중 |
| `Ghoul_Shriek` | 머리를 젖히고 비명 (사냥 시간 시작 때 한 번) | 연출 |

- **끊기는 느낌:** 일부 키를 일부러 프레임 단위로 뚝뚝 끊어(보간 없음) 부자연스럽게 만든다.
- **갑자기 멈추기:** 이동을 멈추면 바로 `Idle`로 0.05초 만에 넘어가 "딱 멈춘" 느낌을 낸다(애니메이터 전환 시간으로 조절).
- 이동 속도·판정 크기는 바뀌지 않는다. 기어도 충돌 캡슐은 그대로다(보이는 모습만 낮아짐). 화면상 머리 높이와 판정이 달라지는 문제는 테스트로 확인한다.
- `GhoulSwipeClipBuilder`(Unity에서 코드로 만든 할퀴기)와 `Ghoul.fbx`·낙서 텍스처·`M_Ghoul`은 새 모델이 확인되면 지운다.
- 구현(2026-11-03): `generate_ghoul.py` → `SK_Ghoul.fbx`(1452 tris, 서 있으면 2.37m, 팔 약 1.4배, 비대칭 어깨, 척추 마디, 검붉은 틈, 세로 입·이빨, 발광 눈 `M_Char_GhoulEye`) + 액션 6개(Idle 2초·Stalk 1.33초·Crawl 0.67초 반복, Attack 1.08초(맞는 프레임 11)·Shriek 1.67초, Twitch 6초 덧입히기). 다시 불러와 6개 액션이 모두 들어 있는지 확인한다. `GhoulSkinBuilder`를 새로 씀: 눈만 URP 발광 머티리얼(`M_GhoulEye`)로 바꿔 끼우고, 애니메이터는 Speed 0.3/2.7(나갈 때 2.5) 기준 대기↔다가가기↔기어가기, 멈출 때 전환 0.05초, Attack·Shriek 트리거(→ 대기), Twitch 덧입히기 레이어. `attackHitDelay`는 다시 만들 때마다 0.05 + 11/24 ≈ 0.508초로 맞춘다. `CharacterAnimatorParameters.Shriek` 추가(트리거 호출은 11-6). `GhoulSwipeClipBuilder` 삭제. 새 메뉴 **"캐릭터 생성/구울 외형만 다시 만들기"**.

**11-6. 구울 — 소리·연출**
- 소리: 낮은 숨소리·으르렁(구울 주변에서 계속 나는 3D 소리, 걸을 때 커짐), 기어갈 때 손발 짚는 소리, 공격할 때 비명, 사냥 시간 시작 비명. 사운드는 프로젝트의 공포 사운드 팩(`Horror SFX - 082918`, 사용자가 보류해 둔 `free horror ambience 2`)에서 고르고, 없으면 구한다. `NoiseType`에 `GhoulBreath`, `GhoulStep`, `GhoulShriek`을 추가하고 `NoiseLibrary`에 연결한다.
- 가드 화면 연출(가드 본인 화면만): 가까운 구울이 **시야 안에 있고 8m 이내**면 화면 가장자리 노이즈·비네트가 진해지고 심장 소리가 빨라진다. 시야 밖의 구울은 알려 주지 않는다(위치 정보를 주지 않기 위함). 기존 `HuntVisionEffect`·`LowHealthVignette` 방식에 효과를 하나 더한다.
- 사냥 시간 시작: 조명이 꺼질 때 모든 구울이 `Shriek`을 한 번 하고, 가드 화면이 잠깐 흔들린다.
- 구현(2026-11-03, 네트워크 변경 없음 — 모두 각 피어가 보이는 상태로 재생): `NoiseType` 9~12(GhoulBreath·GhoulStep·GhoulShriek·GuardHeartbeat) + 임시 합성음(`ProceduralNoiseClips`), `NoiseLibrary` 항목(클립 비움 — 사운드 팩에서 골라 끼우면 교체). `HuntCreatureSounds`(마네킹): 사냥 중·변신 아님·생존이면 3D 숨소리 반복(멈춤 0.45 ~ 3m/s 이상 1배), 걸음 1.3m·기어가기 0.8m마다 발소리. `MannequinHuntingMode`: 외형 교체 다음 프레임에 `Shriek` 트리거 + 비명. `GuardDreadEffect`(가드): `GhoulDreadRange`(8m) 안의 구울이 화면 안·가려지지 않으면 필름 노이즈·색 번짐·채도 감소 + 2D 심장 소리(빨라짐), 사냥 시간 시작 때 0.8초 흔들림. 저체력 비네트와 겹치지 않게 비네트·노출은 쓰지 않음. 눈 밝기 조정값(`GhoulEyeGlow`)은 아직 넣지 않음.
- 구울 눈의 빛은 손전등으로 비추지 않아도 보이므로, 어둠 속에서 "눈만 보이는" 연출이 된다. 밸런스상 너무 잘 보이면 밝기를 `game_tuning.csv`의 `GhoulEyeGlow`로 조정한다.

**11-7. 영향받는 기존 코드**

| 대상 | 변경 |
|---|---|
| `CharacterAssetBuilder` | 원본 경로를 `Art/Characters/Models`의 새 FBX로 교체(마네킹·가드·총). Kevin 걷기·달리기 클립은 그대로 사용 |
| `GhoulSkinBuilder` | 새 구울 FBX·클립 이름·애니메이터(이동 2단계 + 경련 레이어 + 비명) |
| `MannequinAnimatorBuilder` | 새 마네킹에서도 감정 표현 6개가 그대로 붙는지 확인(Humanoid라 바뀌지 않을 것으로 예상) |
| `LocomotionAnimatorDriver` | 구울 달리기 판단용 `IsSprinting` 파라미터(있으면 씀, 없으면 건너뜀 — 기존 규칙) |
| `NoiseType` / `NoiseLibrary` | 구울 소리 3종 |
| HUD·화면 효과 | 가드용 구울 근접 효과(새 `IViewEffect` 구현) |
| `ModdingGuide.md` | 기본 외형 설명(더미·SWAT → 마네킹·경비원), 구울 애니메이터 파라미터 |
| 에셋 정리 | 확인 후 `Low Poly Character Series 1_SWAT`(가드·총), Kevin `Human_BasicMotionsDummy_M` 모델(애니메이션은 유지) 사용 중지 → 크레딧·`AssetLicenses.md` 갱신, 필요 없어진 팩 삭제 |

**11-8. 위험과 한계**
- **로우폴리 품질:** 스크립트로 만든 사람 모델은 손·얼굴 표현에 한계가 있다. 화풍이 단순한 MapKit에 맞추는 것이라 오히려 어울리지만, 미리보기로 단계마다 확인한다.
- **Humanoid 자동 매핑 실패:** 뼈 이름·축이 맞지 않으면 Unity가 아바타를 못 만든다. 11-1에서 빈 몸으로 먼저 Kevin 걷기와 Mixamo 감정 표현이 제대로 붙는지 확인한 뒤 모델을 만든다.
- **Blender 손 애니메이션의 자연스러움:** 구울 걸음은 코드로 키를 찍으므로 처음엔 어색할 수 있다. 무서운 구울은 "약간 어색한 것"이 장점이 되지만, 발이 미끄러지지 않게 이동 속도와 걸음 길이를 맞춘다.
- **밸런스:** 구울 근접 효과와 눈 발광은 가드에게 정보를 준다. 시야 안·짧은 거리로 제한하고 테스트로 조정한다.
- **모드:** 기존 모드의 외형은 그대로 동작한다(외형 시스템은 바뀌지 않음). 기본 외형이 바뀌는 것뿐이다.
- **버전:** 외형만 바뀌면 Protocol은 그대로. `NoiseType`에 새 번호가 생기면(네트워크로 번호를 보냄) **`GameVersion.Protocol` +1**.

**구현 순서**
- [x] 11-1. 공용 생성 기반 + 빈 몸 Humanoid 확인(Kevin 걷기·Mixamo 감정 표현). — 2026-11-02 테스트 방에서 걷기·감정 표현 확인.
- [x] 11-2. 마네킹 → 테스트 방에서 이동·감정 표현·변신 확인. — 2026-11-03 확인.
- [x] 11-3. 가드·총·손전등 → 1인칭 팔다리, 조준 IK, 총 위치 조정. — 2026-11-03 확인(가드·마네킹 머리 반지름도 키움).
- [x] 11-4·11-5. 구울 모델·애니메이션 → 테스트 방 구울 전환으로 확인. — 2026-11-03 확인. 함께 반영: 임시 절차적 감정 표현 삭제, 공격 중 이동 잠금(`MannequinAttackMoveLock` 0.9초), 기어가기 팔 수정, 1인칭 구울 팔 표시(`FirstPersonBodyView` Generic 지원), 1인칭 카메라가 머리를 따라감(`SkinEyePoint`), 뼈를 따라가는 피격 판정 캡슐(`SkinHitboxes`, `Hitbox` 레이어, 총만 `AimRaycaster.TryRaycastHittable`).
- [x] 11-6. 구울 소리·가드 화면 연출, 사냥 시간 시작 비명. — 2026-11-03 확인. 함께 반영: 구울 본인 숨소리·발소리 0.25배, 가드 손전등이 1인칭 총(FirstPersonGun) 아래에 있어 손에 든 총을 쓰면 꺼지던 문제(CameraPivot 아래로 이동), 본인 손전등 그림자 시작 거리 0.8m, 사냥 시작 흔들림이 카메라에 누적되던 문제.
- [x] 11-7a. 옛 외형 에셋 정리, 크레딧·문서. — 2026-11-05: GUID 참조 검사 후 `Low Poly Character Series 1_SWAT`, Kevin `Unity Demo Scenes`(더미 모델·데모 씬), 옛 `Ghoul.fbx`·`Ghoul_Scribble.png`·`M_Ghoul`·`Ghoul_AttackHunched.anim` 삭제. Kevin 애니메이션과 `Models`(클립의 아바타 원본)는 유지.
- [ ] 11-7b. 새 외형 2PC 확인.

---

## 4. 필요 에셋 체크리스트

에셋을 추가할 때마다 `Docs/AssetLicenses.md`에 **이름 / 출처 / 라이선스 / 사용처**를 기록합니다 (크레딧용).

### 4.1 플러그인
- [x] FishNet
- [x] ECM2
- [x] Steamworks.NET
- [x] FishySteamworks
- [ ] 한글 폰트 (Noto Sans KR, TMP 폰트 에셋)
- [ ] (선택) DOTween

### 4.2 3D 모델
- [ ] 마네킹 캐릭터 (휴머노이드 리그)
- [ ] 가드 3인칭 전신 모델 + 1인칭 팔, 총, 손전등
- [ ] 시설 사물 팩 (다양한 크기, 콜라이더 포함)
- [ ] 전원 장치 (정상 / 파괴 상태)
- [ ] 조명 기구 (On/Off Emission 머티리얼)
- [ ] 벽·바닥·천장 모듈 키트

### 4.3 애니메이션
- [ ] 마네킹: Idle, 걷기, 달리기, 감정 표현 6종, 공격, 피격, 사망
- [ ] 가드: 3인칭 이동·사격·피격·사망, 1인칭 사격·손전등

### 4.4 VFX
- [ ] 총구 화염, 탄착, 변신 연기, 수류탄 모델·폭발 이펙트(지금은 임시 도형), 전원 장치 스파크·폭발

### 4.5 사운드
- [ ] 가드: 총소리, 쿨다운 완료음, 수류탄 던지는 소리·폭발음(지금은 합성음), 손전등 클릭, 발소리
- [ ] 마네킹: 변신/해제, 공격, 발소리 (탐지 소음은 탐지기 삭제로, 변신 중 주기 소음은 2026-11-12 삭제로 필요 없음)
- [ ] 전원 장치: 파괴 진행 루프, 폭발
- [ ] 사냥 시간: 전원 차단음, BGM
- [ ] 기타: 시작 카운트다운, 환경음, 로비 BGM, UI 클릭, 승리/패배 징글

### 4.6 UI
- [ ] 크로스헤어, 도구 쿨타임 아이콘, 진행도 바, 로비·룸·설정 UI 스킨

---

## 5. 추후 확장 (1차 개발 범위 제외)

- 커스텀 맵 모드: 에셋번들 로딩, 변신 가능 사물 태깅 규칙, 맵 제작 에디터 툴
- 1차 개발 중에도 확장을 고려해 **맵 데이터(`MapInfo`)는 씬과 분리된 데이터로 관리**합니다. 변신 가능 사물은 씬의 물체에 `TransformableProp`을 붙이는 것만으로 정의됩니다(외형은 실행 중 복사).
