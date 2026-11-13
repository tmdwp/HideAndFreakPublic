using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>쏠 때마다 만들고 지우는 비용을 없애도록 사격·폭발 이펙트를 다시 쓰는 풀.</summary>
    public static class EffectPool
    {
        // 메모리를 계속 붙잡지 않도록 프리팹마다 쉬게 둘 최대 개수.
        private const int MaxIdlePerPrefab = 16;

        private static readonly Dictionary<GameObject, Stack<PooledEffect>> idle = new();
        private static Transform storage;

        /// <summary>
        /// 도메인 재로드를 끈 경우 정적 필드가 남아 이전 플레이의 지워진 항목을 꺼내므로, 플레이 모드에 들어갈 때 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            idle.Clear();
            storage = null;
        }

        /// <summary>
        /// 쉬는 이펙트가 있으면 다시 쓰고, 없을 때만 새로 만들어 생성 비용을 줄인다.
        /// </summary>
        /// <param name="parent">따라 움직여야 하면 붙일 부모.</param>
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

        /// <summary>부모와 함께 지워지지 않도록 보관함으로 옮기고, 넘치면 메모리를 아끼려고 지운다.</summary>
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

        /// <summary>조정은 곱해지는 방식이라 다른 조정값으로 만든 것은 다시 쓸 수 없어 버린다.</summary>
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
        /// 씬이 바뀌어도 쉬는 이펙트가 지워지지 않도록 DontDestroyOnLoad 보관함을 처음 필요할 때 만든다.
        /// </summary>
        private static Transform GetStorage()
        {
            if (storage == null)
            {
                var storageRoot = new GameObject("EffectPool");
                Object.DontDestroyOnLoad(storageRoot);
                storage = storageRoot.transform;
            }
            return storage;
        }
    }
}
