using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using GuardVsMannequin.Player.Mannequin;
using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 사냥 시간 가드 본인 화면의 공포 연출. (로컬 가드에게만, 네트워크 전송 없음)
    /// - 가까운 구울: <see cref="GameTuning.GhoulDreadRange"/> 안의 구울이 화면 안에 있고 벽에 가려지지 않으면
    ///   가까울수록 화면에 노이즈·색 번짐이 끼고 색이 빠지며, 심장 소리가 커지고 빨라진다.
    ///   시야 밖·벽 뒤의 구울은 알려 주지 않는다. (위치 정보를 주지 않기 위함)
    /// - 사냥 시간 시작: 화면이 잠깐 흔들리고 노이즈가 번쩍인다.
    /// 저체력 화면(LowHealthVignette)과 겹치지 않도록 비네트·노출은 건드리지 않는다.
    /// 카메라 회전은 이동 구현이 매 프레임 정하므로, 흔들림은 그 뒤(LateUpdate 실행 순서)에 더한다.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(NoiseEmitter))]
    public class GuardDreadEffect : MonoBehaviour
    {
        [Header("화면")]
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

        private const float TargetHeightOffset = 1f;

        private NoiseEmitter noiseEmitter;
        private ICharacterMotor motor;
        private PlayerHealth health;
        private AudioSource heartbeat;
        private Volume volume;
        private VolumeProfile profile;
        private FilmGrain grain;
        private ChromaticAberration chromatic;
        private ColorAdjustments color;
        private float intensity;
        private float shakeRemaining;
        private Quaternion appliedShake = Quaternion.identity;
        private Quaternion shakenLocalRotation;
        private GameState lastState = GameState.Waiting;
        private readonly RaycastHit[] sightHits = new RaycastHit[8];

        /// <summary>
        /// 구성 요소를 캐시한다. (화면 효과·오디오는 로컬 가드가 되었을 때 만든다)
        /// </summary>
        private void Awake()
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
            motor = GetComponent<ICharacterMotor>();
            health = GetComponent<PlayerHealth>();
        }

        /// <summary>
        /// 만든 화면 효과를 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            if (volume != null)
                Destroy(volume.gameObject);
            if (profile != null)
                Destroy(profile);
        }

        /// <summary>
        /// 로컬 가드이면 연출 세기를 계산해 화면·심장 소리에 적용한다.
        /// </summary>
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
            float target = canFeel ? NearestVisibleGhoulCloseness() : 0f;
            intensity = Mathf.MoveTowards(intensity, target, blendSpeed * Time.deltaTime);

            float flash = shakeRemaining > 0f ? shakeRemaining / shakeDuration : 0f;
            float screen = Mathf.Max(intensity, flash * 0.8f);
            grain.intensity.value = screen * maxGrain;
            chromatic.intensity.value = screen * maxChromatic;
            color.saturation.value = intensity * maxDesaturate;
            UpdateHeartbeat();
        }

        /// <summary>
        /// 사냥 시간 시작 직후 카메라를 잠깐 흔든다. (이동 구현이 카메라 회전을 정한 뒤에 더함)
        /// 1인칭 카메라는 피벗의 자식이라 이동 구현이 카메라 자체 회전을 다시 맞추지 않으므로,
        /// 매 프레임 지난 프레임의 흔들림을 되돌린 뒤 새 흔들림을 더하고, 끝나면 완전히 되돌린다. (기울어진 채 남지 않게)
        /// </summary>
        private void LateUpdate()
        {
            Transform view = motor?.ViewTransform;
            if (view == null)
                return;

            UndoShake(view);
            if (shakeRemaining <= 0f || !IsLocalPlayer())
                return;
            shakeRemaining -= Time.deltaTime;
            if (shakeRemaining <= 0f)
                return;

            float amount = shakeAngle * Mathf.Clamp01(shakeRemaining / shakeDuration);
            appliedShake = Quaternion.Euler(Random.Range(-amount, amount), Random.Range(-amount, amount), Random.Range(-amount, amount) * 0.5f);
            view.localRotation *= appliedShake;
            shakenLocalRotation = view.localRotation;
        }

        /// <summary>
        /// 지난 프레임에 더한 흔들림을 뺀다. 그사이 다른 코드가 카메라 회전을 새로 정했으면(3인칭 등) 이미 지워진 것이므로 그대로 둔다.
        /// </summary>
        private void UndoShake(Transform view)
        {
            if (appliedShake == Quaternion.identity)
                return;
            if (Quaternion.Angle(view.localRotation, shakenLocalRotation) < 0.01f)
                view.localRotation *= Quaternion.Inverse(appliedShake);
            appliedShake = Quaternion.identity;
        }

        /// <summary>이 아바타가 이 클라이언트의 로컬 플레이어인지.</summary>
        private bool IsLocalPlayer()
        {
            PlayerIdentity local = LocalPlayerEvents.Current;
            return local != null && local.gameObject == gameObject;
        }

        /// <summary>
        /// 가장 가까운 "보이는" 구울과의 가까움(0~1)을 구한다. 범위 밖·화면 밖·가려짐·변신 중이면 0.
        /// </summary>
        private float NearestVisibleGhoulCloseness()
        {
            Transform view = motor?.ViewTransform;
            Camera camera = view != null ? view.GetComponent<Camera>() : null;
            if (camera == null)
                return 0f;

            float range = Mathf.Max(0.1f, GameTuning.GhoulDreadRange);
            float best = 0f;
            foreach (PlayerIdentity player in ClientPlayerList.Players)
            {
                if (player == null || player.Role != PlayerRole.Mannequin)
                    continue;
                if (player.TryGetComponent(out MannequinTransformer transformer) && transformer.IsDisguised)
                    continue;

                Vector3 target = player.transform.position + Vector3.up * TargetHeightOffset;
                float distance = Vector3.Distance(view.position, target);
                if (distance > range)
                    continue;

                Vector3 viewport = camera.WorldToViewportPoint(target);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                    continue;
                if (IsBlocked(view.position, target, player.transform))
                    continue;

                best = Mathf.Max(best, 1f - distance / range);
            }
            return best;
        }

        /// <summary>
        /// 눈에서 대상까지 환경이 막는지 확인한다. 자기 몸·대상 몸·트리거는 무시한다.
        /// </summary>
        private bool IsBlocked(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            int count = Physics.RaycastNonAlloc(from, delta / distance, sightHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(target))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 심장 소리 크기·빠르기를 연출 세기에 맞춘다.
        /// </summary>
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

        /// <summary>
        /// 처음 쓸 때 화면 효과 볼륨(노이즈·색 번짐·채도)과 2D 심장 소리 소스를 만든다.
        /// </summary>
        private void EnsureCreated()
        {
            if (volume != null)
                return;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0f);
            grain.response.Override(0.6f);
            chromatic = profile.Add<ChromaticAberration>(true);
            chromatic.intensity.Override(0f);
            color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(0f);

            var holder = new GameObject("GuardDreadVolume");
            holder.transform.SetParent(transform, false);
            volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 90f;
            volume.sharedProfile = profile;

            if (noiseEmitter.Library != null)
            {
                heartbeat = gameObject.AddComponent<AudioSource>();
                heartbeat.playOnAwake = false;
                heartbeat.loop = true;
                heartbeat.spatialBlend = 0f;
                heartbeat.clip = noiseEmitter.Library.GetClip(NoiseType.GuardHeartbeat);
            }
        }
    }
}
