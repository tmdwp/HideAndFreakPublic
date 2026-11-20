using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player.Mannequin;
using GuardVsMannequin.Rendering;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>위치 정보를 새지 않도록 네트워크 없이 로컬 가드 화면에서만 구울 공포 연출을 한다.</summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(NoiseEmitter))]
    public class GuardDreadEffect : MonoBehaviour
    {
        [Header("화면")]
        [Tooltip("필름 그레인·색수차·채도 효과 프로필. 비우면 기본값으로 실행 중에 만든다.")]
        [SerializeField] private VolumeProfile profileTemplate;
        [SerializeField] private float maxGrain = 1f;
        [SerializeField] private float maxChromatic = 0.8f;
        [Tooltip("가장 가까울 때 채도 감소(-100 = 흑백).")]
        [SerializeField] private float maxDesaturate = -60f;
        [Tooltip("연출 세기가 따라가는 빠르기(초당).")]
        [SerializeField] private float blendSpeed = 2.5f;

        [Header("심장 소리")]
        [Tooltip("가장 가까울 때 심장 소리 재생 속도(빨라짐).")]
        [SerializeField] private float maxHeartbeatPitch = 1.8f;

        [Header("사냥 시간 시작 흔들림")]
        [SerializeField] private float shakeDuration = 0.8f;
        [Tooltip("흔들림 최대 각도(도).")]
        [SerializeField] private float shakeAngle = 2.5f;

        // 발밑이 아니라 몸통을 기준으로 봐야 화면 안·가려짐 판정이 실제 보이는 것과 맞는다.
        private const float TargetHeightOffset = 1f;

        private NoiseEmitter noiseEmitter;
        private ICharacterMotor motor;
        private PlayerHealth health;
        private AudioSource heartbeat;
        private Volume volume;
        private VolumeProfile profile;
        private FilmGrain grain;
        private ChromaticAberration chromatic;
        private ColorAdjustments colorAdjustments;
        private float intensity;
        private float shakeRemaining;
        private Quaternion appliedShake = Quaternion.identity;
        private Quaternion shakenLocalRotation;
        private GameState lastState = GameState.Waiting;
        private readonly RaycastHit[] sightHits = new RaycastHit[8];

        /// <summary>
        /// 원격 가드에는 필요 없으므로 화면 효과·오디오는 여기서 만들지 않고, 구성 요소만 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
            motor = GetComponent<ICharacterMotor>();
            health = GetComponent<PlayerHealth>();
        }

        /// <summary>
        /// 런타임에 만든 프로필은 자동으로 해제되지 않으므로 볼륨과 함께 직접 지운다.
        /// </summary>
        private void OnDestroy()
        {
            if (volume != null)
                Destroy(volume.gameObject);
            VolumeProfiles.Release(profile);
        }

        /// <summary>보이는 구울과의 거리로 연출 세기를 정하고, 갑자기 튀지 않게 천천히 따라간다.</summary>
        private void Update()
        {
            if (!IsLocalPlayer())
                return;
            EnsureCreated();

            GameState state = GameServices.CurrentStateOrWaiting;
            if (state == GameState.Hunting && lastState != GameState.Hunting)
                shakeRemaining = shakeDuration;
            lastState = state;

            bool canFeel = state == GameState.Hunting && (health == null || health.IsAlive);
            float targetIntensity = canFeel ? NearestVisibleGhoulCloseness() : 0f;
            intensity = Mathf.MoveTowards(intensity, targetIntensity, blendSpeed * Time.deltaTime);

            float flash = shakeRemaining > 0f ? shakeRemaining / shakeDuration : 0f;
            float screenIntensity = Mathf.Max(intensity, flash * 0.8f);
            grain.intensity.value = screenIntensity * maxGrain;
            chromatic.intensity.value = screenIntensity * maxChromatic;
            colorAdjustments.saturation.value = intensity * maxDesaturate;
            UpdateHeartbeat();
        }

        /// <summary>이동 구현이 카메라 회전을 정한 뒤에 더해야 덮어쓰이지 않으므로 LateUpdate에서 흔든다.</summary>
        private void LateUpdate()
        {
            Transform viewTransform = motor?.ViewTransform;
            if (viewTransform == null)
                return;

            UndoShake(viewTransform);
            if (shakeRemaining <= 0f || !IsLocalPlayer())
                return;
            shakeRemaining -= Time.deltaTime;
            if (shakeRemaining <= 0f)
                return;

            float shakeAmount = shakeAngle * Mathf.Clamp01(shakeRemaining / shakeDuration);
            appliedShake = Quaternion.Euler(Random.Range(-shakeAmount, shakeAmount), Random.Range(-shakeAmount, shakeAmount), Random.Range(-shakeAmount, shakeAmount) * 0.5f);
            viewTransform.localRotation *= appliedShake;
            shakenLocalRotation = viewTransform.localRotation;
        }

        /// <summary>흔들림이 쌓여 카메라가 기울어진 채 남지 않도록 지난 프레임 몫을 뺀다.</summary>
        private void UndoShake(Transform viewTransform)
        {
            if (appliedShake == Quaternion.identity)
                return;
            if (Quaternion.Angle(viewTransform.localRotation, shakenLocalRotation) < 0.01f)
                viewTransform.localRotation *= Quaternion.Inverse(appliedShake);
            appliedShake = Quaternion.identity;
        }

        /// <summary>네트워크 소유 여부보다 확실하게, 이 클라이언트의 로컬 플레이어로 등록된 아바타인지로 판단한다.</summary>
        private bool IsLocalPlayer()
        {
            PlayerIdentity localPlayer = LocalPlayerEvents.Current;
            return localPlayer != null && localPlayer.gameObject == gameObject;
        }

        /// <summary>보이지 않는 구울까지 알려 주면 위치 정보가 되므로 화면 안에 보이는 구울만 센다.</summary>
        private float NearestVisibleGhoulCloseness()
        {
            Transform viewTransform = motor?.ViewTransform;
            Camera viewCamera = viewTransform != null ? viewTransform.GetComponent<Camera>() : null;
            if (viewCamera == null)
                return 0f;

            float range = Mathf.Max(0.1f, GameTuning.GhoulDreadRange);
            float bestCloseness = 0f;
            foreach (PlayerIdentity player in ClientPlayerList.Players)
            {
                if (player == null || player.Role != PlayerRole.Mannequin)
                    continue;
                if (player.TryGetComponent(out MannequinTransformer transformer) && transformer.IsDisguised)
                    continue;

                Vector3 targetPosition = player.transform.position + Vector3.up * TargetHeightOffset;
                float distance = Vector3.Distance(viewTransform.position, targetPosition);
                if (distance > range)
                    continue;

                Vector3 viewport = viewCamera.WorldToViewportPoint(targetPosition);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                    continue;
                if (IsBlocked(viewTransform.position, targetPosition, player.transform))
                    continue;

                bestCloseness = Mathf.Max(bestCloseness, 1f - distance / range);
            }
            return bestCloseness;
        }

        /// <summary>
        /// 자기 몸이나 대상 몸에 맞은 것을 가려진 것으로 보면 항상 막히므로, 둘을 빼고 환경만 막는 것으로 본다.
        /// </summary>
        private bool IsBlocked(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            int count = Physics.RaycastNonAlloc(from, delta / distance, sightHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = sightHits[i].transform;
                if (!hitTransform.IsChildOf(transform) && !hitTransform.IsChildOf(target))
                    return true;
            }
            return false;
        }

        /// <summary>가까울수록 긴장감이 커지도록 심장 소리를 크고 빠르게 하고, 거의 0이면 멈춘다.</summary>
        private void UpdateHeartbeat()
        {
            if (heartbeat == null)
                return;
            if (intensity <= 0.01f)
            {
                if (heartbeat.isPlaying)
                    heartbeat.Stop();
                return;
            }

            NoiseLibrary.Entry entry = noiseEmitter.Library.Get(NoiseType.GuardHeartbeat);
            heartbeat.volume = entry.volume * intensity * AudioVolumes.Sfx;
            heartbeat.pitch = Mathf.Lerp(1f, maxHeartbeatPitch, intensity);
            if (!heartbeat.isPlaying)
                heartbeat.Play();
        }

        /// <summary>원격 가드마다 만들면 낭비이므로 로컬 가드에서 처음 쓸 때 만들고, 위치를 알리지 않게 2D로 둔다.</summary>
        private void EnsureCreated()
        {
            if (volume != null)
                return;

            var volumeHolder = new GameObject("GuardDreadVolume");
            volumeHolder.transform.SetParent(transform, false);
            volume = volumeHolder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 90f;

            profile = VolumeProfiles.Assign(volume, profileTemplate, CreateDefaultProfile);
            grain = VolumeProfiles.GetOrAdd<FilmGrain>(profile);
            chromatic = VolumeProfiles.GetOrAdd<ChromaticAberration>(profile);
            colorAdjustments = VolumeProfiles.GetOrAdd<ColorAdjustments>(profile);
            grain.intensity.Override(0f);
            chromatic.intensity.Override(0f);
            colorAdjustments.saturation.Override(0f);

            if (noiseEmitter.Library != null)
            {
                heartbeat = gameObject.AddComponent<AudioSource>();
                heartbeat.playOnAwake = false;
                heartbeat.loop = true;
                heartbeat.spatialBlend = 0f;
                heartbeat.clip = noiseEmitter.Library.GetClip(NoiseType.GuardHeartbeat);
            }
        }

        /// <summary>에디터 미리 굽기와 실행 중 대체가 같은 값을 쓰도록 기본 프로필을 한곳에서 만든다.</summary>
        public static VolumeProfile CreateDefaultProfile()
        {
            VolumeProfile defaultProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            defaultProfile.name = "VP_GuardDread";

            var defaultGrain = defaultProfile.Add<FilmGrain>(true);
            defaultGrain.type.Override(FilmGrainLookup.Medium3);
            defaultGrain.intensity.Override(0f);
            defaultGrain.response.Override(0.6f);
            defaultProfile.Add<ChromaticAberration>(true).intensity.Override(0f);
            defaultProfile.Add<ColorAdjustments>(true).saturation.Override(0f);
            return defaultProfile;
        }
    }
}
