using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>실행 중 생성과 에디터 미리 굽기가 같은 모양을 만들도록 뼈 판정 캡슐 배치를 한곳에 둔다.</summary>
    public static class SkinHitboxFactory
    {
        /// <summary>Project Settings > Tags and Layers에 같은 이름의 레이어가 있어야 한다.</summary>
        public const string LayerName = "Hitbox";

        // 외형 비율로 캡슐 굵기를 맞추는 기준 골반~머리 거리.
        private const float HumanHipsToHead = 0.63f;

        // 사람 기준 반지름(m)
        private const float TorsoRadius = 0.16f;
        private const float HeadRadius = 0.12f;
        private const float HeadLength = 0.2f;
        private const float UpperArmRadius = 0.075f;
        private const float LowerArmRadius = 0.065f;
        private const float HandRadius = 0.06f;
        private const float HandLength = 0.2f;
        private const float UpperLegRadius = 0.09f;
        private const float LowerLegRadius = 0.075f;

        // 캡슐이 정확히 따라가도록 잇는 부모·자식 뼈와 반지름.
        private static readonly (HumanBodyBones from, HumanBodyBones to, float radius)[] Segments =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine, TorsoRadius),
            (HumanBodyBones.Spine, HumanBodyBones.Chest, TorsoRadius),
            (HumanBodyBones.Chest, HumanBodyBones.UpperChest, TorsoRadius),
            (HumanBodyBones.UpperChest, HumanBodyBones.Neck, TorsoRadius),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, UpperArmRadius),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, LowerArmRadius),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, UpperArmRadius),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, LowerArmRadius),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, UpperLegRadius),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, LowerLegRadius),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, UpperLegRadius),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, LowerLegRadius),
        };

        // 자식 뼈가 없는 끝 부위의 뼈·방향·길이·반지름.
        private static readonly (HumanBodyBones bone, HumanBodyBones parent, float length, float radius)[] Tips =
        {
            (HumanBodyBones.Head, HumanBodyBones.Neck, HeadLength, HeadRadius),
            (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, HandLength, HandRadius),
            (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HandLength, HandRadius),
        };

        // -2: 아직 찾지 않음, -1: 레이어 없음
        private static int hitboxLayer = -2;

        /// <summary>판정 캡슐이 이동·물리에 영향을 주지 않도록 처음 쓸 때 모든 레이어와의 충돌을 끈다.</summary>
        public static int HitboxLayer
        {
            get
            {
                if (hitboxLayer != -2)
                    return hitboxLayer;
                hitboxLayer = LayerMask.NameToLayer(LayerName);
                if (hitboxLayer < 0)
                {
                    Debug.LogWarning($"[SkinHitboxFactory] '{LayerName}' 레이어가 없어 판정 캡슐을 쓰지 않습니다. (Project Settings > Tags and Layers)");
                    return hitboxLayer;
                }
                for (int layer = 0; layer < 32; layer++)
                    Physics.IgnoreLayerCollision(hitboxLayer, layer, true);
                return hitboxLayer;
            }
        }

        /// <summary>
        /// 외형마다 키가 달라서 굵기를 사람 기준 골반~머리 거리와의 비로 맞춘다.
        /// 실행 중에는 애니메이터로, 에디터에서는 아바타 정보로 뼈를 찾으므로 뼈 찾는 방법을 받는다.
        /// </summary>
        public static List<CapsuleCollider> Build(Func<HumanBodyBones, Transform> findBone, int layer)
        {
            var hitboxes = new List<CapsuleCollider>();
            Transform hips = findBone(HumanBodyBones.Hips);
            Transform head = findBone(HumanBodyBones.Head);
            float sizeScale = hips != null && head != null ? Vector3.Distance(hips.position, head.position) / HumanHipsToHead : 1f;

            foreach ((HumanBodyBones from, HumanBodyBones to, float radius) in Segments)
            {
                Transform startBone = findBone(from);
                Transform endBone = findBone(to);
                if (startBone != null && endBone != null)
                    AddCapsule(hitboxes, startBone, startBone.position, endBone.position, radius * sizeScale, layer);
            }
            foreach ((HumanBodyBones bone, HumanBodyBones parent, float length, float radius) in Tips)
            {
                Transform tipBone = findBone(bone);
                Transform parentBone = findBone(parent);
                if (tipBone == null || parentBone == null)
                    continue;
                Vector3 direction = (tipBone.position - parentBone.position).normalized;
                AddCapsule(hitboxes, tipBone, tipBone.position, tipBone.position + direction * length * sizeScale, radius * sizeScale, layer);
            }
            return hitboxes;
        }

        /// <summary>
        /// 뼈를 따라 움직이도록 뼈의 자식으로 만들고, 뼈의 크기 배율이 캡슐 크기를 바꾸지 않도록 나눠서 월드 크기를 맞춘다.
        /// </summary>
        private static void AddCapsule(List<CapsuleCollider> hitboxes, Transform bone, Vector3 from, Vector3 to, float radius, int layer)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.01f)
                return;

            var hitboxObject = new GameObject("Hitbox_" + bone.name) { layer = layer };
            Transform hitboxTransform = hitboxObject.transform;
            hitboxTransform.SetParent(bone, false);
            hitboxTransform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, axis / length));

            float boneScale = Mathf.Max(1e-4f, hitboxTransform.lossyScale.x);
            CapsuleCollider capsule = hitboxObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.direction = 1;
            capsule.radius = radius / boneScale;
            capsule.height = (length + radius * 2f) / boneScale;
            hitboxes.Add(capsule);
        }
    }
}
