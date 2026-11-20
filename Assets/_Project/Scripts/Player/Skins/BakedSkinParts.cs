using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 스폰할 때마다 메시를 거르고 캡슐을 만드는 비용을 없애려고, 에디터에서 외형 모델 프리팹에 미리 만들어 둔 1인칭 팔다리와 판정 캡슐을 알려 준다.
    /// 이 컴포넌트가 없는 외형(모드 외형 등)은 실행 중에 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BakedSkinParts : MonoBehaviour
    {
        [Tooltip("1인칭 팔다리 렌더러가 표시 여부를 따라갈 원본 몸 렌더러. limbRenderers와 같은 순서.")]
        [SerializeField] private List<SkinnedMeshRenderer> limbSources = new();

        [Tooltip("팔다리 삼각형만 남긴 1인칭 렌더러. 평소에는 꺼져 있다.")]
        [SerializeField] private List<SkinnedMeshRenderer> limbRenderers = new();

        [Tooltip("뼈를 따라가는 피격 판정 캡슐.")]
        [SerializeField] private List<CapsuleCollider> hitboxes = new();

        public IReadOnlyList<SkinnedMeshRenderer> LimbSources => limbSources;
        public IReadOnlyList<SkinnedMeshRenderer> LimbRenderers => limbRenderers;
        public IReadOnlyList<CapsuleCollider> Hitboxes => hitboxes;

        /// <summary>
        /// 미리 만든 캡슐은 모델과 함께 바로 생기므로, 첫 프레임부터 이동·물리와 부딪히지 않게 레이어 충돌을 먼저 끈다.
        /// </summary>
        private void Awake()
        {
            if (hitboxes.Count > 0)
                _ = SkinHitboxFactory.HitboxLayer;
        }

        /// <summary>몸 렌더러 목록에 섞이면 1인칭 숨김·외곽선이 복제본까지 건드리므로 빼낼 수 있게 묻는다.</summary>
        public bool IsLimbRenderer(Renderer candidate)
        {
            return candidate is SkinnedMeshRenderer skinned && limbRenderers.Contains(skinned);
        }

#if UNITY_EDITOR
        /// <summary>에디터 굽기 도구만 내용을 채운다.</summary>
        public void SetBakedParts(List<SkinnedMeshRenderer> sources, List<SkinnedMeshRenderer> limbs, List<CapsuleCollider> bakedHitboxes)
        {
            limbSources = sources;
            limbRenderers = limbs;
            hitboxes = bakedHitboxes;
        }
#endif
    }
}
