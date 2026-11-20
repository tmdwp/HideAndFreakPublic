using System;
using System.Collections.Generic;
using System.IO;
using GuardVsMannequin.Map;
using GuardVsMannequin.Player.Guard;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 실행 중에 만들던 하늘·볼륨 프로필·오버레이 UI를 에셋으로 만들어 두고 씬·프리팹에 연결한다.
    /// 이미 있는 에셋은 사용자가 고친 값을 덮어쓰지 않도록 그대로 쓰고, 비어 있는 칸만 채운다.
    /// </summary>
    public static class RuntimeAssetBaker
    {
        private const string BakedFolder = "Assets/_Project/Art/Baked";
        private const string ResourcesUiFolder = "Assets/_Project/Resources/UI";
        private const string GuardPrefabPath = "Assets/_Project/Prefabs/Guard.prefab";

        /// <summary>메뉴 하나로 연출 에셋을 모두 준비하도록 만들기와 연결을 차례로 한다.</summary>
        public static void BakeAll(List<string> report)
        {
            Material darkSkybox = GetOrCreateAsset($"{BakedFolder}/M_HuntDarkSkybox.mat", HuntingLightingController.CreateDarkSkybox, report);
            VolumeProfile lowHealthProfile = GetOrCreateProfile($"{BakedFolder}/VP_LowHealth.asset", LowHealthVignette.CreateDefaultProfile, report);
            VolumeProfile dreadProfile = GetOrCreateProfile($"{BakedFolder}/VP_GuardDread.asset", GuardDreadEffect.CreateDefaultProfile, report);

            GetOrCreatePrefab($"{ResourcesUiFolder}/{Path.GetFileName(PerformanceStatsOverlay.PrefabPath)}.prefab", CreatePerformanceOverlay, report);
            GetOrCreatePrefab($"{ResourcesUiFolder}/{Path.GetFileName(SceneLoadingOverlay.PrefabPath)}.prefab", CreateLoadingOverlay, report);
            GetOrCreatePrefab($"{ResourcesUiFolder}/{Path.GetFileName(ConfirmDialog.PrefabPath)}.prefab", CreateConfirmDialog, report);

            Material flashlightBeam = GetOrCreateAsset($"{BakedFolder}/M_FlashlightBeam.mat", FlashlightBeamView.CreateDefaultMaterial, report);

            ConnectGuardPrefab(dreadProfile, flashlightBeam, report);
            ConnectGameSystemsScene(darkSkybox, lowHealthProfile, report);
        }

        #region 에셋 만들기

        /// <summary>사용자가 색·값을 바꿨을 수 있으므로 이미 있으면 다시 만들지 않는다.</summary>
        private static T GetOrCreateAsset<T>(string path, Func<T> create, List<string> report) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            T created = create();
            if (created == null)
            {
                report.Add($"만들지 못함: {path}");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(created, path);
            report.Add($"만듦: {path}");
            return created;
        }

        /// <summary>프로필의 효과 항목은 따로 저장되지 않으면 다시 열 때 사라지므로 하위 에셋으로 함께 넣는다.</summary>
        private static VolumeProfile GetOrCreateProfile(string path, Func<VolumeProfile> create, List<string> report)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null)
                return existing;

            VolumeProfile profile = GetOrCreateAsset(path, create, report);
            if (profile == null)
                return null;
            foreach (VolumeComponent component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>프리팹은 사용자가 모양을 고쳤을 수 있으므로 이미 있으면 그대로 쓴다.</summary>
        private static GameObject GetOrCreatePrefab(string path, Func<GameObject> create, List<string> report)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GameObject root = create();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.Add($"만듦: {path}");
            return prefab;
        }

        /// <summary>실행 중 대체와 같은 모양이 되도록 오버레이 자신의 기본 모양 만들기를 쓴다.</summary>
        private static GameObject CreatePerformanceOverlay()
        {
            var root = new GameObject(nameof(PerformanceStatsOverlay));
            root.AddComponent<PerformanceStatsOverlay>().EnsureLayout();
            return root;
        }

        /// <summary>실행 중 대체와 같은 모양이 되도록 오버레이 자신의 기본 모양 만들기를 쓴다.</summary>
        private static GameObject CreateLoadingOverlay()
        {
            var root = new GameObject(nameof(SceneLoadingOverlay));
            root.AddComponent<SceneLoadingOverlay>().EnsureView();
            return root;
        }

        /// <summary>실행 중 대체와 같은 모양이 되도록 확인 창 자신의 기본 모양 만들기를 쓴다.</summary>
        private static GameObject CreateConfirmDialog()
        {
            var root = new GameObject(nameof(ConfirmDialog));
            root.AddComponent<ConfirmDialog>().EnsureView();
            return root;
        }

        #endregion

        #region 연결

        /// <summary>가드 프리팹의 빈 칸만 채워, 사용자가 직접 넣은 프로필은 바꾸지 않는다.</summary>
        private static void ConnectGuardPrefab(VolumeProfile dreadProfile, Material flashlightBeam, List<string> report)
        {
            if (!File.Exists(GuardPrefabPath))
            {
                report.Add($"가드 프리팹 없음: {GuardPrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(GuardPrefabPath);
            try
            {
                int filled = 0;
                foreach (GuardDreadEffect dreadEffect in root.GetComponentsInChildren<GuardDreadEffect>(true))
                    filled += FillIfEmpty(dreadEffect, "profileTemplate", dreadProfile);
                // 빌드에서 셰이더를 찾지 못해 빛줄기가 빠지지 않도록 머티리얼을 프리팹이 참조하게 한다.
                foreach (FlashlightTool flashlight in root.GetComponentsInChildren<FlashlightTool>(true))
                    filled += FillIfEmpty(flashlight, "beamMaterial", flashlightBeam);
                foreach (GrenadeView grenadeView in root.GetComponentsInChildren<GrenadeView>(true))
                {
                    filled += FillIfEmpty(grenadeView, "grenadeModel", GrenadeAssets.GetSmokeGrenadeModel());
                    filled += FillIfEmpty(grenadeView, "smokeMaterial", GrenadeAssets.GetSmokeMaterial());
                }

                if (filled > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, GuardPrefabPath);
                report.Add($"Guard.prefab: 빈 칸 {filled}개 연결");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 사냥 조명·체력 비네트는 시스템 씬에 있으므로 씬을 추가로 열어 연결하고, 원래 열려 있지 않았으면 다시 닫는다.
        /// </summary>
        private static void ConnectGameSystemsScene(Material darkSkybox, VolumeProfile lowHealthProfile, List<string> report)
        {
            string scenePath = GameSystemsSceneBuilder.ScenePath;
            if (!File.Exists(scenePath))
            {
                report.Add($"시스템 씬 없음: {scenePath}");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            int filled = 0;
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (HuntingLightingController lighting in rootObject.GetComponentsInChildren<HuntingLightingController>(true))
                {
                    filled += FillIfEmpty(lighting, "darkSkyboxMaterial", darkSkybox);
                    filled += AddAudioSourceIfMissing(lighting.gameObject);
                }
                foreach (LowHealthVignette vignette in rootObject.GetComponentsInChildren<LowHealthVignette>(true))
                {
                    filled += FillIfEmpty(vignette, "profileTemplate", lowHealthProfile);
                    filled += AddVolumeIfMissing(vignette.gameObject, lowHealthProfile);
                }
            }

            if (filled > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);
            report.Add($"Game_Systems: {filled}개 연결");
        }

        /// <summary>사용자가 이미 넣은 값은 존중하도록 비어 있을 때만 넣고, 넣었으면 1을 돌려준다.</summary>
        private static int FillIfEmpty(Component target, string propertyName, Object value)
        {
            if (value == null)
                return 0;
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
                return 0;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>실행 중 대체와 같은 설정이 되도록 컨트롤러의 설정 함수를 같이 쓴다.</summary>
        private static int AddAudioSourceIfMissing(GameObject target)
        {
            if (target.GetComponent<AudioSource>() != null)
                return 0;
            HuntingLightingController.ConfigureAudioSource(target.AddComponent<AudioSource>());
            return 1;
        }

        /// <summary>실행 중에 붙이던 볼륨을 같은 우선순위로 미리 둔다.</summary>
        private static int AddVolumeIfMissing(GameObject target, VolumeProfile profile)
        {
            if (target.GetComponent<Volume>() != null)
                return 0;
            Volume volume = target.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = LowHealthVignette.VolumePriority;
            volume.sharedProfile = profile;
            return 1;
        }

        #endregion
    }
}
