using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>마네킹이 변신할 수 있는 사물을 표시하며, 여러 조각 물체는 부모에 하나만 붙인다.</summary>
    public class TransformableProp : MonoBehaviour
    {
        /// <summary>변신하지 않은 상태 / 번호 없음.</summary>
        public const int None = -1;

        [Tooltip("씬 안의 사물 번호(네트워크 동기화용). 씬을 저장하면 자동으로 매겨지므로 직접 바꾸지 않는다.")]
        [SerializeField] private int index = None;

        [Tooltip("예전 '벽으로 등록' 표시. 벽은 변신 대상이 아니므로 켜져 있으면 변신할 수 없다. (예전 맵·모드 호환용, 새로 켜지 않는다)")]
        [SerializeField] private bool isWall;

        private PropAppearance appearance;

        /// <summary>번호가 없으면 <see cref="None"/>이며 변신할 수 없다.</summary>
        public int Index => index;

        /// <summary>벽은 변신·섞기·튕겨나가기 대상이 아니다.</summary>
        public bool IsWall => isWall;

        /// <summary>메시 복사 비용이 커서 처음 쓸 때 한 번만 만든다.</summary>
        public PropAppearance Appearance => appearance ??= PropAppearance.Capture(this);

        /// <summary>판마다 섞여 옮겨진 뒤에도 서버가 변신 거리를 검증하도록 지금 위치 기준 경계를 구한다.</summary>
        public Bounds CurrentWorldBounds => PropAppearance.ToWorld(Appearance.Bounds, transform.position, transform.rotation);

        /// <summary>서버와 클라이언트가 같은 기준으로 판정하도록 변신 가능 조건을 한곳에 둔다.</summary>
        public bool CanDisguiseAs => !isWall && index != None && Appearance.Parts.Count > 0 && Appearance.LargestSize <= GameTuning.DisguiseMaxSize;

        /// <summary>
        /// 네트워크로 받은 번호로 찾을 수 있도록 켜져 있는 동안만 목록에 둔다.
        /// </summary>
        private void OnEnable()
        {
            PropRegistry.Register(this);
        }

        /// <summary>
        /// 꺼진 사물로 변신하지 않도록 목록에서 뺀다.
        /// </summary>
        private void OnDisable()
        {
            PropRegistry.Unregister(this);
        }

        /// <summary>실행 중 번호가 바뀌면 동기화가 어긋나므로 에디터 도구만 매기도록 둔다.</summary>
        public void SetIndex(int newIndex)
        {
            index = newIndex;
        }

        /// <summary>예전 맵 호환용으로 에디터 도구만 쓰며, 벽 여부가 바뀌면 복사해 둔 외형을 버린다.</summary>
        public void SetWall(bool wall)
        {
            isWall = wall;
            appearance = null;
        }
    }
}
