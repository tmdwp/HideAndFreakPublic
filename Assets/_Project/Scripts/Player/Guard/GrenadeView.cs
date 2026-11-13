using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>에셋이 없어도 보이도록 비어 있으면 임시 도형으로 대신하는 수류탄 연출.</summary>
    public class GrenadeView : MonoBehaviour
    {
        [Tooltip("날아가는 수류탄 모델. 비우면 작은 구를 쓴다.")]
        [SerializeField] private GameObject grenadeModel;

        [Tooltip("폭발 이펙트 프리팹. 비우면 번쩍이는 빛과 퍼지는 구로 대신한다.")]
        [SerializeField] private GameObject explosionEffect;

        [Tooltip("임시 수류탄 구의 머티리얼.")]
        [SerializeField] private Material grenadeMaterial;

        [Tooltip("임시 폭발 구의 머티리얼.")]
        [SerializeField] private Material blastMaterial;

        [SerializeField] private Color flashColor = new(1f, 0.6f, 0.25f);

        private const float FallbackGrenadeSize = 0.18f;
        private const float BlastDuration = 0.35f;
        private const float EffectLifetime = 3f;

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
        /// 폭발 이펙트 에셋이 없어도 위치와 반경이 보이도록 빛과 퍼지는 구로 대신한다.
        /// </summary>
        private void PlayExplosion(Vector3 position)
        {
            if (noiseEmitter != null)
                noiseEmitter.PlayLocalAt(NoiseType.GrenadeExplosion, position);

            if (explosionEffect != null)
            {
                EffectPool.Spawn(explosionEffect, position, Quaternion.identity, null, 1f, 1f, EffectLifetime);
                return;
            }

            var blast = new GameObject("GrenadeBlast");
            blast.transform.position = position;
            Light flash = blast.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = flashColor;
            flash.range = GameTuning.GrenadeRadius * 4f;
            // 순간적인 섬광이라 그림자 비용을 쓸 가치가 없다.
            flash.shadows = LightShadows.None;

            GameObject sphere = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, blastMaterial);
            sphere.transform.SetParent(blast.transform, false);
            blast.AddComponent<GrenadeBlastAnimator>().Run(flash, sphere.transform, GameTuning.GrenadeRadius, BlastDuration);
        }

        /// <summary>
        /// 모델 에셋이 아직 없어도 날아가는 것이 보이도록 작은 구로 대신한다.
        /// </summary>
        private GameObject CreateFallbackGrenade()
        {
            GameObject sphere = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, grenadeMaterial);
            sphere.name = "Grenade";
            sphere.transform.localScale = Vector3.one * FallbackGrenadeSize;
            return sphere;
        }

        /// <summary>
        /// 연출용 도형이 플레이어나 판정 광선에 걸리지 않도록 기본 충돌체를 없앤다.
        /// </summary>
        private static GameObject CreatePrimitiveWithoutCollider(PrimitiveType type, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Destroy(primitive.GetComponent<Collider>());
            Renderer primitiveRenderer = primitive.GetComponent<Renderer>();
            primitiveRenderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null)
                primitiveRenderer.sharedMaterial = material;
            return primitive;
        }

    }
}
