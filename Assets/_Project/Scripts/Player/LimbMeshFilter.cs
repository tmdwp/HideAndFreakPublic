using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>1인칭에서 팔다리만 보이도록 스킨드 메시에서 팔다리 뼈 삼각형만 남긴 복제 메시를 만든다.</summary>
    public static class LimbMeshFilter
    {
        // 아래 뼈까지 모두 팔다리로 보는 팔다리 시작 뼈.
        private static readonly HumanBodyBones[] LimbRoots =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg
        };

        // 어깨·골반 경계 꼭짓점이 몸통 쪽으로 더 끌려 있으면 몸통으로 보도록 절반을 기준으로 둔다.
        private const float LimbWeightThreshold = 0.5f;

        /// <summary>리그마다 손가락 뼈 이름이 달라 시작 뼈만 찾고 그 아래는 모두 팔다리로 묶는다.</summary>
        public static HashSet<Transform> CollectLimbBones(Animator animator)
        {
            var limbBones = new HashSet<Transform>();
            if (animator == null)
                return limbBones;

            foreach (HumanBodyBones rootId in LimbRoots)
            {
                Transform rootBone = SkinBones.Find(animator, rootId);
                if (rootBone == null)
                    continue;
                foreach (Transform bone in rootBone.GetComponentsInChildren<Transform>(true))
                    limbBones.Add(bone);
            }
            return limbBones;
        }

        /// <summary>원본 메시를 고치면 다른 아바타에도 반영되므로 복제본에서 삼각형만 걸러 낸다.</summary>
        public static Mesh CreateLimbOnlyMesh(SkinnedMeshRenderer source, HashSet<Transform> limbBones)
        {
            Mesh sourceMesh = source.sharedMesh;
            if (sourceMesh == null || !sourceMesh.isReadable || limbBones.Count == 0)
                return null;

            bool[] isLimbVertex = ClassifyVertices(sourceMesh, source, limbBones);
            Mesh limbMesh = Object.Instantiate(sourceMesh);
            limbMesh.name = sourceMesh.name + " (1P Limbs)";

            int keptTriangleCount = 0;
            var keptIndices = new List<int>();
            for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
            {
                keptIndices.Clear();
                int[] triangles = sourceMesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int vertexA = triangles[i], vertexB = triangles[i + 1], vertexC = triangles[i + 2];
                    // 한 꼭짓점이라도 몸통이면 그 삼각형이 몸통 쪽으로 늘어져 화면을 가리므로 뺀다.
                    if (!isLimbVertex[vertexA] || !isLimbVertex[vertexB] || !isLimbVertex[vertexC])
                        continue;
                    keptIndices.Add(vertexA);
                    keptIndices.Add(vertexB);
                    keptIndices.Add(vertexC);
                }
                limbMesh.SetTriangles(keptIndices, subMesh);
                keptTriangleCount += keptIndices.Count / 3;
            }

            if (keptTriangleCount > 0)
                return limbMesh;

            Object.Destroy(limbMesh);
            return null;
        }

        /// <summary>삼각형을 거르려면 꼭짓점마다 팔다리인지 먼저 알아야 한다.</summary>
        private static bool[] ClassifyVertices(Mesh mesh, SkinnedMeshRenderer source, HashSet<Transform> limbBones)
        {
            var result = new bool[mesh.vertexCount];
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = source.bones;

            if (weights.Length != result.Length || bones.Length == 0)
            {
                bool isRootLimb = source.rootBone != null && limbBones.Contains(source.rootBone);
                for (int i = 0; i < result.Length; i++)
                    result[i] = isRootLimb;
                return result;
            }

            for (int i = 0; i < result.Length; i++)
            {
                BoneWeight weight = weights[i];
                float limbWeight = LimbWeight(bones, limbBones, weight.boneIndex0, weight.weight0)
                                   + LimbWeight(bones, limbBones, weight.boneIndex1, weight.weight1)
                                   + LimbWeight(bones, limbBones, weight.boneIndex2, weight.weight2)
                                   + LimbWeight(bones, limbBones, weight.boneIndex3, weight.weight3);
                result[i] = limbWeight >= LimbWeightThreshold;
            }
            return result;
        }

        /// <summary>
        /// 쓰이지 않는 슬롯은 인덱스가 범위를 벗어날 수 있어서, 그런 경우도 0으로 안전하게 처리한다.
        /// </summary>
        private static float LimbWeight(Transform[] bones, HashSet<Transform> limbBones, int boneIndex, float weight)
        {
            if (weight <= 0f || boneIndex < 0 || boneIndex >= bones.Length)
                return 0f;
            return limbBones.Contains(bones[boneIndex]) ? weight : 0f;
        }
    }
}
