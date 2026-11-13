using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Rendering;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>위치가 드러난 동안 상대 진영 화면에만 이 아바타의 외곽선을 벽 너머로 표시한다.</summary>
    [RequireComponent(typeof(PlayerIdentity))]
    public class RevealOutlineView : MonoBehaviour
    {
        /// <summary>변신·해제로 외형이 바뀌어도 따라가되, 매 프레임 렌더러를 모으지는 않도록 하는 간격(초).</summary>
        private const float RefreshInterval = 0.25f;

        private readonly List<Renderer> renderers = new();
        private IStatusEffects statusEffects;
        private PlayerIdentity identity;
        private IOutlineService outlines;
        private float nextRefreshTime;
        private bool isShowing;

        /// <summary>
        /// 매 프레임 조회하지 않도록 상태 효과와 외곽선 서비스를 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            identity = GetComponent<PlayerIdentity>();
            statusEffects = GetComponent<IStatusEffects>();
            outlines = OutlineRegistry.Instance;
        }

        /// <summary>
        /// 아바타가 사라진 뒤에도 외곽선이 허공에 남지 않도록 지운다.
        /// </summary>
        private void OnDisable()
        {
            Hide();
        }

        /// <summary>효과 시간과 관전 대상이 언제든 바뀌어 매 프레임 조건을 다시 보고, 렌더러 수집만 간격을 둔다.</summary>
        private void Update()
        {
            bool shouldShow = statusEffects != null && statusEffects.IsRevealed && IsOpponentOfLocalPlayer();
            if (!shouldShow)
            {
                Hide();
                return;
            }

            if (isShowing && Time.time < nextRefreshTime)
                return;

            nextRefreshTime = Time.time + RefreshInterval;
            CollectVisibleRenderers();
            outlines.Show(this, renderers.ToArray(), OutlineKind.Reveal);
            isShowing = true;
        }

        /// <summary>
        /// 같은 편에게 외곽선을 보여 주면 의미가 없고, 진영이 없는 관전자에게 보여 주면 정보가 새므로 상대 진영일 때만 참이다.
        /// </summary>
        private bool IsOpponentOfLocalPlayer()
        {
            PlayerIdentity viewer = LocalPlayerEvents.Current;
            if (viewer == null || viewer == identity)
                return false;

            MatchWinner viewerSide = RoleDefinitions.Get(viewer.Role).Side;
            MatchWinner targetSide = RoleDefinitions.Get(identity.Role).Side;
            return viewerSide != MatchWinner.None && targetSide != MatchWinner.None && viewerSide != targetSide;
        }

        /// <summary>
        /// 빛·파티클·선 렌더러까지 외곽선을 그리면 엉뚱한 모양이 나오므로, 지금 켜져 있는 메시 렌더러만 모은다.
        /// </summary>
        private void CollectVisibleRenderers()
        {
            renderers.Clear();
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
            {
                if (candidate.enabled && (candidate is MeshRenderer || candidate is SkinnedMeshRenderer))
                    renderers.Add(candidate);
            }
        }

        /// <summary>
        /// 매 프레임 불려도 서비스에 불필요한 해제를 보내지 않도록 표시 중일 때만 지운다.
        /// </summary>
        private void Hide()
        {
            if (!isShowing)
                return;
            outlines.Hide(this);
            isShowing = false;
        }
    }
}
