using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Props;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>판을 열어 보기 전에 열린 맵 씬이 게임에 필요한 것을 갖췄는지 검사한다.</summary>
    public static class MapValidator
    {
        /// <summary>검사 결과 한 줄.</summary>
        public readonly struct Issue
        {
            public readonly bool isError;
            public readonly string message;
            public readonly Object context;

            /// <summary>도우미 창의 "선택" 버튼이 문제 오브젝트로 갈 수 있도록 오브젝트를 함께 담는다.</summary>
            public Issue(bool isError, string message, Object context = null)
            {
                this.isError = isError;
                this.message = message;
                this.context = context;
            }
        }

        // 최대 인원 기준 필요한 지점 수.
        private const int MaxGuards = 5;
        private const int MaxMannequins = GameConstants.MaxPlayers - 1;
        private const int MaxGenerators = 12;

        /// <summary>전원 장치 후보로부터 이 거리 안의 사물은 주변 구조물로 보고 변신 대상에서 빼길 권한다.</summary>
        private const float GeneratorSurroundRadius = 1.5f;

        /// <summary>판을 열어 봐야 알 수 있는 문제를 미리 찾으며, MapInfo가 하나가 아니면 기준이 없어 바로 멈춘다.</summary>
        public static List<Issue> Validate()
        {
            var issues = new List<Issue>();
            Scene scene = SceneManager.GetActiveScene();

            MapInfo[] mapInfos = Object.FindObjectsByType<MapInfo>(FindObjectsInactive.Include);
            if (mapInfos.Length != 1)
            {
                issues.Add(new Issue(true, $"MapInfo가 {mapInfos.Length}개입니다. Map 오브젝트에 하나만 있어야 합니다."));
                return issues;
            }
            MapInfo mapInfo = mapInfos[0];
            if (mapInfo.CeilingHeight >= GameConstants.GunRange)
                issues.Add(new Issue(true, $"천장 높이({mapInfo.CeilingHeight})가 가드 사거리({GameConstants.GunRange})보다 낮아야 합니다.", mapInfo));

            NavMeshSurface surface = mapInfo.GetComponent<NavMeshSurface>();
            if (surface == null)
                issues.Add(new Issue(true, "Map에 NavMeshSurface가 없습니다. 가드 스폰 후보를 맵 안에서 고를 수 없습니다.", mapInfo));
            else if (surface.navMeshData == null)
                issues.Add(new Issue(false, "NavMesh를 미리 굽지 않았습니다. 빌드에서는 에셋 모델 메시를 실행 중에 구울 수 없어 가드 스폰 후보가 불완전해집니다. '현재 맵 최적화'를 실행하세요.", mapInfo));
            if (mapInfo.transform.Find(MapAuthoringTools.EnvironmentName) == null)
                issues.Add(new Issue(false, "Map/Environment가 없습니다. 맵 최적화와 경계 맞추기는 이 오브젝트 아래 모델을 대상으로 합니다.", mapInfo));

            CheckSpawns(issues);
            CheckGuardSpawns(issues);
            CheckLights(issues);
            CheckProps(issues, mapInfo);

            if (scene.GetRootGameObjects().All(rootObject => rootObject.name != MapAuthoringTools.BoundaryName))
                issues.Add(new Issue(false, "투명 경계(Boundary (Invisible))가 없습니다. 벽을 타고 맵 밖으로 나갈 수 있습니다. '경계 맞추기'를 실행하세요."));

            if (!EditorBuildSettings.scenes.Any(buildScene => buildScene.path == scene.path && buildScene.enabled) || MapCatalog.FindByScenePath(scene.path) == null)
                issues.Add(new Issue(false, "맵 목록에 등록되지 않았습니다. 직접 만든 맵은 Scenes/Custom 폴더에 두고 '맵 목록 갱신'을 실행하세요."));

            // 공용 씬에 있는 시스템이 맵 씬에도 있으면 판 중에 두 벌이 된다.
            if (Object.FindObjectsByType<FishNet.Object.NetworkObject>(FindObjectsInactive.Include).Length > 0)
                issues.Add(new Issue(true, "맵 씬에 네트워크 오브젝트(예전 GameSystems 등)가 있습니다. 게임 시스템은 공용 씬(Game_Systems)에 있으므로 지우세요."));
            if (Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Length > 0)
                issues.Add(new Issue(false, "맵 씬에 카메라가 있습니다. 씬 카메라는 공용 게임 시스템 씬에 있으므로 지우세요. (대기 화면 위치는 MapInfo의 Overview Point)"));
            if (mapInfo.OverviewPoint == null)
                issues.Add(new Issue(false, "MapInfo에 대기 화면 위치(Overview Point)가 없습니다. 대기·결과 화면에서 카메라가 기본 위치에 있습니다.", mapInfo));

            return issues;
        }

        /// <summary>지점이 최대 인원보다 적으면 같은 자리에 겹쳐 나오므로 최대 인원 기준으로 센다.</summary>
        private static void CheckSpawns(List<Issue> issues)
        {
            SpawnPoint[] spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
            int guardSpawnCount = spawnPoints.Count(spawnPoint => spawnPoint.Role == PlayerRole.Guard);
            int mannequinSpawnCount = spawnPoints.Count(spawnPoint => spawnPoint.Role == PlayerRole.Mannequin);
            if (guardSpawnCount == 0)
                issues.Add(new Issue(true, "가드 스폰 지점이 없습니다."));
            else if (guardSpawnCount < MaxGuards)
                issues.Add(new Issue(false, $"가드 스폰 지점이 {guardSpawnCount}개입니다. 최대 인원({MaxGuards}명)보다 적으면 같은 지점에 겹쳐 나옵니다."));

            if (mannequinSpawnCount == 0)
                issues.Add(new Issue(true, "마네킹 스폰 지점이 없습니다."));
            else if (mannequinSpawnCount < MaxMannequins)
                issues.Add(new Issue(false, $"마네킹 스폰 지점이 {mannequinSpawnCount}개입니다. 최대 인원({MaxMannequins}명)보다 적으면 같은 지점에 겹쳐 나옵니다."));

            int generatorCount = Object.FindObjectsByType<GeneratorSpawnPoint>(FindObjectsInactive.Include).Length;
            if (generatorCount == 0)
                issues.Add(new Issue(true, "전원 장치 후보가 없습니다."));
            else if (generatorCount < MaxGenerators)
                issues.Add(new Issue(false, $"전원 장치 후보가 {generatorCount}개입니다. 룸 설정 최댓값({MaxGenerators}개)보다 적으면 그 수까지만 나옵니다."));
        }

        /// <summary>가드 스폰 후보가 없으면 바닥 무작위 위치만 쓰게 되어 맵 제작자가 의도한 자리가 무시되므로 알려 준다.</summary>
        private static void CheckGuardSpawns(List<Issue> issues)
        {
            SpawnPoint[] spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
            if (!spawnPoints.Any(spawnPoint => spawnPoint.Role == PlayerRole.Guard))
                issues.Add(new Issue(false, "가드 스폰 후보가 없습니다. 바닥(NavMesh) 무작위 위치만 씁니다."));
        }

        /// <summary>
        /// 사냥 시간의 어둠은 MapLight만 끌 수 있고, 구운 조명은 실행 중에 끌 수 없어 사냥 시간에도 밝게 남으므로 둘 다 확인한다.
        /// </summary>
        private static void CheckLights(List<Issue> issues)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            int unregisteredCount = lights.Count(sceneLight => sceneLight.GetComponentInParent<MapLight>() == null);
            if (lights.Length == 0)
                issues.Add(new Issue(false, "맵에 조명이 없습니다."));
            else if (unregisteredCount > 0)
                issues.Add(new Issue(false, $"MapLight가 아닌 조명이 {unregisteredCount}개 있습니다. 사냥 시간에 꺼지지 않습니다. '모든 조명 등록'을 실행하세요."));

            int bakedCount = lights.Count(sceneLight => sceneLight.lightmapBakeType == LightmapBakeType.Baked);
            if (bakedCount > 0)
                issues.Add(new Issue(false, $"구운(Baked) 조명이 {bakedCount}개 있습니다. 라이트맵을 굽지 않으면 빛이 없고, 구우면 사냥 시간에도 밝게 남습니다."));
        }

        /// <summary>저장 전 상태에 속지 않도록 번호를 다시 매긴 뒤 검사한다.</summary>
        private static void CheckProps(List<Issue> issues, MapInfo mapInfo)
        {
            TransformableProp[] props = Object.FindObjectsByType<TransformableProp>(FindObjectsInactive.Include);
            if (props.Length == 0)
            {
                issues.Add(new Issue(false, "변신 가능 사물이 없습니다. 오브젝트를 선택하고 '변신 사물로 등록'을 실행하세요."));
                return;
            }

            if (PropIndexer.AssignIndices(mapInfo.gameObject.scene) > 0)
                issues.Add(new Issue(false, "변신 사물 번호를 다시 매겼습니다. 씬을 저장하세요."));

            GeneratorSpawnPoint[] generatorPoints = Object.FindObjectsByType<GeneratorSpawnPoint>(FindObjectsInactive.Include);
            foreach (TransformableProp prop in props)
            {
                if (PropCopyChecks.TryFindProblem(prop, out string problem, out bool isError))
                    issues.Add(new Issue(isError, $"'{prop.name}': {problem}", prop));
                else if (TryFindPlacementProblem(prop, generatorPoints, out string placementProblem))
                    issues.Add(new Issue(false, $"'{prop.name}': {placementProblem}", prop));
            }
        }

        /// <summary>전원 장치·천장·바닥으로 변신하면 목표가 사라지거나 바닥이 꺼지므로 막는다.</summary>
        private static bool TryFindPlacementProblem(TransformableProp prop, GeneratorSpawnPoint[] generatorPoints, out string message)
        {
            message = null;
            if (prop.GetComponentInParent<Generator>() != null)
                message = "전원 장치는 변신 대상이 될 수 없습니다. 등록을 해제하세요.";
            else if (IsFloorOrCeiling(prop.name))
                message = "천장과 바닥은 변신 대상이 될 수 없습니다. 등록을 해제하세요.";
            else if (IsNearGenerator(prop, generatorPoints))
                message = $"전원 장치 후보 {GeneratorSurroundRadius}m 안의 구조물입니다. 전원 장치를 가릴 수 있으니 등록 해제를 권장합니다.";
            return message != null;
        }

        /// <summary>천장·바닥을 표시하는 컴포넌트가 없어 모델 이름 규칙으로 판단한다.</summary>
        private static bool IsFloorOrCeiling(string objectName)
        {
            string lowerName = objectName.ToLowerInvariant();
            return lowerName.Contains("floor") || lowerName.Contains("ceiling");
        }

        /// <summary>큰 사물은 기준점이 멀어도 표면이 장치에 닿을 수 있어 충돌체의 가장 가까운 점으로 잰다.</summary>
        private static bool IsNearGenerator(TransformableProp prop, GeneratorSpawnPoint[] generatorPoints)
        {
            Collider propCollider = prop.GetComponent<Collider>();
            foreach (GeneratorSpawnPoint generatorPoint in generatorPoints)
            {
                Vector3 closestPoint = propCollider != null ? propCollider.ClosestPoint(generatorPoint.transform.position) : prop.transform.position;
                if (Vector3.Distance(closestPoint, generatorPoint.transform.position) <= GeneratorSurroundRadius)
                    return true;
            }
            return false;
        }
    }
}
