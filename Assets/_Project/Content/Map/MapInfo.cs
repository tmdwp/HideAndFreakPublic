using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 맵 단위 정보.
    /// - 천장 높이가 가드 히트스캔 사거리보다 낮은지 검증한다. (천장에 붙은 마네킹도 맞출 수 있어야 하기 때문)
    /// - 맵별 캐릭터 외형: 비워 두면 프리팹의 기본 외형을 쓴다.
    /// - 사냥 시간 마네킹 외형: 비워 두면 프리팹의 기본 사냥 외형(Ghoul)을 쓴다. (모드 맵이 자기 괴물 외형을 지정할 때 사용)
    /// - 변신 사물: 맵에 놓인 TransformableProp을 그대로 복사하므로 따로 지정할 것이 없다.
    /// 씬에 하나만 둔다.
    /// </summary>
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

        /// <summary>현재 씬의 맵 정보. 없으면 null.</summary>
        public static MapInfo Current { get; private set; }

        public string MapName => mapName;
        public float CeilingHeight => ceilingHeight;
        public CharacterSkin MannequinSkin => mannequinSkin;
        public CharacterSkin GuardSkin => guardSkin;
        public CharacterSkin MannequinHuntSkin => mannequinHuntSkin;

        /// <summary>씬 카메라의 대기 화면 위치. 없으면 null.</summary>
        public Transform OverviewPoint => overviewPoint;

        /// <summary>
        /// 역할이 이 맵에서 쓸 외형. 지정하지 않았거나 외형이 없는 역할이면 null. (호출한 쪽이 기본 외형을 쓴다)
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

        /// <summary>
        /// 역할이 이 맵의 사냥 시간에 쓸 외형. 지정하지 않았거나 사냥 외형이 없는 역할이면 null. (호출한 쪽이 기본 사냥 외형을 쓴다)
        /// </summary>
        public CharacterSkin GetHuntSkin(PlayerRole role)
        {
            return role == PlayerRole.Mannequin ? mannequinHuntSkin : null;
        }

        /// <summary>
        /// 현재 맵으로 등록하고 천장 높이를 검증한다.
        /// (아바타보다 먼저 로드되는 씬 오브젝트이므로, 아바타가 생성될 때는 이미 등록되어 있다)
        /// </summary>
        private void Awake()
        {
            Current = this;
            Validate();
        }

        /// <summary>
        /// 씬이 내려가면 등록을 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>
        /// 에디터에서 값이 바뀔 때 천장 높이를 검증한다.
        /// </summary>
        private void OnValidate()
        {
            Validate();
        }

        /// <summary>
        /// 천장 높이가 사거리 이상이면 에러를 남긴다.
        /// </summary>
        private void Validate()
        {
            if (ceilingHeight >= GameConstants.GunRange)
                Debug.LogError($"[MapInfo] {mapName}: 천장 높이({ceilingHeight})가 가드 사거리({GameConstants.GunRange})보다 낮아야 합니다.", this);
        }
    }
}
