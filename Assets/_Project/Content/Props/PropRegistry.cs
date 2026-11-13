using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Props
{
    /// <summary>변신 상태를 번호 하나로 동기화하도록 지금 맵의 변신 사물을 번호로 찾는다.</summary>
    public static class PropRegistry
    {
        private static readonly Dictionary<int, TransformableProp> props = new();

        /// <summary>도메인 재로드를 꺼도 이전 씬 항목이 남지 않도록 플레이 모드 시작 때 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            props.Clear();
        }

        /// <summary>
        /// 번호가 없거나 겹치면 변신이 엉뚱한 사물로 동기화되므로, 원인(씬을 저장하지 않음)을 알 수 있게 경고한다.
        /// </summary>
        public static void Register(TransformableProp prop)
        {
            if (prop.Index == TransformableProp.None)
            {
                Debug.LogWarning($"[PropRegistry] '{prop.name}'에 사물 번호가 없어 변신할 수 없습니다. 맵 씬을 저장하면 번호가 매겨집니다.", prop);
                return;
            }
            if (props.TryGetValue(prop.Index, out TransformableProp existing) && existing != null && existing != prop)
                Debug.LogWarning($"[PropRegistry] 사물 번호 {prop.Index}가 '{existing.name}'과 '{prop.name}'에 중복됩니다. 맵 씬을 다시 저장하세요.", prop);

            props[prop.Index] = prop;
        }

        /// <summary>
        /// 씬 전환 중 새 사물이 먼저 등록될 수 있어, 그 번호가 아직 이 사물을 가리킬 때만 지운다.
        /// </summary>
        public static void Unregister(TransformableProp prop)
        {
            if (props.TryGetValue(prop.Index, out TransformableProp existing) && existing == prop)
                props.Remove(prop.Index);
        }

        /// <summary>
        /// 네트워크로 받은 번호는 이미 파괴된 사물을 가리킬 수 있어, 파괴된 객체도 없는 것으로 본다.
        /// </summary>
        public static bool TryGet(int index, out TransformableProp prop)
        {
            return props.TryGetValue(index, out prop) && prop != null;
        }
    }
}
