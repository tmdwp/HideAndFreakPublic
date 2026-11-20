using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>에셋이 없어도 보이도록 비어 있으면 임시 도형으로 대신하는 연막탄 연출.</summary>
    public class GrenadeView : MonoBehaviour
    {
        [Tooltip("날아가는 연막탄 모델. 비우면 작은 구를 쓴다.")]
        [SerializeField] private GameObject grenadeModel;

        [Tooltip("터질 때 쓸 이펙트 프리팹. 비우면 코드로 만든 연막을 쓴다.")]
        [SerializeField] private GameObject explosionEffect;

        [Tooltip("임시 수류탄 구의 머티리얼.")]
        [SerializeField] private Material grenadeMaterial;

        [Tooltip("연막 입자 머티리얼(반투명 파티클).")]
        [SerializeField] private Material smokeMaterial;

        [SerializeField] private Color smokeColor = new(0.72f, 0.88f, 1f);

        private const float FallbackGrenadeSize = 0.18f;

        private NoiseEmitter noiseEmitter;

        /// <summary>
        /// 폭발마다 GetComponent를 부르지 않도록 소음 컴포넌트를 미리 찾아 둔다.
        /// </summary>
        private void Awake()
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
        }

        /// <summary>
        /// 던진 가드가 날아가는 도중에 죽어도 연출이 끝까지 재생되도록, 아바타와 떨어진 별도 오브젝트로 날린다.
        /// </summary>
        public void Play(GrenadeSegment[] segments, float fuseTime)
        {
            if (segments == null || segments.Length == 0)
                return;

            GameObject grenade = grenadeModel != null ? Instantiate(grenadeModel) : CreateFallbackGrenade();
            grenade.AddComponent<GrenadeFlight>().Launch(segments, fuseTime, PlayExplosion);
        }

        /// <summary>
        /// 연막이 퍼진 범위가 곧 효과 범위라 마네킹도 피할 곳을 알 수 있도록, 판정과 같은 반경·시간으로 연막을 띄운다.
        /// </summary>
        private void PlayExplosion(Vector3 position)
        {
            if (noiseEmitter != null)
                noiseEmitter.PlayLocalAt(NoiseType.GrenadeExplosion, position);

            float duration = GameTuning.GrenadeSmokeDuration;
            if (explosionEffect != null)
            {
                EffectPool.Spawn(explosionEffect, position, Quaternion.identity, null, 1f, 1f, duration);
                return;
            }

            SmokeCloud.Spawn(position, GameTuning.GrenadeRadius, duration, smokeMaterial, smokeColor);
        }

        /// <summary>
        /// 모델 에셋이 아직 없어도 날아가는 것이 보이도록 작은 구로 대신한다.
        /// </summary>
        private GameObject CreateFallbackGrenade()
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // 연출용 도형이 플레이어나 판정 광선에 걸리지 않도록 기본 충돌체를 없앤다.
            Destroy(sphere.GetComponent<Collider>());
            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            sphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
            if (grenadeMaterial != null)
                sphereRenderer.sharedMaterial = grenadeMaterial;
            sphere.name = "Grenade";
            sphere.transform.localScale = Vector3.one * FallbackGrenadeSize;
            return sphere;
        }
    }
}
