using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>번들이 게임 스크립트를 이름으로 붙여 악용하지 못하도록 맵에 필요한 컴포넌트만 허용한다.</summary>
    public static class ModComponentPolicy
    {
        private const string ContentAssemblyName = "GuardVsMannequin.Content";

        // 패키지 어셈블리를 참조하지 않아도 되도록 전체 이름으로 비교한다.
        private static readonly HashSet<string> AllowedTypeNames = new(StringComparer.Ordinal)
        {
            // 모양
            "UnityEngine.Transform", "UnityEngine.MeshFilter", "UnityEngine.MeshRenderer", "UnityEngine.SkinnedMeshRenderer",
            "UnityEngine.LODGroup", "UnityEngine.SpriteRenderer", "UnityEngine.LineRenderer", "UnityEngine.TrailRenderer",
            "UnityEngine.BillboardRenderer", "UnityEngine.Terrain", "UnityEngine.Tree", "UnityEngine.WindZone",
            // 물리
            "UnityEngine.BoxCollider", "UnityEngine.SphereCollider", "UnityEngine.CapsuleCollider", "UnityEngine.MeshCollider",
            "UnityEngine.TerrainCollider", "UnityEngine.Rigidbody",
            // 조명·렌더링
            "UnityEngine.Light", "UnityEngine.LightProbeGroup", "UnityEngine.LightProbeProxyVolume", "UnityEngine.ReflectionProbe",
            "UnityEngine.OcclusionArea", "UnityEngine.OcclusionPortal", "UnityEngine.Rendering.Volume",
            "UnityEngine.Rendering.LensFlareComponentSRP", "UnityEngine.Rendering.Universal.UniversalAdditionalLightData",
            "UnityEngine.Rendering.Universal.DecalProjector",
            // 애니메이션·소리·이펙트
            "UnityEngine.Animator", "UnityEngine.AudioSource", "UnityEngine.AudioReverbZone",
            "UnityEngine.ParticleSystem", "UnityEngine.ParticleSystemRenderer",
            // 길찾기
            "UnityEngine.AI.NavMeshObstacle", "Unity.AI.Navigation.NavMeshSurface", "Unity.AI.Navigation.NavMeshModifier",
            "Unity.AI.Navigation.NavMeshModifierVolume", "Unity.AI.Navigation.NavMeshLink"
        };

        /// <summary>
        /// 게임 로직 스크립트를 번들로 붙이면 모드가 게임 동작을 바꿀 수 있으므로, 맵 데이터만 담는 콘텐츠 어셈블리와 허용 목록의 엔진 컴포넌트만 통과시킨다.
        /// </summary>
        public static bool IsAllowed(Type componentType)
        {
            return componentType != null
                && (componentType.Assembly.GetName().Name == ContentAssemblyName || AllowedTypeNames.Contains(componentType.FullName));
        }

        /// <summary>꺼 둔 오브젝트도 나중에 켜질 수 있어 자식 전체를 검사하고 고칠 곳을 알 수 있게 모은다.</summary>
        public static void CollectViolations(GameObject root, ICollection<string> violations)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && !IsAllowed(component.GetType()))
                    violations.Add($"{component.gameObject.name}: {component.GetType().FullName}");
            }
        }
    }
}
