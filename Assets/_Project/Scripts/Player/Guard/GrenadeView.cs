using GuardVsMannequin.Audio;
using GuardVsMannequin.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 수류탄 연출: 날아가는 모델, 폭발(번쩍임 + 퍼지는 구 + 폭발음).
    /// 모델·폭발 이펙트 프리팹을 넣으면 그것을 쓰고, 비어 있으면 임시 도형으로 대신한다. (에셋이 준비되면 인스펙터에서 교체)
    /// 판정은 서버(<see cref="GrenadeTool"/>)가 하고, 이 컴포넌트는 모든 클라이언트에서 같은 궤적을 보여주기만 한다.
    /// </summary>
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
        /// 폭발음을 낼 소음 컴포넌트를 찾는다.
        /// </summary>
        private void Awake()
        {
            noiseEmitter = GetComponent<NoiseEmitter>();
        }

        /// <summary>
        /// 수류탄을 궤적대로 날리고, 도착하면 폭발 연출을 재생한다.
        /// </summary>
        public void Play(Vector3 start, Vector3 velocity, float flightTime)
        {
            GameObject grenade = grenadeModel != null ? Instantiate(grenadeModel) : CreateFallbackGrenade();
            grenade.AddComponent<GrenadeFlight>().Launch(start, velocity, flightTime, PlayExplosion);
        }

        /// <summary>
        /// 폭발 지점에서 이펙트와 소리를 재생한다.
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
            flash.shadows = LightShadows.None;

            GameObject sphere = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, blastMaterial);
            sphere.transform.SetParent(blast.transform, false);
            blast.AddComponent<GrenadeBlastAnimator>().Run(flash, sphere.transform, GameTuning.GrenadeRadius, BlastDuration);
        }

        /// <summary>
        /// 모델이 없을 때 쓸 작은 구를 만든다.
        /// </summary>
        private GameObject CreateFallbackGrenade()
        {
            GameObject sphere = CreatePrimitiveWithoutCollider(PrimitiveType.Sphere, grenadeMaterial);
            sphere.name = "Grenade";
            sphere.transform.localScale = Vector3.one * FallbackGrenadeSize;
            return sphere;
        }

        /// <summary>
        /// 충돌체 없는 기본 도형을 만든다. (연출 전용)
        /// </summary>
        private static GameObject CreatePrimitiveWithoutCollider(PrimitiveType type, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Destroy(primitive.GetComponent<Collider>());
            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null)
                renderer.sharedMaterial = material;
            return primitive;
        }

    }
}
