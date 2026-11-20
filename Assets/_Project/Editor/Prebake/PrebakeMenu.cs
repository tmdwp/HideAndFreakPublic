using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>실행 중에 만들던 오브젝트·메시·에셋을 미리 만들어 두는 메뉴. 결과는 Temp/Prebake/report.txt에 남긴다.</summary>
    public static class PrebakeMenu
    {
        private const string MenuRoot = EditorMenus.Root + "미리 굽기/";
        private const string ReportPath = "Temp/Prebake/report.txt";

        /// <summary>새 프로젝트나 외형·맵을 고친 뒤 한 번에 맞추도록 모든 굽기를 차례로 한다.</summary>
        [MenuItem(MenuRoot + "전체 실행", priority = 0)]
        private static void BakeEverything()
        {
            if (!Confirm("외형 프리팹(1인칭 팔다리·판정 캡슐), 모든 맵 씬(경계 안개), Game_Systems 씬·Guard 프리팹(하늘·볼륨)을 고치고 저장합니다."))
                return;
            Run(report =>
            {
                SkinPartBaker.BakeAllSkins(report);
                RuntimeAssetBaker.BakeAll(report);
                BoundaryFogBaker.BakeAllMaps(report);
            });
        }

        /// <summary>외형 모델만 바꿨을 때 맵 씬을 다시 저장하지 않도록 따로 둔다.</summary>
        [MenuItem(MenuRoot + "외형 1인칭 팔다리·판정 캡슐", priority = 20)]
        private static void BakeSkins()
        {
            if (!Confirm("모든 외형 모델 프리팹(Art/Skins/Model_*)에 1인칭 팔다리와 판정 캡슐을 다시 굽습니다."))
                return;
            Run(SkinPartBaker.BakeAllSkins);
        }

        /// <summary>조정 파일의 안개 깊이를 바꿔도 실행 중에 다시 맞추므로, 맵 경계가 바뀐 경우에만 다시 돌리면 된다.</summary>
        [MenuItem(MenuRoot + "경계 안개 (전체 맵)", priority = 21)]
        private static void BakeBoundaryFog()
        {
            if (!Confirm("모든 맵 씬과 테스트 씬을 열어 경계 안개를 다시 넣고 저장합니다."))
                return;
            Run(BoundaryFogBaker.BakeAllMaps);
        }

        /// <summary>하늘·볼륨·UI 에셋만 다시 확인하도록 따로 둔다.</summary>
        [MenuItem(MenuRoot + "연출 에셋 (하늘·볼륨·로딩 UI)", priority = 22)]
        private static void BakeRuntimeAssets()
        {
            if (!Confirm("없는 연출 에셋을 만들고 Game_Systems 씬·Guard 프리팹의 빈 칸에 연결합니다. 이미 있는 에셋과 채워진 칸은 그대로 둡니다."))
                return;
            Run(RuntimeAssetBaker.BakeAll);
        }

        /// <summary>여러 씬·프리팹을 저장하는 작업이라 실행 전에 확인을 받고, 열린 씬의 수정 내용부터 저장할지 묻는다.</summary>
        private static bool Confirm(string message)
        {
            return EditorUtility.DisplayDialog("미리 굽기", message + "\n계속할까요?", "실행", "취소")
                   && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        /// <summary>결과를 한 파일에 모아 무엇이 바뀌었는지 나중에 확인할 수 있게 한다.</summary>
        private static void Run(System.Action<List<string>> bake)
        {
            var report = new List<string>();
            try
            {
                bake(report);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllLines(ReportPath, report);
                Debug.Log("[PrebakeMenu] 미리 굽기 결과\n" + string.Join("\n", report));
            }
            EditorUtility.DisplayDialog("미리 굽기", $"완료했습니다. ({report.Count}줄)\n자세한 내용: {ReportPath}", "확인");
        }
    }
}
