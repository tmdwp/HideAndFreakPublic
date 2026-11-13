using GuardVsMannequin.Room;
using GuardVsMannequin.UI;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>UI를 망가뜨려도 되돌릴 수 있도록 기준본 저장·초기화 메뉴를 둔다.</summary>
    public static class UiPrefabMenus
    {
        /// <summary>UI를 다듬은 뒤 실수로 망가뜨려도 되돌릴 수 있도록 지금 모양을 기준본으로 남긴다.</summary>
        [MenuItem(EditorMenus.Root + "UI/현재 UI를 초기화 기준으로 저장")]
        private static void SaveDefaults()
        {
            if (!EditorUtility.DisplayDialog("초기화 기준 저장",
                    "지금 UI 프리팹을 초기화 기준본으로 저장합니다.\n이후 'UI 프리팹 초기화'를 하면 이 모양으로 되돌아갑니다.", "저장", "취소"))
                return;

            // 프리팹 편집 화면에서 저장하지 않은 변경도 파일에 쓴다.
            AssetDatabase.SaveAssets();

            int savedCount = 0;
            foreach (string prefabPath in UiPrefabLibrary.AllPaths)
            {
                if (UiPrefabDefaults.Save(prefabPath))
                    savedCount++;
            }
            Debug.Log($"[UiPrefabMenus] UI 프리팹 {savedCount}개를 초기화 기준본({UiPrefabDefaults.Folder}/)으로 저장했습니다.");
        }

        /// <summary>기준본이 없는 프리팹은 되돌릴 곳이 없어 지우고 다음 생성 때 코드 기본 모양으로 만든다.</summary>
        [MenuItem(EditorMenus.Root + "UI/UI 프리팹 초기화 (저장한 기준으로 되돌리기)")]
        private static void ResetPrefabs()
        {
            if (!EditorUtility.DisplayDialog("UI 프리팹 초기화",
                    "UI 프리팹을 '현재 UI를 초기화 기준으로 저장'으로 저장한 모양으로 되돌립니다. 그 뒤에 고친 내용은 사라집니다.\n" +
                    "기준본이 없는 프리팹은 지워지며, '테스트 환경 생성'과 '맵 생성'을 다시 실행하면 코드 기본 모양으로 만들어집니다.", "되돌리기", "취소"))
                return;

            int restoredCount = 0, deletedCount = 0;
            foreach (string prefabPath in UiPrefabLibrary.AllPaths)
            {
                if (UiPrefabDefaults.Restore(prefabPath))
                    restoredCount++;
                else if (AssetDatabase.DeleteAsset(prefabPath))
                    deletedCount++;
            }
            Debug.Log($"[UiPrefabMenus] 기준본으로 되돌림 {restoredCount}개, 기준본이 없어 지움 {deletedCount}개." +
                      (deletedCount > 0 ? " 지운 프리팹은 '테스트 환경 생성'을 실행하면 다시 만들어집니다." : string.Empty));
        }

        /// <summary>손으로 다듬은 모양을 지키려고 프리팹을 초기화하지 않고 빠진 UI만 더한다.</summary>
        [MenuItem(EditorMenus.Root + "UI/빠진 UI 영역·키 줄 추가 (기존 프리팹 유지)")]
        private static void AddNewUiSections()
        {
            AssetDatabase.SaveAssets();

            int hudAdded = EditPrefab(UiPrefabLibrary.HudCanvasPath, root => SharedUiBuilder.AddMissingHudSections(root.transform));
            int rowsAdded = EditPrefab(UiPrefabLibrary.SettingsPanelPath, SharedUiBuilder.AddMissingRebindRows);
            int graphicsRowsAdded = EditPrefab(UiPrefabLibrary.SettingsPanelPath, SharedUiBuilder.AddMissingGraphicsRows);
            int lobbyAdded = EditPrefab(UiPrefabLibrary.LobbyCanvasPath, MenuSceneBuilder.AddMissingLobbySections);
            int roomAdded = EditPrefab(UiPrefabLibrary.RoomCanvasPath, MenuSceneBuilder.AddMissingRoomSections);
            int roomPlayersAdded = EditPrefab(UiPrefabLibrary.RoomCanvasPath, MenuSceneBuilder.AddMissingPlayerPanel);
            int hudPlayersAdded = EditPrefab(UiPrefabLibrary.HudCanvasPath, MenuSceneBuilder.AddMissingPlayerPanel);
            // 새 게임 설정 항목(예: 총 탄창·장전)의 줄: 룸 설정 화면과 HUD 테스트 메뉴
            int roomRowsAdded = EditPrefab(UiPrefabLibrary.RoomCanvasPath, root =>
                MenuSceneBuilder.AddMissingSettingRows(root.GetComponentInChildren<RoomSettingsView>(true), GameSettingDefinitions.IsShownInRoom));
            int testRowsAdded = EditPrefab(UiPrefabLibrary.HudCanvasPath, root =>
                MenuSceneBuilder.AddMissingSettingRows(root.GetComponentInChildren<HudTestRoomPanel>(true), id => id != GameSettingId.Map));

            Debug.Log($"[UiPrefabMenus] HUD 영역 {Describe(hudAdded)}, 키 설정 줄 {Describe(rowsAdded)}, 그래픽 설정 줄 {Describe(graphicsRowsAdded)}, 로비 영역(버전 표시·모드 화면·방 이름·공개방 찾기) {Describe(lobbyAdded)}, 룸 영역(모드 설치 안내) {Describe(roomAdded)}, 플레이어 관리(룸 {Describe(roomPlayersAdded)}, HUD {Describe(hudPlayersAdded)}), " +
                      $"룸 설정 줄 {Describe(roomRowsAdded)}, 테스트 메뉴 설정 줄 {Describe(testRowsAdded)}. " +
                      "모양이 마음에 들면 '현재 UI를 초기화 기준으로 저장'으로 기준본도 갱신하세요.");
        }

        /// <summary>씬 인스턴스를 고치면 프리팹에 반영되지 않으므로 프리팹 내용을 직접 열어 고친다.</summary>
        /// <param name="edit">고치는 함수로, 추가한 개수(대상이 없으면 -1)를 돌려준다.</param>
        /// <returns>edit의 결과이며, 프리팹이 없으면 -1.</returns>
        private static int EditPrefab(string path, System.Func<GameObject, int> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return -1;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int addedCount = edit(prefabRoot);
                if (addedCount > 0)
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                return addedCount;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>결과를 한 줄 로그로 보여 주려고 숫자를 사람이 읽는 문구로 바꾼다.</summary>
        private static string Describe(int addedCount)
        {
            return addedCount switch
            {
                < 0 => "대상을 찾지 못함 (프리팹이 없으면 '테스트 환경 생성'이 새 모양으로 만듭니다)",
                0 => "추가할 것 없음",
                _ => $"{addedCount}개 추가"
            };
        }

        /// <summary>기준본이 남으면 프리팹을 지워도 기준본으로 다시 만들어지므로 기준본까지 지운다.</summary>
        [MenuItem(EditorMenus.Root + "UI/UI 프리팹 초기화 (코드 기본 모양, 기준본 무시)")]
        private static void ResetPrefabsToCodeDefaults()
        {
            if (!EditorUtility.DisplayDialog("코드 기본 모양으로 초기화",
                    "UI 프리팹을 지우고, '테스트 환경 생성' 때 기준본 대신 코드 기본 모양으로 다시 만듭니다.\n" +
                    $"그러려면 {UiPrefabDefaults.Folder} 폴더(기준본)도 지워야 합니다. 함께 지울까요?", "프리팹과 기준본 지우기", "취소"))
                return;

            foreach (string prefabPath in UiPrefabLibrary.AllPaths)
                AssetDatabase.DeleteAsset(prefabPath);
            UiPrefabDefaults.DeleteAll();
            Debug.Log("[UiPrefabMenus] UI 프리팹과 기준본을 지웠습니다. '테스트 환경 생성'과 '맵 생성'을 실행하세요.");
        }
    }
}
