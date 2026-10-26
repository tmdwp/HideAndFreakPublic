using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Props;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 모드 맵 씬이 게임에서 동작하는지 검사한다. (모드 SDK의 "모드 내보내기" 전에 실행)
    /// 오류가 하나라도 있으면 내보내지 않는다. 경고는 동작은 하지만 확인이 필요한 것이다.
    /// 게임 프로젝트의 맵 검사와 달리 게임 코드(네트워크·HUD)를 몰라도 되는 항목만 본다.
    /// </summary>
    public static class ModMapValidator
    {
        /// <summary>검사 결과 한 줄.</summary>
        public readonly struct Issue
        {
            public readonly bool isError;
            public readonly string message;
            public readonly Object context;

            public Issue(bool isError, string message, Object context = null)
            {
                this.isError = isError;
                this.message = message;
                this.context = context;
            }
        }

        private const int RecommendedMannequinSpawns = GameConstants.MaxPlayers - 1;
        private const int RecommendedGenerators = 12;

        /// <summary>
        /// 열린 씬 하나를 검사한다.
        /// </summary>
        public static List<Issue> Validate(Scene scene)
        {
            var issues = new List<Issue>();
            List<T> Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToList();

            List<MapInfo> mapInfos = Find<MapInfo>();
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

            List<SpawnPoint> spawns = Find<SpawnPoint>();
            int mannequins = spawns.Count(s => s.Role == PlayerRole.Mannequin);
            if (mannequins == 0)
                issues.Add(new Issue(true, "마네킹 스폰 지점(SpawnPoint, Role=Mannequin)이 없습니다."));
            else if (mannequins < RecommendedMannequinSpawns)
                issues.Add(new Issue(false, $"마네킹 스폰 지점이 {mannequins}개입니다. {RecommendedMannequinSpawns}개 이상을 권장합니다."));
            if (spawns.All(s => s.Role != PlayerRole.Guard))
                issues.Add(new Issue(false, "가드 스폰 후보(SpawnPoint, Role=Guard)가 없습니다. 바닥 무작위 위치만 씁니다."));

            int generators = Find<GeneratorSpawnPoint>().Count;
            if (generators == 0)
                issues.Add(new Issue(true, "전원 장치 후보(GeneratorSpawnPoint)가 없습니다."));
            else if (generators < RecommendedGenerators)
                issues.Add(new Issue(false, $"전원 장치 후보가 {generators}개입니다. {RecommendedGenerators}개를 권장합니다."));

            // 게임 시스템·카메라는 게임이 붙이므로 맵에 두면 안 된다. (FishNet을 참조하지 않으려고 이름으로 확인)
            if (Find<Component>().Any(c => c != null && c.GetType().FullName == "FishNet.Object.NetworkObject"))
                issues.Add(new Issue(true, "맵 씬에 네트워크 오브젝트가 있습니다. 맵에는 환경·지점·사물·조명만 둡니다."));
            if (Find<Camera>().Count > 0)
                issues.Add(new Issue(false, "맵 씬에 카메라가 있습니다. 게임이 카메라를 붙이므로 지우세요."));

            List<Light> lights = Find<Light>();
            int unregistered = lights.Count(l => l.GetComponentInParent<MapLight>() == null);
            if (unregistered > 0)
                issues.Add(new Issue(false, $"MapLight가 아닌 조명이 {unregistered}개 있습니다. 사냥 시간에 꺼지지 않습니다."));

            List<TransformableProp> props = Find<TransformableProp>();
            if (props.Count == 0)
                issues.Add(new Issue(false, "변신 가능 사물(TransformableProp)이 없습니다."));
            foreach (TransformableProp prop in props)
            {
                if (PropCopyChecks.TryFindProblem(prop, out string problem, out bool isError))
                    issues.Add(new Issue(isError, $"'{prop.name}': {problem}", prop));
            }
            return issues;
        }
    }
}
