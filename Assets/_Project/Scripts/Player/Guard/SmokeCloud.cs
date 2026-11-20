using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>연막탄이 터진 자리에 정해진 시간 동안 머무는 연막 연출(효과 판정은 서버의 GrenadeSmokeZone이 맡는다).</summary>
    public static class SmokeCloud
    {
        private const int BurstCount = 24;
        private const int MaxParticles = 96;
        private const float MinLifetime = 2.2f;
        private const float MaxLifetime = 3f;
        private const float PeakAlpha = 0.6f;

        /// <summary>
        /// 이펙트 에셋 없이도 연막이 보이도록 파티클 시스템을 코드로 만들고, 다 사라지면 스스로 지워지게 한다.
        /// </summary>
        public static GameObject Spawn(Vector3 position, float radius, float duration, Material material, Color color)
        {
            var cloud = new GameObject("GrenadeSmoke");
            cloud.transform.position = position;
            // AddComponent 직후 바로 재생되면 설정 전 값으로 한 번 뿜으므로 꺼 둔 채 설정한다.
            cloud.SetActive(false);
            ParticleSystem particles = cloud.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float lifetime = Mathf.Min(MaxLifetime, duration);
            ConfigureMain(particles.main, radius, duration, lifetime, color);
            ConfigureEmission(particles.emission, lifetime);
            ConfigureShape(particles.shape, radius);
            ConfigureFade(particles.colorOverLifetime);
            ConfigureGrowth(particles.sizeOverLifetime);
            ConfigureSwirl(particles.rotationOverLifetime);
            ConfigureRenderer(cloud.GetComponent<ParticleSystemRenderer>(), material);

            cloud.SetActive(true);
            particles.Play();
            return cloud;
        }

        /// <summary>마지막에 뿜은 입자가 사라지는 순간이 연막 지속 시간 끝과 맞도록 방출 시간을 수명만큼 줄인다.</summary>
        private static void ConfigureMain(ParticleSystem.MainModule main, float radius, float duration, float lifetime, Color color)
        {
            main.duration = Mathf.Max(0.05f, duration - lifetime);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * (MinLifetime / MaxLifetime), lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.9f, radius * 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            // 연기가 아주 천천히 피어오르게 한다.
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;
            main.stopAction = ParticleSystemStopAction.Destroy;
        }

        /// <summary>섬광처럼 한 번에 퍼진 뒤 같은 짙기를 유지하도록 처음에 몰아서 뿜고 나머지는 꾸준히 채운다.</summary>
        private static void ConfigureEmission(ParticleSystem.EmissionModule emission, float lifetime)
        {
            emission.enabled = true;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, BurstCount) });
            emission.rateOverTime = BurstCount / Mathf.Max(0.5f, lifetime);
        }

        /// <summary>판정 반경 안쪽에서 생겨야 보이는 연막과 효과 범위가 어긋나지 않는다.</summary>
        private static void ConfigureShape(ParticleSystem.ShapeModule shape, float radius)
        {
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.55f;
        }

        /// <summary>입자가 갑자기 생기거나 사라지면 연기처럼 보이지 않아 짙기를 서서히 올리고 내린다.</summary>
        private static void ConfigureFade(ParticleSystem.ColorOverLifetimeModule colorOverLifetime)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(PeakAlpha, 0.12f), new GradientAlphaKey(PeakAlpha, 0.65f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;
        }

        /// <summary>연기가 퍼지는 느낌을 주려고 수명 동안 조금씩 커지게 한다.</summary>
        private static void ConfigureGrowth(ParticleSystem.SizeOverLifetimeModule sizeOverLifetime)
        {
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.15f));
        }

        /// <summary>같은 모양 입자가 겹쳐 보이지 않도록 천천히 돌린다.</summary>
        private static void ConfigureSwirl(ParticleSystem.RotationOverLifetimeModule rotationOverLifetime)
        {
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        }

        /// <summary>반투명 입자가 많아 그림자까지 그리면 비용만 크므로 끈다.</summary>
        private static void ConfigureRenderer(ParticleSystemRenderer particleRenderer, Material material)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortMode = ParticleSystemSortMode.Distance;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            if (material != null)
                particleRenderer.sharedMaterial = material;
            else
                Debug.LogWarning("[SmokeCloud] 연막 머티리얼이 비어 있습니다. 메뉴 \"GameSettingTools/미리 굽기/전체 실행\"으로 Guard 프리팹에 연결하세요.");
        }
    }
}
