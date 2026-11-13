using System.Collections.Generic;
using GuardVsMannequin.Props;
using UnityEngine;

namespace GuardVsMannequin.Player.Mannequin
{
    /// <summary>변신한 사물의 보이는 모양대로 막히고 맞도록, 외형 조각마다 상자 충돌체를 만든다.</summary>
    public sealed class DisguiseShape
    {
        // 조각이 많으면 이동 보정 검사가 무거워지므로 넘으면 전체 경계 상자 하나로 대신한다.
        private const int MaxPartBoxes = 8;

        // 종이처럼 두께가 0인 조각도 막히고 맞도록 두는 최소 두께(m).
        private const float MinThickness = 0.02f;

        private readonly List<BoxCollider> boxes = new();
        private readonly List<BoxCollider> activeBoxes = new();
        private readonly Transform root;

        /// <summary>외형 조각과 함께 움직이고 넘어지도록 외형 루트 아래에 만든다.</summary>
        public DisguiseShape(Transform disguiseRoot)
        {
            root = disguiseRoot;
        }

        /// <summary>지금 쓰는 상자만 담기며 변신하지 않았으면 비어 있다.</summary>
        public IReadOnlyList<BoxCollider> ActiveBoxes => activeBoxes;

        /// <summary>
        /// 변신할 때마다 오브젝트를 만들고 지우지 않도록 상자를 다시 쓰고, 모자라면 만들고 남으면 끈다.
        /// </summary>
        public void Build(IReadOnlyList<PropAppearance.Part> parts, Bounds wholeBounds)
        {
            activeBoxes.Clear();
            if (parts.Count == 0 || parts.Count > MaxPartBoxes)
            {
                ApplyBox(GetBox(0), Vector3.zero, Quaternion.identity, wholeBounds.center, wholeBounds.size);
            }
            else
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    PropAppearance.Part part = parts[i];
                    Bounds meshBounds = part.Mesh != null ? part.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                    Vector3 scale = Abs(part.Scale);
                    ApplyBox(GetBox(i), part.Position, part.Rotation, Vector3.Scale(meshBounds.center, part.Scale), Vector3.Scale(meshBounds.size, scale));
                }
            }

            for (int i = activeBoxes.Count; i < boxes.Count; i++)
                boxes[i].gameObject.SetActive(false);
        }

        /// <summary>변신을 풀면 사람 모습이 보이지 않는 사물 모양에 막히거나 맞지 않도록 전부 끈다.</summary>
        public void Clear()
        {
            activeBoxes.Clear();
            foreach (BoxCollider box in boxes)
                box.gameObject.SetActive(false);
        }

        /// <summary>
        /// 조각에 배율이 있어도 상자 크기가 월드 크기와 같도록, 배율 없는 자식 오브젝트에 크기를 직접 넣는다.
        /// </summary>
        private void ApplyBox(BoxCollider box, Vector3 position, Quaternion rotation, Vector3 center, Vector3 size)
        {
            box.transform.SetLocalPositionAndRotation(position, rotation);
            box.center = center;
            box.size = Vector3.Max(size, Vector3.one * MinThickness);
            box.gameObject.SetActive(true);
            activeBoxes.Add(box);
        }

        /// <summary>
        /// 다른 플레이어를 막도록 아바타와 같은 레이어로 만들고, 처음 필요할 때만 만든다.
        /// </summary>
        private BoxCollider GetBox(int index)
        {
            while (boxes.Count <= index)
            {
                var boxObject = new GameObject($"Shape_{boxes.Count}") { layer = root.gameObject.layer };
                boxObject.transform.SetParent(root, false);
                boxes.Add(boxObject.AddComponent<BoxCollider>());
            }
            return boxes[index];
        }

        /// <summary>뒤집힌 조각의 음수 배율이 상자 크기를 음수로 만들지 않도록 한다.</summary>
        private static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
