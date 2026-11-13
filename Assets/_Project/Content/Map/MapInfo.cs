using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>맵별 외형과 천장 높이 검증처럼 맵마다 다른 정보를 씬에 하나 두고 제공한다.</summary>
    public class MapInfo : MonoBehaviour
    {
        [SerializeField] private string mapName = "Test";

        [Tooltip("바닥에서 천장까지의 높이. 천장이 개방된 맵은 투명 천장 높이.")]
        [SerializeField] private float ceilingHeight = 4f;

        [Tooltip("이 맵에서 마네킹이 쓸 외형. 비우면 프리팹의 기본 외형.")]
        [SerializeField] private CharacterSkin mannequinSkin;

        [Tooltip("이 맵에서 가드가 쓸 외형. 비우면 프리팹의 기본 외형.")]
        [SerializeField] private CharacterSkin guardSkin;

        [Tooltip("사냥 시간이 되면 마네킹이 바뀔 외형. 비우면 게임 기본 사냥 외형(Ghoul).")]
        [SerializeField] private CharacterSkin mannequinHuntSkin;

        [Tooltip("대기·결과 화면에서 씬 카메라가 맵을 내려다볼 위치와 방향. 비우면 카메라의 기본 위치.")]
        [SerializeField] private Transform overviewPoint;

        /// <summary>씬마다 하나뿐이라 찾지 않고 바로 쓰도록 두며, 맵이 없으면 null이다.</summary>
        public static MapInfo Current { get; private set; }

        public string MapName => mapName;
        public float CeilingHeight => ceilingHeight;
        public CharacterSkin MannequinSkin => mannequinSkin;
        public CharacterSkin GuardSkin => guardSkin;
        public CharacterSkin MannequinHuntSkin => mannequinHuntSkin;

        /// <summary>지정하지 않으면 카메라 기본 위치를 쓰도록 null일 수 있다.</summary>
        public Transform OverviewPoint => overviewPoint;

        /// <summary>
        /// 맵마다 외형을 바꿀 수 있게 하되 대부분의 맵은 기본 외형을 쓰므로, 지정하지 않았으면 null을 돌려 호출한 쪽이 기본 외형을 쓰게 한다.
        /// </summary>
        public CharacterSkin GetSkin(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Guard => guardSkin,
                PlayerRole.Mannequin => mannequinSkin,
                _ => null
            };
        }

        /// <summary>모드 맵이 자기 괴물 외형을 쓸 수 있도록 사냥 외형을 돌려주며, 마네킹이 아니면 null이다.</summary>
        public CharacterSkin GetHuntSkin(PlayerRole role)
        {
            return role == PlayerRole.Mannequin ? mannequinHuntSkin : null;
        }

        /// <summary>아바타가 외형을 고르기 전에 등록돼야 하므로 Awake에서 등록하고, 모드 맵도 천장 높이를 검사한다.</summary>
        private void Awake()
        {
            Current = this;
            Validate();
        }

        /// <summary>
        /// 다음 씬이 먼저 등록했을 수 있어, 아직 자신을 가리킬 때만 비운다.
        /// </summary>
        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>
        /// 맵을 만드는 중에 바로 알 수 있도록 인스펙터 값이 바뀔 때도 확인한다.
        /// </summary>
        private void OnValidate()
        {
            Validate();
        }

        /// <summary>
        /// 천장이 사거리보다 높으면 천장에 붙은 마네킹을 맞출 수 없어 판이 깨지므로 에러로 알린다.
        /// </summary>
        private void Validate()
        {
            if (ceilingHeight >= GameConstants.GunRange)
                Debug.LogError($"[MapInfo] {mapName}: 천장 높이({ceilingHeight})가 가드 사거리({GameConstants.GunRange})보다 낮아야 합니다.", this);
        }
    }
}
