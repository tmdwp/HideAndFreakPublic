using System.Collections.Generic;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>
    /// 코드 안에서는 아무도 호출하지 않는데 엔진이 직접 부르는 함수는 "사용처 없음"이 정상이라, 죽은 코드로 오해하지 않게 구분한다.
    /// </summary>
    internal static class EngineCallbackNames
    {
        private static readonly HashSet<string> names = new HashSet<string>
        {
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset",
            "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected",
            "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay",
            "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
            "OnControllerColliderHit", "OnAnimatorIK", "OnAnimatorMove",
            "OnApplicationQuit", "OnApplicationFocus", "OnApplicationPause",
            "OnBecameVisible", "OnBecameInvisible", "OnRenderObject", "OnWillRenderObject",
            "OnMouseDown", "OnMouseUp", "OnMouseEnter", "OnMouseExit",
            "OnTransformParentChanged", "OnRectTransformDimensionsChange",
            "OnBeforeSerialize", "OnAfterDeserialize",
            // FishNet NetworkBehaviour 콜백
            "OnStartServer", "OnStopServer", "OnStartClient", "OnStopClient",
            "OnStartNetwork", "OnStopNetwork", "OnOwnershipServer", "OnOwnershipClient",
            "OnSpawnServer", "OnDespawnServer",
        };

        public static bool Contains(string methodName) => names.Contains(methodName);
    }
}
