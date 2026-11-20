using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Mods;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Player;
using GuardVsMannequin.Props;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>모드 맵이 게임에서 동작하는지 게임 코드 없이 확인할 수 있는 항목으로 내보내기 전에 검사한다.</summary>
    public static class ModMapValidator
    {
        /// <summary>검사 결과 한 줄.</summary>
        public readonly struct Issue
        {
            public readonly bool isError;
            public readonly string message;
            public readonly Object context;

            /// <summary>Console에서 누르면 문제 오브젝트로 갈 수 있도록 오브젝트를 함께 담는다.</summary>
            public Issue(bool isError, string message, Object context = null)
            {
                this.isError = isError;
                this.message = message;
                this.context = context;
            }
        }

        private const int RecommendedMannequinSpawns = GameConstants.MaxPlayers - 1;
        private const int RecommendedGenerators = 12;

        // 게임이 뼈 판정(Hitbox)에 쓰는 레이어 번호.
        private const int ReservedHitboxLayer = 8;

        /// <summary>게임에서 열어 봐야 알 수 있는 문제를 내보내기 전에 찾으며, MapInfo가 하나가 아니면 기준이 없어 바로 멈춘다.</summary>
        public static List<Issue> Validate(Scene scene)
        {
            var issues = new List<Issue>();
            List<T> FindInScene<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();

            List<MapInfo> mapInfos = FindInScene<MapInfo>();
            if (mapInfos.Count != 1)
            {
                issues.Add(new Issue(true, $"MapInfo가 {mapInfos.Count}개입니다. 하나만 있어야 합니다."));
                return issues;
            }
            MapInfo mapInfo = mapInfos[0];
            if (mapInfo.CeilingHeight >= GameConstants.GunRange)
                issues.Add(new Issue(true, $"천장 높이({mapInfo.CeilingHeight})가 {GameConstants.GunRange}보다 낮아야 합니다.", mapInfo));
            if (mapInfo.OverviewPoint == null)
                issues.Add(new Issue(false, "MapInfo에 대기 화면 위치(Overview Point)가 없습니다.", mapInfo));

            NavMeshSurface surface = mapInfo.GetComponentInChildren<NavMeshSurface>(true);
            if (surface == null || surface.navMeshData == null)
                issues.Add(new Issue(true, "MapInfo 오브젝트(또는 자식)에 NavMeshSurface가 없거나 굽지 않았습니다. NavMeshSurface의 Bake를 누르세요. (가드 스폰 후보)", mapInfo));

            List<SpawnPoint> spawnPoints = FindInScene<SpawnPoint>();
            int mannequinSpawnCount = spawnPoints.Count(spawnPoint => spawnPoint.Role == PlayerRole.Mannequin);
            if (mannequinSpawnCount == 0)
                issues.Add(new Issue(true, "마네킹 스폰 지점(SpawnPoint, Role=Mannequin)이 없습니다."));
            else if (mannequinSpawnCount < RecommendedMannequinSpawns)
                issues.Add(new Issue(false, $"마네킹 스폰 지점이 {mannequinSpawnCount}개입니다. {RecommendedMannequinSpawns}개 이상을 권장합니다."));
            if (spawnPoints.All(spawnPoint => spawnPoint.Role != PlayerRole.Guard))
                issues.Add(new Issue(false, "가드 스폰 후보(SpawnPoint, Role=Guard)가 없습니다. 바닥 무작위 위치만 씁니다."));

            int generatorCount = FindInScene<GeneratorSpawnPoint>().Count;
            if (generatorCount == 0)
                issues.Add(new Issue(true, "전원 장치 후보(GeneratorSpawnPoint)가 없습니다."));
            else if (generatorCount < RecommendedGenerators)
                issues.Add(new Issue(false, $"전원 장치 후보가 {generatorCount}개입니다. {RecommendedGenerators}개를 권장합니다."));

            // 게임이 번들을 불러올 때도 같은 규칙으로 검사하므로, 걸리는 맵은 게임에서 열리지 않는다.
            var violations = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                ModComponentPolicy.CollectViolations(root, violations);
            foreach (CharacterSkin skin in new[] { mapInfo.MannequinSkin, mapInfo.GuardSkin, mapInfo.MannequinHuntSkin })
            {
                if (skin != null && skin.modelPrefab != null)
                    ModComponentPolicy.CollectViolations(skin.modelPrefab, violations);
                if (skin != null && skin.fireAnimationEvents)
                    issues.Add(new Issue(false, $"외형 '{skin.name}'의 Fire Animation Events는 모드에서 항상 꺼집니다.", skin));
            }
            AddGuardFlashlightNotice(mapInfo.GuardSkin, issues);
            foreach (string violation in violations)
                issues.Add(new Issue(true, $"허용되지 않은 컴포넌트입니다 - {violation}. 맵에는 환경·지점·사물·조명만 둡니다. (카메라·네트워크 오브젝트·UI·게임 스크립트 불가)"));

            if (FindInScene<Transform>().Any(sceneTransform => sceneTransform.gameObject.layer == ReservedHitboxLayer))
                issues.Add(new Issue(false, $"레이어 {ReservedHitboxLayer}번 오브젝트가 있습니다. 게임이 쓰는 레이어라 충돌이 동작하지 않습니다. 다른 레이어로 옮기세요."));

            List<Light> lights = FindInScene<Light>();
            int unregisteredLightCount = lights.Count(sceneLight => sceneLight.GetComponentInParent<MapLight>() == null);
            if (unregisteredLightCount > 0)
                issues.Add(new Issue(false, $"MapLight가 아닌 조명이 {unregisteredLightCount}개 있습니다. 사냥 시간에 꺼지지 않습니다."));

            List<TransformableProp> props = FindInScene<TransformableProp>();
            if (props.Count == 0)
                issues.Add(new Issue(false, "변신 가능 사물(TransformableProp)이 없습니다."));
            foreach (TransformableProp prop in props)
            {
                if (PropCopyChecks.TryFindProblem(prop, out string problem, out bool isError))
                    issues.Add(new Issue(isError, $"'{prop.name}': {problem}", prop));
            }
            return issues;
        }

        /// <summary>
        /// 손전등 위치는 모델의 Flashlight 지점을 따르므로, 지점이 없거나 부품이 붙어 있으면 제작자가 알 수 있게 안내한다.
        /// </summary>
        private static void AddGuardFlashlightNotice(CharacterSkin guardSkin, List<Issue> issues)
        {
            if (guardSkin == null || guardSkin.modelPrefab == null)
                return;

            Transform point = guardSkin.modelPrefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == FlashlightLook.MountPointName);
            if (point == null)
            {
                issues.Add(new Issue(false, $"가드 외형 '{guardSkin.name}'에 \"{FlashlightLook.MountPointName}\" 지점이 없어 손전등이 기본 위치(눈앞 오른쪽)에서 나갑니다. " +
                                            "헬멧·어깨·총 등에 같은 이름의 빈 오브젝트를 두면 그 위치에서 나갑니다.", guardSkin));
                return;
            }
            if (point.GetComponents<Component>().Length > 1)
                issues.Add(new Issue(false, $"가드 외형 '{guardSkin.name}'의 \"{FlashlightLook.MountPointName}\" 지점에 부품이 붙어 있습니다. 위치만 쓰므로 빈 오브젝트로 두세요. (빛은 게임이 만듭니다)", point));
        }
    }
}
