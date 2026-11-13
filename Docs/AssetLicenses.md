# 사용 에셋 및 라이선스

게임 내 크레딧(`Assets/_Project/Resources/Credits/credits.<언어 코드>.txt`)의 "이용한 에셋" 목록은 이 문서를 기준으로 관리합니다.
에셋을 추가하거나 제거할 때는 **이 문서와 모든 언어의 크레딧 파일을 함께 갱신**하세요.

| 에셋 | 제작 | 버전 | 라이선스 | 위치 | 비고 |
|---|---|---|---|---|---|
| FishNet: Networking Evolved | FirstGearGames | 4.7.3 | FishNet License (`Assets/FishNet/LICENSE.txt`) | `Assets/FishNet` | 네트워크 |
| FishySteamworks | FirstGearGames | 4.1.1 | MIT License | `Assets/FishNet/Plugins/FishySteamworks` | FishNet용 Steam 트랜스포트 |
| Steamworks.NET | Riley Labrecque | 2025.164.1 | MIT License (`Assets/com.rlabrecque.steamworks.net/LICENSE.md`) | `Assets/com.rlabrecque.steamworks.net` | Steam API 래퍼 |
| Easy Character Movement 2 | Oscar Gracián | 1.4.3 | Unity Asset Store EULA | `Assets/ECM2` | **유료 에셋. 저장소는 비공개로 유지** |
| Universal Render Pipeline, Input System, AI Navigation, uGUI | Unity Technologies | 패키지 | Unity Companion License | Package Manager | 엔진 패키지 |

## 그래픽 에셋 (게임에서 사용)
제작자·라이선스 칸의 **(확인 필요)** 는 에셋 스토어 페이지에서 확인해 채워 주세요. 크레딧 파일도 함께 고칩니다.

| 에셋 | 제작 | 라이선스 | 위치 | 사용처 |
|---|---|---|---|---|
| Human Basic Motions FREE | Kevin Iglesias | Unity Asset Store EULA (확인 필요) | `Assets/Kevin Iglesias` | 휴머노이드 걷기·달리기 애니메이션 (데모 씬·더미 모델은 삭제) |
| Modern Vending Machine | (확인 필요) | (확인 필요) | `Assets/Modern Vending Machine` | 환승역·연구소 맵 자판기 |
| Master Stylized Projectiles | (확인 필요) | (확인 필요) | `Assets/MasterStylizedProjectiles` | 총알·명중·총구 화염 이펙트 (OrangeGunShot) |
| Casual Game Sounds U6 | (확인 필요) | (확인 필요) | `Assets/Casual Game Sounds U6` | 게임 소음 (`NoiseLibrary`) |
| Sci-Fi Weapons - Bullet Hell Sound Effects Pack | (확인 필요) | (확인 필요) | `Assets/Sci-Fi Weapons-Bullet Hell Sound Effects Pack` | 게임 소음 (`NoiseLibrary`) |
| Horror SFX - 082918 | GWriterStudio (확인 필요) | (확인 필요) | `Assets/Horror SFX - 082918` | 게임 소음 (`NoiseLibrary`) |

맵 구조물·소품은 Blender로 직접 만든 MapKit 모델(`Assets/_Project/Art/MapKit`)이고, 마네킹·가드·구울·가드 소총도 Blender로 직접 만든 모델(`Assets/_Project/Art/Characters`)입니다.

## 사운드
- 현재 게임 소음은 코드로 만든 **임시 합성음**(`ProceduralNoiseClips`)입니다. 사운드 팩의 클립을 `NoiseLibrary`에 지정하면 교체됩니다.
- 사운드·모델·폰트 에셋을 추가하면 위 표와 크레딧 파일에 추가하세요. (특히 폰트는 게임 배포 허용 여부를 확인)

## 주의
- ECM2는 Unity Asset Store EULA에 따라 **저장소를 공개로 전환할 경우 반드시 제외**해야 합니다.
- FishNet은 자체 라이선스(`Assets/FishNet/LICENSE.txt`)를 따릅니다. 출시 전에 원문의 재배포·사용 조건을 확인하세요.
