using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>같은 캔버스의 UI 코드를 찾고, 못 찾으면 원인을 알 수 있게 오류를 남긴다.</summary>
    public static class UiLookup
    {
        /// <summary>
        /// 프리팹 배치가 잘못되면 나중에 null 참조로만 드러나므로, 찾지 못한 즉시 어떤 컴포넌트가 빠졌는지 로그로 남긴다.
        /// </summary>
        public static T FindInParent<T>(Component owner) where T : class
        {
            T found = owner.GetComponentInParent<T>(true);
            if (found == null)
                Debug.LogError($"[{owner.GetType().Name}] 같은 캔버스(부모 쪽)에 {typeof(T).Name}가 없습니다. 프리팹의 컴포넌트 배치를 확인하세요.", owner);
            return found;
        }
    }
}
