using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>네트워크 없이 각 피어가 보이는 움직임으로 구울 몸소리를 재생한다.</summary>
    [RequireComponent(typeof(NoiseEmitter), typeof(MannequinTransformer), typeof(PlayerHealth))]
    public class HuntCreatureSounds : MonoBehaviour
    {
        // 한 프레임에 이보다 많이 움직이면 순간이동·되돌림으로 보고 걸음으로 치지 않는다(m).
        private const float TeleportDistance = 2f;

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
        /// 매 프레임 조회하지 않도록 미리 찾아 두고, 첫 프레임 이동량이 튀지 않도록 시작 위치를 기억한다.
        /// </summary>
        private void Awake()
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
            transformer = GetComponent<MannequinTransformer>();
            health = GetComponent<PlayerHealth>();
            lastPosition = transform.position;
        }

        /// <summary>소리를 동기화하지 않도록 실제 이동 거리로 숨소리 크기와 발소리를 정한다.</summary>
        private void Update()
        {
            Vector3 currentPosition = transform.position;
            Vector3 movedDelta = currentPosition - lastPosition;
            lastPosition = currentPosition;
            movedDelta.y = 0f;

            bool isActive = GameServices.CurrentStateOrWaiting == GameState.Hunting && !transformer.IsDisguised && health.IsAlive;
            if (!isActive || Time.deltaTime <= 0f)
            {
                SetBreathing(false, 0f);
                strideProgress = 0f;
                return;
            }

            float movedDistance = movedDelta.magnitude;
            if (movedDistance > TeleportDistance)
                movedDistance = 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, movedDistance / Time.deltaTime, 1f - Mathf.Exp(-8f * Time.deltaTime));

            float selfScale = IsLocalPlayer() ? ownerVolume : 1f;
            float loudness = Mathf.Lerp(idleBreathVolume, 1f, Mathf.Clamp01(smoothedSpeed / loudBreathSpeed));
            SetBreathing(true, loudness * selfScale);

            strideProgress += movedDistance;
            float strideLength = smoothedSpeed >= crawlSpeed ? crawlStride : stalkStride;
            if (strideProgress >= strideLength)
            {
                strideProgress -= strideLength;
                noiseEmitter.PlayLocal(NoiseType.GhoulStep, selfScale);
            }
        }

        /// <summary>본인 귀 바로 옆에서 나는 소리는 줄여야 해서 로컬 플레이어인지 확인한다.</summary>
        private bool IsLocalPlayer()
        {
            PlayerIdentity localPlayer = LocalPlayerEvents.Current;
            return localPlayer != null && localPlayer.gameObject == gameObject;
        }

        /// <summary>사냥하지 않는 구울에 소스를 붙이지 않도록 처음 켤 때 만든다.</summary>
        private void SetBreathing(bool isOn, float loudness)
        {
            if (!isOn)
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
