using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>네트워크 없이 시험할 수 있도록 서버의 사물 물리 진행 규칙만 맡는다.</summary>
    public sealed class PropPhysicsSimulation
    {
        // 계속 굴러다니지 않도록 이보다 오래 움직이면 멈추는 시간(초).
        private const float MaxActiveSeconds = 10f;

        /// <summary>
        /// 멈춤·시간 초과를 판단하려면 깨운 시각과 느려진 시간이 필요해서 사물과 함께 기억한다.
        /// </summary>
        private sealed class Active
        {
            public PropBody body;
            public float activatedTime;
            public float slowTime;
        }

        // 가장 오래된 것을 먼저 멈추려고 깨운 순서대로 둔다.
        private readonly List<Active> active = new();

        public int ActiveCount => active.Count;

        /// <summary>서버 물리와 동기화 비용을 줄이도록 상한을 넘으면 가장 오래 움직인 사물을 멈춘다.</summary>
        public void Activate(PropBody body, Vector3 point, Vector3 impulse, float now, List<PropBody> settled)
        {
            Active entry = Find(body);
            if (entry == null)
            {
                if (active.Count >= GameTuning.PropPhysicsMaxActive)
                {
                    Active oldestEntry = active[0];
                    active.RemoveAt(0);
                    oldestEntry.body.SetDynamic(false);
                    settled.Add(oldestEntry.body);
                }

                body.SetDynamic(true);
                entry = new Active { body = body, activatedTime = now };
                active.Add(entry);
            }

            entry.slowTime = 0f;
            body.ApplyImpulse(point, impulse);
        }

        /// <summary>
        /// <summary>동기화하는 쪽이 보낼 것을 고르도록 움직이는 사물과 이번에 멈춘 사물을 나눠 담는다.</summary>
        /// 맵 밖으로 떨어진 사물은 판에서 사라지지 않게 시작 자리로 되돌린다.
        /// </summary>
        public void Step(float now, float deltaTime, List<PropBody> moving, List<PropBody> settled)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Active entry = active[i];
                PropBody body = entry.body;
                if (body == null)
                {
                    // 맵 씬이 내려가 사물이 없어졌다.
                    active.RemoveAt(i);
                    continue;
                }

                if (HasFallenOut(body))
                {
                    body.SetDynamic(false);
                    body.SetPose(body.StartPose.position, body.StartPose.rotation);
                    active.RemoveAt(i);
                    settled.Add(body);
                    continue;
                }

                entry.slowTime = PropPhysicsRules.IsSlow(body.Body) ? entry.slowTime + deltaTime : 0f;
                if (entry.slowTime >= GameTuning.PropSettleTime || now - entry.activatedTime >= MaxActiveSeconds)
                {
                    body.SetDynamic(false);
                    active.RemoveAt(i);
                    settled.Add(body);
                    continue;
                }

                moving.Add(body);
            }
        }

        /// <summary>
        /// 판이 끝나거나 서버가 멈춘 뒤에도 사물이 계속 움직이지 않도록 모두 그 자리에서 멈춘다.
        /// </summary>
        public void Clear()
        {
            foreach (Active entry in active)
            {
                if (entry.body != null)
                    entry.body.SetDynamic(false);
            }
            active.Clear();
        }

        /// <summary>
        /// 맵마다 바닥 높이가 달라서 절대 높이 대신 시작 자리 기준으로 <see cref="GameTuning.PropKillDepth"/> 넘게 떨어졌는지 본다.
        /// </summary>
        private static bool HasFallenOut(PropBody body)
        {
            return body.transform.position.y < body.StartPose.position.y - GameTuning.PropKillDepth;
        }

        /// <summary>이미 움직이는 사물을 다시 깨우지 않으려고 목록에서 찾는다.</summary>
        private Active Find(PropBody body)
        {
            foreach (Active entry in active)
            {
                if (entry.body == body)
                    return entry;
            }
            return null;
        }
    }
}
