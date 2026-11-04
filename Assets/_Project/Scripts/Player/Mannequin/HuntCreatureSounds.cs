using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>
    /// 사냥 시간 마네킹(구울)의 몸에서 나는 소리. 모든 피어가 각자 보이는 움직임으로 재생한다. (네트워크 전송 없음)
    /// - 숨소리·으르렁: 구울 주변에서 계속 반복하는 3D 소리. 멈춰 있으면 작게, 움직일수록 크게.
    /// - 발소리: 움직인 거리가 한 걸음(기어가면 짧게)을 넘을 때마다 손발이 바닥을 짚는 소리.
    /// 변신 중·사망·사냥 시간이 아니면 내지 않는다. 클립과 들리는 거리는 <see cref="NoiseLibrary"/>에서 정한다.
    /// </summary>
    [RequireComponent(typeof(NoiseEmitter), typeof(MannequinTransformer), typeof(PlayerHealth))]
    public class HuntCreatureSounds : MonoBehaviour
    {
        [Tooltip("멈춰 있을 때 숨소리 크기 배율.")]
        [SerializeField] private float idleBreathVolume = 0.45f;

        [Tooltip("구울 본인(로컬 플레이어)에게 들리는 숨소리·발소리 크기 배율. (바로 귀 옆이라 다른 사람보다 훨씬 크게 들리므로 줄인다)")]
        [SerializeField] private float ownerVolume = 0.25f;

        [Tooltip("이 속도(m/s) 이상이면 숨소리가 가장 커진다.")]
        [SerializeField] private float loudBreathSpeed = 3f;

        [Tooltip("걸을 때 한 걸음 거리(m).")]
        [SerializeField] private float stalkStride = 1.3f;

        [Tooltip("기어갈 때 한 걸음 거리(m). 이 속도 이상이면 기어가는 것으로 본다.")]
        [SerializeField] private float crawlStride = 0.8f;

        [SerializeField] private float crawlSpeed = 2.7f;

        private NoiseEmitter noiseEmitter;
        private MannequinTransformer transformer;
        private PlayerHealth health;
        private AudioSource breathSource;
        private Vector3 lastPosition;
        private float strideProgress;
        private float smoothedSpeed;

        /// <summary>
        /// 구성 요소를 캐시한다.
        /// </summary>
        private void Awake()
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            lastPosition = transform.position;
        }

        /// <summary>
        /// 사냥 중인 구울이면 숨소리 크기와 발소리를 갱신하고, 아니면 숨소리를 멈춘다.
        /// </summary>
        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            lastPosition = position;
            moved.y = 0f;

            bool active = GameServices.CurrentStateOrWaiting == GameState.Hunting && !transformer.IsDisguised && health.IsAlive;
            if (!active || Time.deltaTime <= 0f)
            {
                SetBreathing(false, 0f);
                strideProgress = 0f;
                return;
            }

            float distance = moved.magnitude;
            // 순간이동·되돌림은 걸음으로 치지 않는다.
            if (distance > 2f)
                distance = 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, distance / Time.deltaTime, 1f - Mathf.Exp(-8f * Time.deltaTime));

            float selfScale = IsLocalPlayer() ? ownerVolume : 1f;
            float loudness = Mathf.Lerp(idleBreathVolume, 1f, Mathf.Clamp01(smoothedSpeed / loudBreathSpeed));
            SetBreathing(true, loudness * selfScale);

            strideProgress += distance;
            float stride = smoothedSpeed >= crawlSpeed ? crawlStride : stalkStride;
            if (strideProgress >= stride)
            {
                strideProgress -= stride;
                noiseEmitter.PlayLocal(NoiseType.GhoulStep, selfScale);
            }
        }

        /// <summary>이 구울이 이 클라이언트의 로컬 플레이어(본인)인지.</summary>
        private bool IsLocalPlayer()
        {
            PlayerIdentity local = LocalPlayerEvents.Current;
            return local != null && local.gameObject == gameObject;
        }

        /// <summary>
        /// 숨소리 반복 재생을 켜고 크기를 맞추거나 끈다. 처음 켤 때 3D 오디오 소스를 만든다.
        /// </summary>
        private void SetBreathing(bool on, float loudness)
        {
            if (!on)
            {
                if (breathSource != null && breathSource.isPlaying)
                    breathSource.Stop();
                return;
            }

            NoiseLibrary library = noiseEmitter.Library;
            if (library == null)
                return;
            NoiseLibrary.Entry entry = library.Get(NoiseType.GhoulBreath);
            if (breathSource == null)
            {
                breathSource = gameObject.AddComponent<AudioSource>();
                breathSource.playOnAwake = false;
                breathSource.loop = true;
                breathSource.spatialBlend = 1f;
                breathSource.rolloffMode = AudioRolloffMode.Linear;
                breathSource.minDistance = 1f;
                breathSource.clip = library.GetClip(NoiseType.GhoulBreath);
            }

            breathSource.maxDistance = entry.maxDistance;
            breathSource.volume = entry.volume * loudness * AudioVolumes.Sfx;
            if (!breathSource.isPlaying)
            {
                // 여러 구울의 숨이 똑같이 겹치지 않게 시작 지점을 흩는다.
                breathSource.time = Random.Range(0f, breathSource.clip.length);
                breathSource.Play();
            }
        }
    }
}
