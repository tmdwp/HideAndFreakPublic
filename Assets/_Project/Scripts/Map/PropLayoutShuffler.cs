using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Core;
using GuardVsMannequin.Objectives;
using GuardVsMannequin.Props;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Map
{
    /// <summary>
    /// 맵 씬을 불러오면 변신 사물 배치를 판마다 바꾼다. (모든 PC가 각자 같은 계산을 하므로 위치를 따로 보내지 않음)
    /// 시드는 호스트가 판을 시작할 때 정해 룸으로 보낸다(<see cref="IRoomInfo.PropLayoutSeed"/>). 계산은 <see cref="PropLayoutPlanner"/>가 한다.
    /// 1. 크기가 비슷한 사물끼리 자리를 바꾼다.
    /// 2. 사물마다 조금 옮기고 돌려 본 뒤, 다른 물체와 겹치거나 받침이 없거나 스폰 지점을 막으면 돌리기만, 그것도 안 되면 자리만 바꾼 상태로 둔다.
    /// 벽 조각으로 변신하는 사물(IsWall)과 너무 커서 변신할 수 없는 사물은 옮기지 않는다.
    /// 플레이어·전원 장치는 이보다 나중에 스폰되므로 검사에 들어가지 않는다.
    /// </summary>
    public static class PropLayoutShuffler
    {
        // 겹침 검사 상자를 사방으로 줄이는 여유(m). 받침 면이나 살짝 닿은 이웃은 겹침으로 치지 않는다.
        private const float OverlapMargin = 0.05f;
        // 받침 검사: 바닥 가운데 점 위에서 아래로 쏘는 광선
        private const float SupportProbeHeight = 0.2f;
        private const float SupportProbeDepth = 0.3f;
        // 스폰 지점(플레이어·전원 장치)에서 사물 가장자리까지 비워 둘 거리(m)와, 다른 층으로 보는 높이 차이(m)
        private const float SpawnClearance = 0.5f;
        private const float SpawnFloorTolerance = 2f;

        private static readonly Collider[] overlapBuffer = new Collider[32];
        private static readonly RaycastHit[] rayBuffer = new RaycastHit[16];

        /// <summary>
        /// 옮길 사물 하나와 원래 상태.
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

        /// <summary>
        /// 판 중에 맵 씬이 열리면 배치를 섞는다. 룸 없이 맵 씬만 실행하면 섞지 않는다. (맵 씬 준비 순서는 <see cref="MapPropSetup"/>)
        /// </summary>
        public static void ShuffleForMatch(Scene scene)
        {
            IRoomInfo room = GameServices.Room;
            if (room == null || room.PropLayoutSeed == 0 || !GameTuning.PropShuffleEnabled)
                return;

            Shuffle(scene, room.PropLayoutSeed);
        }

        /// <summary>
        /// 씬의 변신 사물 배치를 시드로 섞는다.
        /// </summary>
        private static void Shuffle(Scene scene, int seed)
        {
            List<MovableProp> props = CollectProps(scene);
            if (props.Count < 2)
                return;

            PropLayoutPlanner.Spot[] spots = props.Select(movable => movable.spot).ToArray();
            PropLayoutPlanner.Placement[] plan = PropLayoutPlanner.Plan(spots, seed,
                GameTuning.PropShuffleSizeTolerance, GameTuning.PropJitterDistance, GameTuning.PropJitterRotation);
            List<Vector3> spawnPositions = CollectSpawnPositions(scene);

            // 1단계: 모두 바뀐 자리로 옮긴다. (흩기 검사가 아직 떠나지 않은 사물에 걸리지 않도록 먼저 끝낸다)
            for (int i = 0; i < props.Count; i++)
                Place(props[i], spots[plan[i].spot].bottomCenter, 0f);
            Physics.SyncTransforms();

            // 2단계: 조금 옮기고 돌린다. 안 되면 돌리기만, 그것도 안 되면 자리만 바꾼 상태.
            int moved = 0, scattered = 0;
            for (int i = 0; i < props.Count; i++)
            {
                if (plan[i].spot != i)
                    moved++;
                Vector3 bottom = spots[plan[i].spot].bottomCenter;
                if (TryPlace(props[i], bottom + plan[i].jitter, plan[i].yaw, true, spawnPositions)
                    || TryPlace(props[i], bottom, plan[i].yaw, false, spawnPositions))
                {
                    scattered++;
                    continue;
                }
                Place(props[i], bottom, 0f);
                Physics.SyncTransforms();
            }

            Debug.Log($"[PropLayoutShuffler] {scene.name}: 변신 사물 {props.Count}개 중 {moved}개 자리 바꿈, {scattered}개 흩음/돌림 (시드 {seed})");
        }

        /// <summary>
        /// 옮길 변신 사물을 번호순으로 모은다. 벽 조각·변신 불가 사물·다른 변신 사물 안에 든 사물은 뺀다.
        /// </summary>
        private static List<MovableProp> CollectProps(Scene scene)
        {
            var result = new List<MovableProp>();
            IEnumerable<TransformableProp> candidates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TransformableProp>())
                .Where(prop => !prop.IsWall && prop.CanDisguiseAs)
                .Where(prop => prop.transform.parent == null || prop.transform.parent.GetComponentInParent<TransformableProp>() == null)
                .OrderBy(prop => prop.Index);

            foreach (TransformableProp prop in candidates)
            {
                Renderer[] renderers = prop.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                if (renderers.Length == 0)
                    continue;
                // 정적 배칭에 묶인 메시는 옮겨도 화면에서 그대로이므로 옮기지 않는다. (맵 최적화가 변신 사물은 배칭에서 뺌)
                if (renderers.Any(r => r.isPartOfStaticBatch))
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
        /// 비워 둘 스폰 지점(플레이어 스폰, 전원 장치 자리) 위치를 모은다.
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
        /// 겉모습 바닥 가운데 점이 bottom에 오도록, 원래 방향에서 세로축으로 yaw만큼 돌려 놓는다.
        /// </summary>
        private static void Place(MovableProp movable, Vector3 bottom, float yaw)
        {
            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up);
            movable.prop.transform.SetPositionAndRotation(bottom + turn * movable.pivotOffset, turn * movable.originalRotation);
        }

        /// <summary>
        /// 놓아 보고 쓸 수 있는 자리인지 검사한다. 안 되면 false. (사물은 놓은 상태로 남으므로 호출한 쪽이 다음 후보를 놓는다)
        /// </summary>
        /// <param name="needSupport">자리에서 옮겼으면 true. 받침(바닥·탁자 등) 위에 있는지도 확인한다.</param>
        private static bool TryPlace(MovableProp movable, Vector3 bottom, float yaw, bool needSupport, List<Vector3> spawnPositions)
        {
            Place(movable, bottom, yaw);
            Physics.SyncTransforms();

            Bounds bounds = GetBounds(movable.renderers);
            return IsClear(movable, bounds)
                   && (!needSupport || IsSupported(movable, bounds))
                   && IsAwayFromSpawns(bounds, spawnPositions);
        }

        /// <summary>
        /// 겉모습 상자(조금 줄인 것)가 자기 것이 아닌 콜라이더와 겹치지 않는지.
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
        /// 바닥 가운데 점 바로 아래에 받쳐 주는 물체가 있는지. (탁자 끝에서 밀려나 허공에 뜨는 것 방지)
        /// </summary>
        private static bool IsSupported(MovableProp movable, Bounds bounds)
        {
            var origin = new Vector3(bounds.center.x, bounds.min.y + SupportProbeHeight, bounds.center.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, rayBuffer, SupportProbeHeight + SupportProbeDepth,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!movable.ownColliders.Contains(rayBuffer[i].collider))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 같은 층의 스폰 지점이 사물 가장자리에서 일정 거리 밖에 있는지.
        /// </summary>
        private static bool IsAwayFromSpawns(Bounds bounds, List<Vector3> spawnPositions)
        {
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + SpawnClearance;
            foreach (Vector3 spawn in spawnPositions)
            {
                if (Mathf.Abs(spawn.y - bounds.min.y) > SpawnFloorTolerance)
                    continue;
                var offset = new Vector2(spawn.x - bounds.center.x, spawn.z - bounds.center.z);
                if (offset.sqrMagnitude < radius * radius)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 렌더러들의 겉모습 상자를 합친다.
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
