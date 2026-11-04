using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 스킨드 메시에서 팔다리 뼈에 붙은 삼각형만 남긴 복제 메시를 만든다. (1인칭에서 몸통·머리를 빼고 보여주기 위함)
    /// 꼭짓점의 뼈 가중치 중 팔다리 뼈 몫이 절반 이상이면 팔다리 꼭짓점으로 보고, 세 꼭짓점이 모두 팔다리인 삼각형만 남긴다.
    /// 원본 메시는 바꾸지 않는다. 메시를 실행 중에 읽으려면 모델 가져오기 설정의 Read/Write가 켜져 있어야 한다.
    /// </summary>
    public static class LimbMeshFilter
    {
        // 팔다리의 시작 뼈. 그 아래 뼈(손가락, 발가락, 비틀림 뼈 등)도 모두 팔다리로 본다.
        private static readonly HumanBodyBones[] LimbRoots =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg
        };

        private const float LimbWeightThreshold = 0.5f;

        /// <summary>
        /// 애니메이터의 팔다리 뼈(시작 뼈와 그 아래 모든 뼈)를 모은다.
        /// 뼈는 <see cref="SkinBones"/>로 찾는다(Generic 리그는 이름으로). 못 찾으면 빈 집합.
        /// </summary>
        public static HashSet<Transform> CollectLimbBones(Animator animator)
        {
            var limbBones = new HashSet<Transform>();
            if (animator == null)
                return limbBones;

            foreach (HumanBodyBones rootId in LimbRoots)
            {
                Transform root = SkinBones.Find(animator, rootId);
                if (root == null)
                    continue;
                foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
                    limbBones.Add(bone);
            }
            return limbBones;
        }

        /// <summary>
        /// 팔다리 삼각형만 남긴 복제 메시를 만든다. 메시를 읽을 수 없거나 남는 삼각형이 없으면 null.
        /// </summary>
        public static Mesh CreateLimbOnlyMesh(SkinnedMeshRenderer source, HashSet<Transform> limbBones)
        {
            Mesh mesh = source.sharedMesh;
            if (mesh == null || !mesh.isReadable || limbBones.Count == 0)
                return null;

            bool[] isLimbVertex = ClassifyVertices(mesh, source, limbBones);
            Mesh limbMesh = Object.Instantiate(mesh);
            limbMesh.name = mesh.name + " (1P Limbs)";

            int keptTriangles = 0;
            var kept = new List<int>();
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                kept.Clear();
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    if (!isLimbVertex[a] || !isLimbVertex[b] || !isLimbVertex[c])
                        continue;
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }
                limbMesh.SetTriangles(kept, subMesh);
                keptTriangles += kept.Count / 3;
            }

            if (keptTriangles > 0)
                return limbMesh;

            Object.Destroy(limbMesh);
            return null;
        }

        /// <summary>
        /// 꼭짓점마다 팔다리 꼭짓점인지 정한다. 가중치가 없는 메시는 뿌리 뼈가 팔다리인지로 전체를 정한다.
        /// </summary>
        private static bool[] ClassifyVertices(Mesh mesh, SkinnedMeshRenderer source, HashSet<Transform> limbBones)
        {
            var result = new bool[mesh.vertexCount];
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = source.bones;

            if (weights.Length != result.Length || bones.Length == 0)
            {
                bool rootIsLimb = source.rootBone != null && limbBones.Contains(source.rootBone);
                for (int i = 0; i < result.Length; i++)
                    result[i] = rootIsLimb;
                return result;
            }

            for (int i = 0; i < result.Length; i++)
            {
                BoneWeight w = weights[i];
                float limbWeight = LimbWeight(bones, limbBones, w.boneIndex0, w.weight0)
                                   + LimbWeight(bones, limbBones, w.boneIndex1, w.weight1)
                                   + LimbWeight(bones, limbBones, w.boneIndex2, w.weight2)
                                   + LimbWeight(bones, limbBones, w.boneIndex3, w.weight3);
                result[i] = limbWeight >= LimbWeightThreshold;
            }
            return result;
        }

        /// <summary>
        /// 가중치 하나가 팔다리 뼈 몫이면 그 값을, 아니면 0을 돌려준다.
        /// </summary>
        private static float LimbWeight(Transform[] bones, HashSet<Transform> limbBones, int boneIndex, float weight)
        {
            if (weight <= 0f || boneIndex < 0 || boneIndex >= bones.Length)
                return 0f;
            return limbBones.Contains(bones[boneIndex]) ? weight : 0f;
        }
    }
}
