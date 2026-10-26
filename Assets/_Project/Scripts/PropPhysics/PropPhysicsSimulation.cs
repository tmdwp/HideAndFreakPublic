using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.PropPhysics
{
    /// <summary>
    /// 서버의 사물 물리 진행: 깨우기, 동시에 움직이는 수 상한, 멈춤 판정, 맵 밖 추락 복귀. (네트워크와 무관한 순수 진행 규칙)
    /// 멈춘 사물은 다시 키네마틱이 되어 새 자리에 남는다.
    /// </summary>
    public sealed class PropPhysicsSimulation
    {
        // 이보다 오래 움직이면 멈춤 판정과 관계없이 멈춘다(초). (계속 굴러다니는 사물 방지)
        private const float MaxActiveSeconds = 10f;

        /// <summary>
        /// 움직이는 사물 하나와 진행 상태.
        /// </summary>
        private sealed class Active
        {
            public PropBody body;
            public float activatedTime;
            public float slowTime;
        }

        private readonly List<Active> active = new();

        /// <summary>지금 움직이는 사물 수.</summary>
        public int ActiveCount => active.Count;

        /// <summary>
        /// 사물을 깨워 충격을 준다. 이미 움직이면 충격만 더한다.
        /// 상한을 넘으면 가장 오래 움직인 사물을 그 자리에서 멈추고 <paramref name="settled"/>에 넣는다.
        /// </summary>
        public void Activate(PropBody body, Vector3 point, Vector3 impulse, float now, List<PropBody> settled)
        {
            Active entry = Find(body);
            if (entry == null)
            {
                if (active.Count >= GameTuning.PropPhysicsMaxActive)
                {
                    Active oldest = active[0];
                    active.RemoveAt(0);
                    oldest.body.SetDynamic(false);
                    settled.Add(oldest.body);
                }

                body.SetDynamic(true);
                entry = new Active { body = body, activatedTime = now };
                active.Add(entry);
            }

            entry.slowTime = 0f;
            body.ApplyImpulse(point, impulse);
        }

        /// <summary>
        /// 물리 한 단계 뒤의 상태를 정리한다. 계속 움직이는 사물은 <paramref name="moving"/>, 이번에 멈춘 사물은 <paramref name="settled"/>에 넣는다.
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
        /// 모든 사물을 그 자리에서 멈춘다. (판 종료, 서버 종료)
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
        /// 시작 자리보다 <see cref="GameTuning.PropKillDepth"/> 넘게 아래로 떨어졌는지. (맵 밖 추락)
        /// </summary>
        private static bool HasFallenOut(PropBody body)
        {
            return body.transform.position.y < body.StartPose.position.y - GameTuning.PropKillDepth;
        }

        /// <summary>
        /// 움직이는 목록에서 사물을 찾는다. 없으면 null.
        /// </summary>
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
