using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuardVsMannequin.Player;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>스폰할 때마다 메시를 거르고 캡슐을 만들지 않도록 외형 모델 프리팹에 1인칭 팔다리와 판정 캡슐을 미리 만들어 둔다.</summary>
    public static class SkinPartBaker
    {
        private const string MeshFolder = "Assets/_Project/Art/Skins/Baked";

        /// <summary>외형 프리팹을 다시 만들면 손으로 고친 값이 사라지므로, 있는 프리팹을 그 자리에서 열어 굽는다.</summary>
        public static void BakeAllSkins(List<string> report)
        {
            List<string> prefabPaths = AssetDatabase.FindAssets("t:" + nameof(CharacterSkin), new[] { "Assets/_Project" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CharacterSkin>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(skin => skin != null && skin.modelPrefab != null)
                .Select(skin => AssetDatabase.GetAssetPath(skin.modelPrefab))
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();

            foreach (string prefabPath in prefabPaths)
            {
                EnsureMeshesReadable(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), report);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    report.Add(Bake(root));
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        /// <summary>
        /// 외형 생성 도구가 저장 직전에 부를 수 있도록 열린 모델 루트에 굽는다. 다시 구워도 겹치지 않게 이전 결과를 먼저 지운다.
        /// </summary>
        public static string Bake(GameObject modelRoot)
        {
            EnsureMeshesReadable(modelRoot, null);
            if (!modelRoot.TryGetComponent(out BakedSkinParts parts))
                parts = modelRoot.AddComponent<BakedSkinParts>();
            RemovePrevious(parts);

            Animator animator = modelRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                parts.SetBakedParts(new List<SkinnedMeshRenderer>(), new List<SkinnedMeshRenderer>(), new List<CapsuleCollider>());
                return $"{modelRoot.name}: 애니메이터가 없어 건너뜀";
            }

            Func<HumanBodyBones, Transform> findBone = CreateBoneFinder(animator);
            var sources = new List<SkinnedMeshRenderer>();
            var limbs = new List<SkinnedMeshRenderer>();
            HashSet<Transform> limbBones = LimbMeshFilter.CollectLimbBones(findBone);
            if (limbBones.Count > 0)
            {
                foreach (SkinnedMeshRenderer source in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh limbMesh = LimbMeshFilter.CreateLimbOnlyMesh(source, limbBones);
                    if (limbMesh == null)
                        continue;
                    SkinnedMeshRenderer limb = LimbRendererFactory.Create(source, SaveMesh(limbMesh, $"{modelRoot.name}_{source.name}_Limbs"));
                    // 다른 사람 화면에는 온몸이 보여야 하므로 꺼 두고, 로컬 1인칭일 때만 켠다.
                    limb.gameObject.SetActive(false);
                    sources.Add(source);
                    limbs.Add(limb);
                }
            }

            // 실행 중 전용 HitboxLayer는 충돌 설정을 바꾸므로, 에디터에서는 레이어 번호만 읽는다.
            int hitboxLayer = LayerMask.NameToLayer(SkinHitboxFactory.LayerName);
            List<CapsuleCollider> hitboxes = hitboxLayer >= 0 ? SkinHitboxFactory.Build(findBone, hitboxLayer) : new List<CapsuleCollider>();
            // 가드처럼 판정 캡슐을 쓰지 않는 역할의 총에 걸리지 않도록 꺼 두고, SkinHitboxes가 있는 아바타에서만 켠다.
            foreach (CapsuleCollider hitbox in hitboxes)
                hitbox.enabled = false;
            parts.SetBakedParts(sources, limbs, hitboxes);
            EditorUtility.SetDirty(parts);

            string layerNote = hitboxLayer < 0 ? $" ('{SkinHitboxFactory.LayerName}' 레이어 없음)" : string.Empty;
            return $"{modelRoot.name}: 1인칭 팔다리 {limbs.Count}개, 판정 캡슐 {hitboxes.Count}개{layerNote}";
        }

        /// <summary>다시 구울 때 이전 팔다리·캡슐이 남아 두 번 그려지거나 두 번 맞지 않도록 지운다.</summary>
        private static void RemovePrevious(BakedSkinParts parts)
        {
            foreach (SkinnedMeshRenderer limb in parts.LimbRenderers.ToList())
            {
                if (limb != null)
                    UnityEngine.Object.DestroyImmediate(limb.gameObject);
            }
            foreach (CapsuleCollider hitbox in parts.Hitboxes.ToList())
            {
                if (hitbox != null)
                    UnityEngine.Object.DestroyImmediate(hitbox.gameObject);
            }
        }

        /// <summary>
        /// 실행 중이 아닌 애니메이터는 뼈 조회가 보장되지 않아, 아바타의 사람 뼈 이름표(휴머노이드) 또는 같은 이름의 뼈(Generic)로 찾는다.
        /// </summary>
        private static Func<HumanBodyBones, Transform> CreateBoneFinder(Animator animator)
        {
            Transform[] allBones = animator.GetComponentsInChildren<Transform>(true);
            Dictionary<string, string> humanToBone = animator.avatar != null && animator.avatar.isHuman
                ? animator.avatar.humanDescription.human.ToDictionary(human => human.humanName, human => human.boneName)
                : null;

            return bone =>
            {
                Transform mapped = animator.isHuman ? animator.GetBoneTransform(bone) : null;
                if (mapped != null)
                    return mapped;

                string boneName = bone.ToString();
                if (humanToBone != null)
                {
                    int index = (int)bone;
                    if (index < 0 || index >= HumanTrait.BoneName.Length || !humanToBone.TryGetValue(HumanTrait.BoneName[index], out boneName))
                        return null;
                }
                return allBones.FirstOrDefault(candidate => candidate.name == boneName);
            };
        }

        /// <summary>읽을 수 없는 메시는 삼각형을 거를 수 없으므로 원본 모델의 Read/Write를 켠다.</summary>
        private static void EnsureMeshesReadable(GameObject model, List<string> report)
        {
            if (model == null)
                return;
            foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null || skinned.sharedMesh.isReadable)
                    continue;
                string meshPath = AssetDatabase.GetAssetPath(skinned.sharedMesh);
                if (AssetImporter.GetAtPath(meshPath) is not ModelImporter importer || importer.isReadable)
                    continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
                report?.Add($"Read/Write 켬: {meshPath}");
            }
        }

        /// <summary>다시 구워도 GUID가 바뀌지 않아 프리팹 참조가 흔들리지 않도록, 있는 메시 에셋은 내용만 덮어쓴다.</summary>
        private static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            Directory.CreateDirectory(MeshFolder);
            foreach (char invalid in Path.GetInvalidFileNameChars())
                assetName = assetName.Replace(invalid, '_');
            string path = $"{MeshFolder}/{assetName}.asset";
            mesh.name = assetName;

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
