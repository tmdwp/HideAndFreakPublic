using GuardVsMannequin.Core;
using GuardVsMannequin.Player;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>변신한 사물을 맞혔는지 바로 알 수 있도록 서버가 명중을 확인하면 조준점에 X 표시와 짧은 소리를 낸다.</summary>
    public class HudHitMarker : MonoBehaviour, IHudSection
    {
        [SerializeField] private RectTransform marker;
        [SerializeField] private Graphic[] markerLines;

        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private Color eliminateColor = new(1f, 0.25f, 0.2f, 1f);

        [Tooltip("명중 표시가 보이는 시간(초).")]
        [SerializeField] private float hitDuration = 0.18f;

        [Tooltip("탈락시켰을 때 표시가 보이는 시간(초). 일반 명중과 구별되도록 더 길게 둔다.")]
        [SerializeField] private float eliminateDuration = 0.45f;

        [Tooltip("탈락시켰을 때 표시 크기 배율.")]
        [SerializeField] private float eliminateScale = 1.35f;

        [Tooltip("명중 소리. 비우면 짧은 합성음을 쓴다.")]
        [SerializeField] private AudioClip hitSound;

        [Tooltip("탈락 소리. 비우면 짧은 합성음을 쓴다.")]
        [SerializeField] private AudioClip eliminateSound;

        private AudioSource audioSource;
        private float shownAt = float.NegativeInfinity;
        private float shownDuration;

        /// <summary>
        /// 명중은 Refresh 밖에서 오므로 이벤트를 듣고, 소리는 위치 없이 들리도록 2D로 재생한다.
        /// </summary>
        public void Bind(HudContext hudContext)
        {
            LocalCombatEvents.HitConfirmed += OnHitConfirmed;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        /// <summary>
        /// 정적 이벤트라 끊지 않으면 파괴된 HUD가 계속 불린다.
        /// </summary>
        private void OnDestroy()
        {
            LocalCombatEvents.HitConfirmed -= OnHitConfirmed;
        }

        /// <summary>
        /// 네트워크 준비 전에는 맞힐 일이 없으므로 숨긴다.
        /// </summary>
        public void Hide()
        {
            marker.gameObject.SetActive(false);
        }

        /// <summary>
        /// 표시 시간만 지나면 꺼지면 되므로 남은 시간만 확인한다.
        /// </summary>
        public void Refresh(GameState state)
        {
            marker.gameObject.SetActive(Time.unscaledTime - shownAt < shownDuration);
        }

        /// <summary>탈락은 일반 명중보다 중요해 색·크기·소리를 다르게 한다.</summary>
        private void OnHitConfirmed(bool isEliminated)
        {
            shownAt = Time.unscaledTime;
            shownDuration = isEliminated ? eliminateDuration : hitDuration;
            marker.localScale = Vector3.one * (isEliminated ? eliminateScale : 1f);
            Color color = isEliminated ? eliminateColor : hitColor;
            foreach (Graphic line in markerLines)
            {
                if (line != null)
                    line.color = color;
            }
            marker.gameObject.SetActive(true);

            AudioClip clip = isEliminated
                ? eliminateSound != null ? eliminateSound : UiFeedbackClips.Eliminate
                : hitSound != null ? hitSound : UiFeedbackClips.Hit;
            audioSource.PlayOneShot(clip, AudioVolumes.Sfx);
        }
    }
}
