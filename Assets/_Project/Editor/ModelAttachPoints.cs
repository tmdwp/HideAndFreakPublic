using System.Collections.Generic;
using GuardVsMannequin.Player;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 외형 모델을 원본에서 다시 만들면 사람이 손으로 넣은 부착 지점(손전등 위치 등)이 사라지므로,
    /// 덮어쓰기 전에 기억했다가 새 모델의 같은 뼈 아래에 같은 위치로 되살린다.
    /// </summary>
    public static class ModelAttachPoints
    {
        // 게임이 이름으로 찾는 부착 지점. 지점이 늘면 여기에 더한다.
        private static readonly string[] PointNames = { FlashlightLook.MountPointName };

        /// <summary>지점 하나의 부모 뼈 이름과 그 뼈 기준 자세.</summary>
        public readonly struct SavedPoint
        {
            public readonly string name;

            /// <summary>모델 루트 바로 아래였으면 null.</summary>
            public readonly string parentName;

            public readonly Vector3 localPosition;
            public readonly Quaternion localRotation;
            public readonly Vector3 localScale;

            /// <summary>
            /// 한 줄로 기록하도록 값을 한 번에 받는다.
            /// </summary>
            public SavedPoint(string name, string parentName, Transform point)
            {
                this.name = name;
                this.parentName = parentName;
                localPosition = point.localPosition;
                localRotation = point.localRotation;
                localScale = point.localScale;
            }
        }

        /// <summary>덮어쓰기 전 기존 모델 프리팹에 있던 부착 지점을 읽는다. 프리팹이 없으면 빈 목록.</summary>
        public static List<SavedPoint> Capture(string prefabPath)
        {
            var saved = new List<SavedPoint>();
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null)
                return saved;

            foreach (Transform child in existing.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(PointNames, child.name) < 0)
                    continue;
                string parentName = child.parent == existing.transform ? null : child.parent.name;
                saved.Add(new SavedPoint(child.name, parentName, child));
            }
            return saved;
        }

        /// <summary>
        /// 새 모델에서 같은 이름의 뼈를 찾아 지점을 다시 만든다. 이미 있으면(원본에 들어 있던 경우) 원본을 존중해 건너뛴다.
        /// </summary>
        /// <returns>되살린 지점 수.</returns>
        public static int Restore(GameObject root, List<SavedPoint> saved)
        {
            int restored = 0;
            foreach (SavedPoint point in saved)
            {
                if (FindDescendant(root.transform, point.name) != null)
                    continue;

                Transform parent = point.parentName == null ? root.transform : FindDescendant(root.transform, point.parentName);
                if (parent == null)
                {
                    Debug.LogWarning($"[ModelAttachPoints] 새 모델에 '{point.parentName}' 뼈가 없어 '{point.name}' 지점을 되살리지 못했습니다. 모델을 열어 다시 넣으세요.", root);
                    continue;
                }

                var pointObject = new GameObject(point.name);
                pointObject.transform.SetParent(parent, false);
                pointObject.transform.localPosition = point.localPosition;
                pointObject.transform.localRotation = point.localRotation;
                pointObject.transform.localScale = point.localScale;
                restored++;
            }
            return restored;
        }

        /// <summary>
        /// 지점·뼈는 계층 깊이 있을 수 있어 모든 자식을 차례로 찾는다.
        /// </summary>
        private static Transform FindDescendant(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child != parent && child.name == childName)
                    return child;
            }
            return null;
        }
    }
}
