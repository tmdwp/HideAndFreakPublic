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
    /// <summary>
    /// Blender 스크립트로 만든 로우폴리 캐릭터(Art/Characters/Models)가 Unity Humanoid로 쓸 수 있는지 확인한다. (Phase 11-1)
    /// 1. FBX 임포트를 Humanoid(이 모델로 Avatar 만들기) + Read/Write로 맞춘다.
    /// 2. Avatar가 올바른 휴머노이드인지, 필수 뼈가 모두 연결됐는지, 정면(+Z)·왼쪽(−X)·키가 맞는지 검사한다.
    /// 3. 마네킹 애니메이터를 쓰는 시험용 외형(Skin_TestBody)을 만든다. 맵의 MapInfo → Mannequin Skin에 넣어 테스트 방에서
    ///    걷기·달리기·감정 표현을 확인한다. (에디터에서 휴머노이드 클립을 입혀 사진을 찍는 방식은 기존 더미 모델에서도
    ///    근육 자세가 적용되지 않아 쓰지 않는다)
    /// 결과는 Console과 Temp/CharacterCheck/report.txt에 남긴다.
    /// </summary>
    public static class LowPolyCharacterCheck
    {
        internal const string ModelFolder = "Assets/_Project/Art/Characters/Models";
        private const string TestBodyPath = ModelFolder + "/SK_TestBody.fbx";
        private const string TestSkinPath = CharacterAssetBuilder.SkinFolder + "/Skin_TestBody.asset";
        private const string MannequinControllerPath = CharacterAssetBuilder.SkinFolder + "/AC_MannequinHumanoid.controller";
        private const string ReportFolder = "Temp/CharacterCheck";

        private const float ExpectedHeight = 1.8f;

        /// <summary>
        /// 시험용 빈 몸(SK_TestBody)을 확인하고 시험용 외형을 만든다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/로우폴리 몸 Humanoid 확인 (SK_TestBody)")]
        private static void CheckTestBody()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // 검사용 인스턴스가 열린 씬에 남지 않도록 빈 씬에서 검사하고, 끝나면 원래 씬을 다시 연다.
            string previousScene = SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var report = new List<string>();
            bool ok = Check(TestBodyPath, report);
            if (ok)
            {
                CharacterSkin skin = CharacterAssetBuilder.CreateSkin("TestBody", TestBodyPath,
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MannequinControllerPath),
                    ExpectedHeight, useAimIK: false, TestSkinPath);
                if (skin != null)
                    report.Add($"시험용 외형: {TestSkinPath} (맵의 MapInfo → Mannequin Skin에 넣어 테스트 방에서 확인)");
                AssetDatabase.SaveAssets();
            }

            if (!string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(Path.Combine(ReportFolder, "report.txt"), string.Join("\n", report), Encoding.UTF8);
            string summary = string.Join("\n", report);
            if (ok)
                Debug.Log($"[LowPolyCharacterCheck] 통과: {TestBodyPath}\n{summary}");
            else
                Debug.LogError($"[LowPolyCharacterCheck] 실패: {TestBodyPath}\n{summary}");
        }

        /// <summary>
        /// 모델 하나를 Humanoid로 임포트하고 검사한다. 통과하면 true.
        /// </summary>
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

            bool ok = CheckBoneMapping(avatar, report);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = Object.Instantiate(source);
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                ok &= CheckOrientation(animator, report);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            return ok;
        }

        /// <summary>
        /// FBX 임포트를 Humanoid(이 모델로 Avatar 만들기), Read/Write, 애니메이션 없음으로 맞추고 다시 가져온다.
        /// </summary>
        private static bool ConfigureHumanoidImport(string modelPath, List<string> report)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
            {
                report.Add($"모델을 찾을 수 없습니다: {modelPath} (Blender 생성 스크립트를 먼저 실행)");
                return false;
            }

            bool changed = importer.animationType != ModelImporterAnimationType.Human
                           || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                           || !importer.isReadable || importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.isReadable = true;
            importer.importAnimation = false;
            if (changed)
                importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// 필수 휴머노이드 뼈가 모두 연결됐는지 확인하고 연결 목록을 남긴다.
        /// </summary>
        private static bool CheckBoneMapping(Avatar avatar, List<string> report)
        {
            Dictionary<string, string> mapped = avatar.humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            bool ok = true;
            for (int i = 0; i < HumanTrait.BoneCount; i++)
            {
                if (!HumanTrait.RequiredBone(i))
                    continue;
                string humanName = HumanTrait.BoneName[i];
                if (!mapped.ContainsKey(humanName))
                {
                    report.Add($"필수 뼈 연결 없음: {humanName}");
                    ok = false;
                }
            }
            report.Add($"연결된 휴머노이드 뼈 {mapped.Count}개: " +
                       string.Join(", ", mapped.OrderBy(m => m.Key).Select(m => m.Key == m.Value ? m.Key : $"{m.Key}={m.Value}")));
            return ok;
        }

        /// <summary>
        /// 기본 자세에서 정면이 +Z(발끝이 발목보다 앞), 왼손이 −X 쪽, 키가 사람 크기인지 확인한다.
        /// </summary>
        private static bool CheckOrientation(Animator animator, List<string> report)
        {
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            bool ok = true;

            if (leftFoot != null && leftToes != null)
            {
                float forward = leftToes.position.z - leftFoot.position.z;
                report.Add($"정면: 발끝 − 발목 z = {forward:F3} ({(forward > 0f ? "+Z 정면, 맞음" : "정면이 뒤집힘")})");
                ok &= forward > 0f;
            }
            if (leftHand != null)
            {
                report.Add($"왼손 x = {leftHand.position.x:F3} ({(leftHand.position.x < 0f ? "왼쪽 맞음" : "좌우가 뒤집힘")})");
                ok &= leftHand.position.x < 0f;
            }

            Bounds bounds = CharacterAssetBuilder.GetRendererBounds(animator.gameObject);
            report.Add($"키(경계 높이) {bounds.size.y:F2}m, 발 높이 {bounds.min.y:F2}m, 머리 뼈 높이 {(head != null ? head.position.y : 0f):F2}m");
            if (Mathf.Abs(bounds.size.y - ExpectedHeight) > 0.2f)
            {
                report.Add($"키가 {ExpectedHeight}m와 많이 다릅니다. (FBX 단위·크기 설정 확인)");
                ok = false;
            }
            return ok;
        }
    }
}
