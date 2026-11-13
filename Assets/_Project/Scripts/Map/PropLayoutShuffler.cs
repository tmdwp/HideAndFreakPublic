using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Props;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Map
{
    /// <summary>위치를 따로 보내지 않도록 모든 PC가 같은 시드로 판마다 변신 사물 배치를 바꾼다.</summary>
    public static class PropLayoutShuffler
    {
        // 살짝 닿은 이웃은 겹침으로 치지 않도록 검사 상자를 줄이는 여유(m).
        private const float OverlapMargin = 0.05f;
        // 받침 검사: 바닥 가운데 점 위에서 아래로 쏘는 광선
        private const float SupportProbeHeight = 0.2f;
        private const float SupportProbeDepth = 0.3f;
        // 스폰 지점에서 비워 둘 거리와 다른 층으로 보는 높이 차이(m).
        private const float SpawnClearance = 0.5f;
        private const float SpawnFloorTolerance = 2f;

        // 검사마다 배열을 새로 만들지 않도록 재사용한다.
        private static readonly Collider[] overlapBuffer = new Collider[32];
        private static readonly RaycastHit[] rayBuffer = new RaycastHit[16];

        /// <summary>
        /// 실패하면 원래 방향 기준으로 다시 놓아야 해서, 옮길 사물과 원래 상태를 함께 기억한다.
        /// </summary>
        private sealed class MovableProp
        {
            public TransformableProp prop;
            public Renderer[] renderers;
            public HashSet<Collider> ownColliders;
            public Quaternion originalRotation;
            // 겉모습 상자의 바닥 가운데 점 → 사물 기준점 (원래 방향 기준)
            public Vector3 pivotOffset;
            public PropLayoutPlanner.Spot spot;
        }

        /// <summary>맵 씬만 열어 편집·테스트할 때 헷갈리지 않도록 룸이 없으면 섞지 않는다.</summary>
        public static void ShuffleForMatch(Scene scene)
        {
            IRoomInfo room = GameServices.Room;
            if (room == null || room.PropLayoutSeed == 0 || !GameTuning.PropShuffleEnabled)
                return;

            Shuffle(scene, room.PropLayoutSeed);
        }

        /// <summary>떠나지 않은 사물에 걸리지 않도록 먼저 모두 옮긴 뒤 하나씩 흩고, 안 되면 단계적으로 물러난다.</summary>
        private static void Shuffle(Scene scene, int seed)
        {
            List<MovableProp> props = CollectProps(scene);
            if (props.Count < 2)
                return;

            PropLayoutPlanner.Spot[] spots = props.Select(movable => movable.spot).ToArray();
            PropLayoutPlanner.Placement[] plan = PropLayoutPlanner.Plan(spots, seed,
                GameTuning.PropShuffleSizeTolerance, GameTuning.PropJitterDistance, GameTuning.PropJitterRotation);
            List<Vector3> spawnPositions = CollectSpawnPositions(scene);

            for (int i = 0; i < props.Count; i++)
                Place(props[i], spots[plan[i].spot].bottomCenter, 0f);
            Physics.SyncTransforms();

            int movedCount = 0, scatteredCount = 0;
            for (int i = 0; i < props.Count; i++)
            {
                if (plan[i].spot != i)
                    movedCount++;
                Vector3 bottomCenter = spots[plan[i].spot].bottomCenter;
                if (TryPlace(props[i], bottomCenter + plan[i].jitter, plan[i].yaw, true, spawnPositions)
                    || TryPlace(props[i], bottomCenter, plan[i].yaw, false, spawnPositions))
                {
                    scatteredCount++;
                    continue;
                }
                Place(props[i], bottomCenter, 0f);
                Physics.SyncTransforms();
            }

            Debug.Log($"[PropLayoutShuffler] {scene.name}: 변신 사물 {props.Count}개 중 {movedCount}개 자리 바꿈, {scatteredCount}개 흩음/돌림 (시드 {seed})");
        }

        /// <summary>모든 PC가 같은 순서로 계산하도록 번호순으로 모으고, 옮기면 맵이 깨지는 사물은 뺀다.</summary>
        private static List<MovableProp> CollectProps(Scene scene)
        {
            var result = new List<MovableProp>();
            IEnumerable<TransformableProp> candidates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TransformableProp>())
                .Where(prop => prop.CanDisguiseAs)
                .Where(prop => prop.transform.parent == null || prop.transform.parent.GetComponentInParent<TransformableProp>() == null)
                .OrderBy(prop => prop.Index);

            foreach (TransformableProp prop in candidates)
            {
                Renderer[] renderers = prop.GetComponentsInChildren<Renderer>().Where(propRenderer => propRenderer.enabled && !(propRenderer is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0)
                    continue;
                // 정적 배칭된 메시는 옮겨도 화면에서 그대로이므로 옮기지 않는다.
                if (renderers.Any(propRenderer => propRenderer.isPartOfStaticBatch))
                {
                    Debug.LogWarning($"[PropLayoutShuffler] '{prop.name}'이 정적 배칭에 묶여 있어 옮기지 않습니다. 맵 최적화를 다시 실행하세요.", prop);
                    continue;
                }

                Bounds bounds = GetBounds(renderers);
                var bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                result.Add(new MovableProp
                {
                    prop = prop,
                    renderers = renderers,
                    ownColliders = new HashSet<Collider>(prop.GetComponentsInChildren<Collider>()),
                    originalRotation = prop.transform.rotation,
                    pivotOffset = prop.transform.position - bottomCenter,
                    spot = new PropLayoutPlanner.Spot(prop.Index, bottomCenter, bounds.size)
                });
            }
            return result;
        }

        /// <summary>
        /// 플레이어·전원 장치는 섞은 뒤에 스폰되어 겹침 검사에 걸리지 않으므로, 그 자리를 막지 않도록 위치를 따로 모은다.
        /// </summary>
        private static List<Vector3> CollectSpawnPositions(Scene scene)
        {
            var result = new List<Vector3>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<SpawnPoint>(true).Select(point => point.transform.position));
                result.AddRange(root.GetComponentsInChildren<GeneratorSpawnPoint>(true).Select(point => point.transform.position));
            }
            return result;
        }

        /// <summary>
        /// 사물마다 기준점 위치가 달라서, 겉모습 바닥 가운데 점이 목표에 오도록 기준점 오프셋을 돌려 함께 옮긴다.
        /// </summary>
        private static void Place(MovableProp movable, Vector3 bottomCenter, float yaw)
        {
            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up);
            movable.prop.transform.SetPositionAndRotation(bottomCenter + turn * movable.pivotOffset, turn * movable.originalRotation);
        }

        /// <summary>실제 경계로 검사하려면 놓아 봐야 하므로 먼저 놓고 검사한다.</summary>
        /// <param name="needSupport">자리에서 옮겼으면 받침 밖으로 밀려났을 수 있어서 받침 검사도 한다.</param>
        private static bool TryPlace(MovableProp movable, Vector3 bottomCenter, float yaw, bool needSupport, List<Vector3> spawnPositions)
        {
            Place(movable, bottomCenter, yaw);
            Physics.SyncTransforms();

            Bounds bounds = GetBounds(movable.renderers);
            return IsClear(movable, bounds)
                   && (!needSupport || IsSupported(movable, bounds))
                   && IsAwayFromSpawns(bounds, spawnPositions);
        }

        /// <summary>
        /// 받침 면이나 살짝 닿은 이웃까지 겹침으로 치면 거의 모든 자리가 실패하므로 상자를 조금 줄여서 본다.
        /// </summary>
        private static bool IsClear(MovableProp movable, Bounds bounds)
        {
            Vector3 halfExtents = Vector3.Max(bounds.extents - Vector3.one * OverlapMargin, Vector3.one * 0.01f);
            int count = Physics.OverlapBoxNonAlloc(bounds.center + Vector3.up * OverlapMargin, halfExtents, overlapBuffer,
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!movable.ownColliders.Contains(overlapBuffer[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 탁자 끝에서 밀려나 허공에 뜨지 않도록 바닥 가운데 점 바로 아래에 받쳐 주는 물체가 있는지 본다.
        /// </summary>
        private static bool IsSupported(MovableProp movable, Bounds bounds)
        {
            var probeOrigin = new Vector3(bounds.center.x, bounds.min.y + SupportProbeHeight, bounds.center.z);
            int count = Physics.RaycastNonAlloc(probeOrigin, Vector3.down, rayBuffer, SupportProbeHeight + SupportProbeDepth,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!movable.ownColliders.Contains(rayBuffer[i].collider))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 위·아래층 스폰 지점까지 막는 것으로 보면 다층 맵에서 거의 못 옮기므로, 같은 층의 스폰 지점만 거리로 비교한다.
        /// </summary>
        private static bool IsAwayFromSpawns(Bounds bounds, List<Vector3> spawnPositions)
        {
            float clearRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) + SpawnClearance;
            foreach (Vector3 spawnPosition in spawnPositions)
            {
                if (Mathf.Abs(spawnPosition.y - bounds.min.y) > SpawnFloorTolerance)
                    continue;
                var offset = new Vector2(spawnPosition.x - bounds.center.x, spawnPosition.z - bounds.center.z);
                if (offset.sqrMagnitude < clearRadius * clearRadius)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 여러 조각으로 된 사물도 하나의 겉모습으로 다루려고 렌더러 경계를 합친다.
        /// </summary>
        private static Bounds GetBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
