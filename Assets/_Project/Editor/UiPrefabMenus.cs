using GuardVsMannequin.Room;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// UI 프리팹 기준본 저장·초기화 메뉴. (GameSettingTools > UI)
    /// </summary>
    public static class UiPrefabMenus
    {
        /// <summary>
        /// 지금 UI 프리팹을 초기화 기준본으로 저장한다. 이후 "UI 프리팹 초기화"는 이 모양으로 되돌린다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "UI/현재 UI를 초기화 기준으로 저장")]
        private static void SaveDefaults()
        {
            if (!EditorUtility.DisplayDialog("초기화 기준 저장",
                    "지금 UI 프리팹을 초기화 기준본으로 저장합니다.\n이후 'UI 프리팹 초기화'를 하면 이 모양으로 되돌아갑니다.", "저장", "취소"))
                return;

            // 프리팹 편집 화면에서 저장하지 않은 변경도 파일에 쓴다.
            AssetDatabase.SaveAssets();

            int saved = 0;
            foreach (string path in UiPrefabLibrary.AllPaths)
            {
                if (UiPrefabDefaults.Save(path))
                    saved++;
            }
            Debug.Log($"[UiPrefabMenus] UI 프리팹 {saved}개를 초기화 기준본({UiPrefabDefaults.Folder}/)으로 저장했습니다.");
        }

        /// <summary>
        /// UI 프리팹을 초기화 기준본으로 되돌린다. 기준본이 없는 프리팹은 지워서 다음 "테스트 환경 생성" 때 코드 기본 모양으로 만든다.
        /// (기준본 이후에 고친 내용이 사라지므로 확인을 받는다)
        /// </summary>
        [MenuItem(EditorMenus.Root + "UI/UI 프리팹 초기화 (저장한 기준으로 되돌리기)")]
        private static void ResetPrefabs()
        {
            if (!EditorUtility.DisplayDialog("UI 프리팹 초기화",
                    "UI 프리팹을 '현재 UI를 초기화 기준으로 저장'으로 저장한 모양으로 되돌립니다. 그 뒤에 고친 내용은 사라집니다.\n" +
                    "기준본이 없는 프리팹은 지워지며, '테스트 환경 생성'과 '맵 생성'을 다시 실행하면 코드 기본 모양으로 만들어집니다.", "되돌리기", "취소"))
                return;

            int restored = 0, deleted = 0;
            foreach (string path in UiPrefabLibrary.AllPaths)
            {
                if (UiPrefabDefaults.Restore(path))
                    restored++;
                else if (AssetDatabase.DeleteAsset(path))
                    deleted++;
            }
            Debug.Log($"[UiPrefabMenus] 기준본으로 되돌림 {restored}개, 기준본이 없어 지움 {deleted}개." +
                      (deleted > 0 ? " 지운 프리팹은 '테스트 환경 생성'을 실행하면 다시 만들어집니다." : string.Empty));
        }

        /// <summary>
        /// 지금 프리팹을 지우지 않고, 코드에는 있지만 프리팹에 빠진 UI(HUD 영역, 설정 화면의 키 줄)만 더한다.
        /// 이미 있는 영역은 건드리지 않으므로 여러 번 실행해도 된다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "UI/빠진 UI 영역·키 줄 추가 (기존 프리팹 유지)")]
        private static void AddNewUiSections()
        {
            AssetDatabase.SaveAssets();

            int hudAdded = EditPrefab(UiPrefabLibrary.HudCanvasPath, root => SharedUiBuilder.AddMissingHudSections(root.transform));
            int rowsAdded = EditPrefab(UiPrefabLibrary.SettingsPanelPath, SharedUiBuilder.AddMissingRebindRows);
            int lobbyAdded = EditPrefab(UiPrefabLibrary.LobbyCanvasPath, MenuSceneBuilder.AddMissingLobbySections);
            // 새 게임 설정 항목(예: 총 탄창·장전)의 줄: 룸 설정 화면과 HUD 테스트 메뉴
            int roomRowsAdded = EditPrefab(UiPrefabLibrary.RoomCanvasPath, root =>
                MenuSceneBuilder.AddMissingSettingRows(root.GetComponentInChildren<RoomSettingsView>(true), GameSettingDefinitions.IsShownInRoom));
            int testRowsAdded = EditPrefab(UiPrefabLibrary.HudCanvasPath, root =>
                MenuSceneBuilder.AddMissingSettingRows(root.GetComponentInChildren<HudTestRoomPanel>(true), id => id != GameSettingId.Map));

            Debug.Log($"[UiPrefabMenus] HUD 영역 {Describe(hudAdded)}, 키 설정 줄 {Describe(rowsAdded)}, 로비 영역(버전 표시·모드 화면·모드 경고 창) {Describe(lobbyAdded)}, " +
                      $"룸 설정 줄 {Describe(roomRowsAdded)}, 테스트 메뉴 설정 줄 {Describe(testRowsAdded)}. " +
                      "모양이 마음에 들면 '현재 UI를 초기화 기준으로 저장'으로 기준본도 갱신하세요.");
        }

        /// <summary>
        /// 프리팹을 열어 고치고, 바뀐 것이 있으면 저장한다.
        /// </summary>
        /// <param name="edit">고치는 함수. 추가한 개수(없으면 0, 대상을 못 찾으면 -1)를 반환한다.</param>
        /// <returns>edit의 결과. 프리팹이 없으면 -1.</returns>
        private static int EditPrefab(string path, System.Func<GameObject, int> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return -1;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int added = edit(root);
                if (added > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                return added;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 추가 결과를 로그 문구로 바꾼다.
        /// </summary>
        private static string Describe(int added)
        {
            return added switch
            {
                < 0 => "대상을 찾지 못함 (프리팹이 없으면 '테스트 환경 생성'이 새 모양으로 만듭니다)",
                0 => "추가할 것 없음",
                _ => $"{added}개 추가"
            };
        }

        /// <summary>
        /// 코드 기본 모양으로 되돌린다. 프리팹과 기준본을 모두 지운다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "UI/UI 프리팹 초기화 (코드 기본 모양, 기준본 무시)")]
        private static void ResetPrefabsToCodeDefaults()
        {
            if (!EditorUtility.DisplayDialog("코드 기본 모양으로 초기화",
                    "UI 프리팹을 지우고, '테스트 환경 생성' 때 기준본 대신 코드 기본 모양으로 다시 만듭니다.\n" +
                    $"그러려면 {UiPrefabDefaults.Folder} 폴더(기준본)도 지워야 합니다. 함께 지울까요?", "프리팹과 기준본 지우기", "취소"))
                return;

            foreach (string path in UiPrefabLibrary.AllPaths)
                AssetDatabase.DeleteAsset(path);
            UiPrefabDefaults.DeleteAll();
            Debug.Log("[UiPrefabMenus] UI 프리팹과 기준본을 지웠습니다. '테스트 환경 생성'과 '맵 생성'을 실행하세요.");
        }
    }
}
