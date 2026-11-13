using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>처음 맵을 만드는 사람도 단계를 따라 게임용 맵을 완성하도록 돕는 창.</summary>
    public class MapMakerWindow : EditorWindow
    {
        private string newMapName = "MyMap";
        private List<MapValidator.Issue> issues = new();
        private bool validated;
        private Vector2 scroll;

        /// <summary>단계 설명이 잘리지 않도록 최소 크기를 둔 채 연다.</summary>
        [MenuItem(EditorMenus.Root + "맵 제작/맵 제작 도우미")]
        private static void Open()
        {
            GetWindow<MapMakerWindow>("맵 제작 도우미").minSize = new Vector2(360f, 520f);
        }

        /// <summary>순서대로 따라 할 수 있도록 단계별 버튼을 늘어놓고, 맵이 아닌 씬에서는 맵 전용 단계를 막는다.</summary>
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Scene scene = SceneManager.GetActiveScene();
            bool isMap = MapAuthoringTools.FindMapRoot(scene) != null;

            EditorGUILayout.HelpBox(isMap
                ? $"열린 맵: {scene.name}\n변경 후 씬 저장(Ctrl+S)을 잊지 마세요."
                : "열린 씬이 맵이 아닙니다. 새 맵을 만들거나 맵 씬(Scenes 또는 Scenes/Custom)을 여세요.", isMap ? MessageType.Info : MessageType.Warning);

            Section("1. 새 맵 만들기",
                "게임 시스템·UI·경계·기본 지점이 들어 있는 빈 맵을 Scenes/Custom에 만듭니다.");
            newMapName = EditorGUILayout.TextField("맵 이름 (영문)", newMapName);
            if (GUILayout.Button("새 맵 만들기"))
            {
                MapAuthoringTools.CreateNewMap(newMapName);
                // 버튼 처리 중에 씬이 바뀌어 남은 레이아웃이 어긋나므로, 이번 GUI 그리기를 여기서 끝낸다.
                GUIUtility.ExitGUI();
            }

            using (new EditorGUI.DisabledScope(!isMap))
            {
                Section("2. 모델 배치",
                    "맵 모델(프리팹)을 Hierarchy의 Map/Environment 아래로 끌어다 놓으세요. 시작용 바닥은 지워도 됩니다.\n" +
                    "캐릭터 키 기준: 사람 1.8m, 문 높이 2m 이상 권장.");

                Section("3. 선택한 오브젝트 등록", "Hierarchy에서 오브젝트를 선택한 뒤 누르세요. (자식 포함, Ctrl+Z로 되돌리기 가능)");
                if (GUILayout.Button("충돌체 추가 (걸어 다닐 바닥·벽·사물)"))
                    Report("충돌체", MapAuthoringTools.AddCollidersToSelection());
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("변신 사물로 등록"))
                        Report("변신 사물", MapAuthoringTools.RegisterSelectedAsProps());
                    if (GUILayout.Button("등록 해제"))
                        Report("등록 해제", MapAuthoringTools.UnregisterSelected());
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("선택한 조명 등록"))
                        Report("조명", MapAuthoringTools.RegisterLights(selectionOnly: true));
                    if (GUILayout.Button("모든 조명 등록"))
                        Report("조명", MapAuthoringTools.RegisterLights(selectionOnly: false));
                }
                EditorGUILayout.LabelField("등록한 조명은 사냥 시간에 꺼집니다.", EditorStyles.miniLabel);

                Section("4. 지점 배치", "씬 뷰 화면 중앙 아래 바닥에 지점을 추가합니다. 추가 후 이동 도구(W)로 옮기세요.");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("전원 장치 후보"))
                        MapEditTools.AddGeneratorPoint();
                    if (GUILayout.Button("마네킹 스폰"))
                        MapEditTools.AddMannequinSpawn();
                    if (GUILayout.Button("가드 스폰 후보"))
                        MapEditTools.AddGuardSpawn();
                }
                if (GUILayout.Button("전원 장치 후보 넓게 다시 배치 (바닥 자동 분석)"))
                    MapEditTools.RespreadGenerators();
                EditorGUILayout.LabelField("가드는 판마다 마네킹 스폰에서 먼 곳(가드 스폰 후보 + 바닥 무작위 위치)에서 시작합니다.", EditorStyles.miniLabel);

                Section("5. 경계", "플레이어가 맵 밖으로 나가지 않도록 투명 벽·천장을 Environment 크기에 맞춥니다.");
                if (GUILayout.Button("경계를 맵 크기에 맞추기"))
                    MapAuthoringTools.FitBoundaryToEnvironment();

                Section("6. 검사", "게임에서 동작하는 데 필요한 것을 확인합니다.");
                if (GUILayout.Button("맵 검사"))
                {
                    issues = MapValidator.Validate();
                    validated = true;
                }
                DrawIssues();

                Section("7. 최적화", "경계 밖 오브젝트 제거, 정적 플래그, 오클루전 컬링 굽기. (씬 저장 후, 수 분 걸릴 수 있음)");
                if (GUILayout.Button("현재 맵 최적화"))
                    MapOptimizer.OptimizeActiveScene();
            }

            Section("8. 맵 목록 갱신", "빌드 설정, 룸의 맵 목록, 맵 이름 문구(strings.csv), 맵 설정 범위를 맞춥니다. 맵을 추가·삭제한 뒤 실행하세요.");
            if (GUILayout.Button("맵 목록 갱신"))
                MapRegistrySync.Sync();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 단계마다 같은 모양으로 보이도록 제목·설명 그리기를 한곳에 둔다.
        /// </summary>
        private static void Section(string title, string description)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>검사 전에는 "문제 없음"으로 오해하지 않도록 아무것도 그리지 않는다.</summary>
        private void DrawIssues()
        {
            if (!validated)
                return;
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("문제가 없습니다.", MessageType.Info);
                return;
            }

            foreach (MapValidator.Issue issue in issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.message, issue.isError ? MessageType.Error : MessageType.Warning);
                    if (issue.context != null && GUILayout.Button("선택", GUILayout.Width(44f), GUILayout.Height(38f)))
                    {
                        Selection.activeObject = issue.context;
                        EditorGUIUtility.PingObject(issue.context);
                        SceneView.FrameLastActiveSceneView();
                    }
                }
            }
        }

        /// <summary>Console을 열지 않아도 결과를 알 수 있게 창 알림으로 보여 준다.</summary>
        private void Report(string label, int changedCount)
        {
            ShowNotification(new GUIContent($"{label} {changedCount}개"));
            validated = false;
        }
    }
}
