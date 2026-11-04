using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 사격·폭발 이펙트를 다시 쓰는 풀. 쏠 때마다 만들고 지우던 비용(생성·파티클 초기화·가비지)을 없앤다.
    /// 원본 프리팹별로 쉬는 이펙트를 쌓아 두고, 씬이 바뀌어도 남도록 DontDestroyOnLoad 보관함에 둔다.
    /// 총구 화염처럼 다른 오브젝트에 붙인 이펙트는 그 오브젝트와 함께 지워질 수 있으므로, 꺼낼 때 지워진 것은 건너뛴다.
    /// </summary>
    public static class EffectPool
    {
        // 프리팹마다 쉬게 둘 최대 개수. 넘치면 지운다.
        private const int MaxIdlePerPrefab = 16;

        private static readonly Dictionary<GameObject, Stack<PooledEffect>> idle = new();
        private static Transform storage;

        /// <summary>
        /// 플레이 모드에 들어갈 때 목록을 비운다. (도메인 재로드를 끈 경우 이전 플레이의 항목이 남지 않도록)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            idle.Clear();
            storage = null;
        }

        /// <summary>
        /// 이펙트를 꺼내(없으면 만들어) 위치에 놓고 처음부터 재생한다.
        /// </summary>
        /// <param name="prefab">이펙트 원본 프리팹.</param>
        /// <param name="position">월드 위치.</param>
        /// <param name="rotation">월드 회전.</param>
        /// <param name="parent">붙일 부모. 없으면 null.</param>
        /// <param name="brightness">밝기 배율 (<see cref="EffectTuning"/>).</param>
        /// <param name="scale">크기 배율 (<see cref="EffectTuning"/>).</param>
        /// <param name="lifetime">이 시간(초)이 지나면 풀로 돌아간다.</param>
        public static PooledEffect Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
            float brightness, float scale, float lifetime)
        {
            PooledEffect effect = TakeIdle(prefab, brightness, scale);
            if (effect == null)
            {
                GameObject instance = Object.Instantiate(prefab, position, rotation, parent);
                EffectTuning.Apply(instance, brightness, scale);
                effect = instance.AddComponent<PooledEffect>();
                effect.Initialize(prefab, brightness, scale);
            }
            else
            {
                effect.transform.SetParent(parent, false);
                effect.transform.SetPositionAndRotation(position, rotation);
                effect.gameObject.SetActive(true);
            }

            effect.Restart(lifetime);
            return effect;
        }

        /// <summary>
        /// 이펙트를 끄고 풀로 돌려놓는다. 이미 충분히 쌓여 있으면 지운다.
        /// </summary>
        public static void Release(PooledEffect effect)
        {
            if (effect == null)
                return;

            if (!idle.TryGetValue(effect.SourcePrefab, out Stack<PooledEffect> stack))
                idle[effect.SourcePrefab] = stack = new Stack<PooledEffect>();
            if (stack.Count >= MaxIdlePerPrefab)
            {
                Object.Destroy(effect.gameObject);
                return;
            }

            effect.gameObject.SetActive(false);
            effect.transform.SetParent(GetStorage(), false);
            stack.Push(effect);
        }

        /// <summary>
        /// 쉬고 있는 이펙트 중 같은 조정값으로 만든 것을 꺼낸다. 지워졌거나 조정값이 바뀐 것은 버린다.
        /// (Play 중 인스펙터에서 밝기·크기를 바꾸면 다음 사격부터 새 값으로 만들어진다)
        /// </summary>
        private static PooledEffect TakeIdle(GameObject prefab, float brightness, float scale)
        {
            if (!idle.TryGetValue(prefab, out Stack<PooledEffect> stack))
                return null;

            while (stack.Count > 0)
            {
                PooledEffect effect = stack.Pop();
                if (effect == null)
                    continue;
                if (Mathf.Approximately(effect.Brightness, brightness) && Mathf.Approximately(effect.Scale, scale))
                    return effect;
                Object.Destroy(effect.gameObject);
            }
            return null;
        }

        /// <summary>
        /// 쉬는 이펙트를 둘 보관함. 씬이 바뀌어도 남는다.
        /// </summary>
        private static Transform GetStorage()
        {
            if (storage == null)
            {
                var root = new GameObject("EffectPool");
                Object.DontDestroyOnLoad(root);
                storage = root.transform;
            }
            return storage;
        }
    }
}
