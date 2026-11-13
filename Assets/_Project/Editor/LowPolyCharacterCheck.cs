using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuardVsMannequin.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>Blender로 만든 로우폴리 캐릭터가 Unity Humanoid로 쓰일 수 있는지 확인한다.</summary>
    public static class LowPolyCharacterCheck
    {
        internal const string ModelFolder = "Assets/_Project/Art/Characters/Models";
        private const string TestBodyPath = ModelFolder + "/SK_TestBody.fbx";
        private const string TestSkinPath = CharacterAssetBuilder.SkinFolder + "/Skin_TestBody.asset";
        private const string MannequinControllerPath = CharacterAssetBuilder.SkinFolder + "/AC_MannequinHumanoid.controller";
        private const string ReportFolder = "Temp/CharacterCheck";

        private const float ExpectedHeight = 1.8f;

        /// <summary>게임에 넣기 전에 Humanoid 애니메이션이 맞게 붙는지 확인하고 테스트 방용 시험 외형을 만든다.</summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/로우폴리 몸 Humanoid 확인 (SK_TestBody)")]
        private static void CheckTestBody()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 검사용 인스턴스가 열린 씬에 남지 않도록 빈 씬에서 검사하고, 끝나면 원래 씬을 다시 연다.
            string previousScenePath = SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var report = new List<string>();
            bool isPassed = Check(TestBodyPath, report);
            if (isPassed)
            {
                CharacterSkin skin = CharacterAssetBuilder.CreateSkin("TestBody", TestBodyPath,
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MannequinControllerPath),
                    ExpectedHeight, useAimIK: false, TestSkinPath);
                if (skin != null)
                    report.Add($"시험용 외형: {TestSkinPath} (맵의 MapInfo → Mannequin Skin에 넣어 테스트 방에서 확인)");
                AssetDatabase.SaveAssets();
            }

            if (!string.IsNullOrEmpty(previousScenePath))
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);

            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(Path.Combine(ReportFolder, "report.txt"), string.Join("\n", report), Encoding.UTF8);
            string summary = string.Join("\n", report);
            if (isPassed)
                Debug.Log($"[LowPolyCharacterCheck] 통과: {TestBodyPath}\n{summary}");
            else
                Debug.LogError($"[LowPolyCharacterCheck] 실패: {TestBodyPath}\n{summary}");
        }

        /// <summary>문제를 한 번에 모두 보이도록 실패해도 가능한 검사는 끝까지 한다.</summary>
        internal static bool Check(string modelPath, List<string> report)
        {
            if (!ConfigureHumanoidImport(modelPath, report))
                return false;

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                report.Add("Avatar가 올바른 휴머노이드가 아닙니다. (Console의 임포트 경고와 뼈 이름을 확인)");
                return false;
            }

            bool isPassed = CheckBoneMapping(avatar, report);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = Object.Instantiate(modelAsset);
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                isPassed &= CheckOrientation(animator, report);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            return isPassed;
        }

        /// <summary>정점을 검사에 읽으려면 Read/Write가 필요해 켜고, 설정이 다를 때만 다시 가져온다.</summary>
        private static bool ConfigureHumanoidImport(string modelPath, List<string> report)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
            {
                report.Add($"모델을 찾을 수 없습니다: {modelPath} (Blender 생성 스크립트를 먼저 실행)");
                return false;
            }

            bool isChanged = importer.animationType != ModelImporterAnimationType.Human
                             || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                             || !importer.isReadable || importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.isReadable = true;
            importer.importAnimation = false;
            if (isChanged)
                importer.SaveAndReimport();
            return true;
        }

        /// <summary>필수 뼈가 하나라도 빠지면 Humanoid 클립이 엉뚱하게 재생되므로 확인한다.</summary>
        private static bool CheckBoneMapping(Avatar avatar, List<string> report)
        {
            Dictionary<string, string> mappedBones = avatar.humanDescription.human.ToDictionary(humanBone => humanBone.humanName, humanBone => humanBone.boneName);
            bool isPassed = true;
            for (int i = 0; i < HumanTrait.BoneCount; i++)
            {
                if (!HumanTrait.RequiredBone(i))
                    continue;
                string humanName = HumanTrait.BoneName[i];
                if (!mappedBones.ContainsKey(humanName))
                {
                    report.Add($"필수 뼈 연결 없음: {humanName}");
                    isPassed = false;
                }
            }
            report.Add($"연결된 휴머노이드 뼈 {mappedBones.Count}개: " +
                       string.Join(", ", mappedBones.OrderBy(bone => bone.Key).Select(bone => bone.Key == bone.Value ? bone.Key : $"{bone.Key}={bone.Value}")));
            return isPassed;
        }

        /// <summary>
        /// Blender와 Unity의 축이 달라 내보내기 설정이 틀리면 캐릭터가 뒤돌거나 좌우가 바뀌므로, 기본 자세의 뼈 위치로 정면(+Z)·왼쪽(−X)·키를 확인한다.
        /// </summary>
        private static bool CheckOrientation(Animator animator, List<string> report)
        {
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            bool isPassed = true;

            if (leftFoot != null && leftToes != null)
            {
                float toeForwardOffset = leftToes.position.z - leftFoot.position.z;
                report.Add($"정면: 발끝 − 발목 z = {toeForwardOffset:F3} ({(toeForwardOffset > 0f ? "+Z 정면, 맞음" : "정면이 뒤집힘")})");
                isPassed &= toeForwardOffset > 0f;
            }
            if (leftHand != null)
            {
                report.Add($"왼손 x = {leftHand.position.x:F3} ({(leftHand.position.x < 0f ? "왼쪽 맞음" : "좌우가 뒤집힘")})");
                isPassed &= leftHand.position.x < 0f;
            }

            Bounds bounds = CharacterAssetBuilder.GetRendererBounds(animator.gameObject);
            report.Add($"키(경계 높이) {bounds.size.y:F2}m, 발 높이 {bounds.min.y:F2}m, 머리 뼈 높이 {(head != null ? head.position.y : 0f):F2}m");
            if (Mathf.Abs(bounds.size.y - ExpectedHeight) > 0.2f)
            {
                report.Add($"키가 {ExpectedHeight}m와 많이 다릅니다. (FBX 단위·크기 설정 확인)");
                isPassed = false;
            }
            return isPassed;
        }
    }
}
