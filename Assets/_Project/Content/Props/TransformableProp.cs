using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>
    /// 마네킹이 변신 대상으로 지정할 수 있는 사물 표시. 변신하면 이 오브젝트와 자식의 메시를 그대로 복사한다(<see cref="PropAppearance"/>).
    /// 여러 조각으로 된 물체는 부모에 하나만 붙인다. 천장, 바닥, 조명 기구, 전원 장치와 그 주변 구조물에는 붙이지 않는다.
    /// 번호는 에디터가 씬 저장·빌드 때 자동으로 매긴다.
    /// </summary>
    public class TransformableProp : MonoBehaviour
    {
        /// <summary>변신하지 않은 상태 / 번호 없음.</summary>
        public const int None = -1;

        [Tooltip("씬 안의 사물 번호(네트워크 동기화용). 씬을 저장하면 자동으로 매겨지므로 직접 바꾸지 않는다.")]
        [SerializeField] private int index = None;

        [Tooltip("켜면 원래 크기와 관계없이 고정 크기 벽 조각(1.2×2×0.25m, 첫 번째 머티리얼)으로 변신한다. 큰 벽에 사용.")]
        [SerializeField] private bool isWall;

        private PropAppearance appearance;

        /// <summary>씬 안의 사물 번호. 없으면 <see cref="None"/>.</summary>
        public int Index => index;

        /// <summary>고정 크기 벽 조각으로 변신하는지 여부.</summary>
        public bool IsWall => isWall;

        /// <summary>복사한 외형. 처음 쓸 때 한 번 만든다. (씬의 사물은 움직이지 않음)</summary>
        public PropAppearance Appearance => appearance ??= PropAppearance.Capture(this);

        /// <summary>
        /// 지금 놓인 자리의 월드 경계. (서버의 변신 거리 검증)
        /// 판마다 섞기로 외형을 복사한 뒤에 옮겨져도 맞도록, 복사해 둔 사물 기준 경계를 지금 위치·방향으로 옮겨 구한다.
        /// 벽 조각은 경계가 고정 크기 조각이라 원래 벽의 경계를 쓴다. (벽은 섞기에서 옮기지 않음)
        /// </summary>
        public Bounds CurrentWorldBounds => isWall
            ? Appearance.WorldBounds
            : PropAppearance.ToWorld(Appearance.Bounds, transform.position, transform.rotation);

        /// <summary>
        /// 이 사물로 변신할 수 있는지. 번호가 있고, 외형이 있고, 너무 크지 않아야 한다. (서버·클라이언트 같은 판정)
        /// </summary>
        public bool CanDisguiseAs => index != None && Appearance.Parts.Count > 0 && Appearance.LargestSize <= GameTuning.DisguiseMaxSize;

        /// <summary>
        /// 번호 목록에 등록한다.
        /// </summary>
        private void OnEnable()
        {
            PropRegistry.Register(this);
        }

        /// <summary>
        /// 번호 목록에서 뺀다.
        /// </summary>
        private void OnDisable()
        {
            PropRegistry.Unregister(this);
        }

        /// <summary>
        /// 사물 번호를 지정한다. (에디터 툴 전용)
        /// </summary>
        public void SetIndex(int newIndex)
        {
            index = newIndex;
        }

        /// <summary>
        /// 벽 조각으로 변신할지 지정한다. (에디터 툴 전용)
        /// </summary>
        public void SetWall(bool wall)
        {
            isWall = wall;
            appearance = null;
        }
    }
}
