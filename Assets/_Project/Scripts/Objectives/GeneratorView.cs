using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using UnityEngine;

namespace GuardVsMannequin.Objectives
{
    /// <summary>
    /// 전원 장치의 파괴 진행도 바(마네킹에게만 표시)와 파괴 상태 외형을 표시한다.
    /// </summary>
    [RequireComponent(typeof(Generator))]
    public class GeneratorView : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Material destroyedMaterial;

        [Tooltip("진행도 바 전체(배경 포함). 로컬 플레이어를 향해 회전한다.")]
        [SerializeField] private Transform barRoot;

        [Tooltip("진행도에 따라 X 크기가 늘어나는 채움 막대. 피벗은 왼쪽 끝.")]
        [SerializeField] private Transform barFill;

        private Generator generator;
        private bool isDestroyedApplied;

        /// <summary>
        /// 진행이 시작되기 전에는 바가 보이면 안 되므로 처음에 숨겨 둔다.
        /// </summary>
        private void Awake()
        {
            generator = GetComponent<Generator>();
            barRoot.gameObject.SetActive(false);
        }

        /// <summary>동기화 값에는 이벤트가 없어 매 프레임 파괴 여부와 진행도를 반영한다.</summary>
        private void Update()
        {
            if (generator.IsDestroyed && !isDestroyedApplied)
            {
                bodyRenderer.sharedMaterial = destroyedMaterial;
                isDestroyedApplied = true;
            }

            UpdateProgressBar();
        }

        /// <summary>가드가 지킬 곳을 쉽게 알지 못하도록 진행도는 마네킹에게만 보이고 바는 로컬 플레이어를 향한다.</summary>
        private void UpdateProgressBar()
        {
            PlayerIdentity localPlayer = LocalPlayerEvents.Current;
            bool isVisible = localPlayer != null && localPlayer.Role == PlayerRole.Mannequin
                             && !generator.IsDestroyed && generator.Progress > 0f;

            if (barRoot.gameObject.activeSelf != isVisible)
                barRoot.gameObject.SetActive(isVisible);
            if (!isVisible)
                return;

            Vector3 fillScale = barFill.localScale;
            fillScale.x = Mathf.Clamp01(generator.Progress);
            barFill.localScale = fillScale;

            Vector3 toPlayer = localPlayer.transform.position - barRoot.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
                barRoot.rotation = Quaternion.LookRotation(-toPlayer);
        }
    }
}
