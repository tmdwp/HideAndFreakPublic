using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 엑셀·메모장으로 CSV를 고치다 따옴표·쉼표·오타 때문에 게임이 줄을 조용히 무시하는 일을 막도록, 표 편집과 저장 전 검사를 함께 제공하는 창.
    /// </summary>
    public class CsvDataEditorWindow : EditorWindow
    {
        private const float RowHeight = 20f;
        private const float HeaderHeight = 22f;
        private const float LineNumberWidth = 46f;
        private const float ActionButtonWidth = 24f;
        private const int ActionButtonCount = 4;
        private const float MinColumnWidth = 50f;
        private const float ResizeHandleWidth = 8f;
        private const float BottomPanelHeight = 220f;
        private const float StatusBarHeight = 20f;
        private const float ScrollBarAllowance = 16f;
        private const string CellEditorControlName = "CsvCellEditor";
        private const string WidthPrefsPrefix = "GuardVsMannequin.CsvEditor.Width.";

        private static readonly Color SelectedRowColor = new Color(0.25f, 0.5f, 0.95f, 0.22f);
        private static readonly Color SelectedCellColor = new Color(0.3f, 0.6f, 1f, 1f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.2f, 0.2f, 0.3f);
        private static readonly Color WarningColor = new Color(1f, 0.75f, 0.1f, 0.25f);
        private static readonly Color InfoColor = new Color(0.3f, 0.6f, 1f, 0.1f);

        // 되돌리기(Ctrl+Z)와 스크립트 재컴파일 뒤에도 편집 중인 내용이 남도록 직렬화한다.
        [SerializeField] private string selectedPath;
        [SerializeField] private string workingText;
        [SerializeField] private bool showFileNotes = true;
        [SerializeField] private bool showBottomPanel = true;
        [SerializeField] private bool showInfoIssues = true;

        private List<CsvTableProfile> profiles = new List<CsvTableProfile>();
        private CsvTableProfile profile;
        private CsvTableDocument document;
        private string savedText;
        private DateTime loadedWriteTime;
        private bool isChangedOnDisk;

        private List<CsvIssue> issues = new List<CsvIssue>();
        private readonly Dictionary<CsvLine, List<CsvIssue>> issuesByLine = new Dictionary<CsvLine, List<CsvIssue>>();
        private readonly Dictionary<CsvLine, int> lineNumbers = new Dictionary<CsvLine, int>();
        private readonly Dictionary<CsvLine, int> sectionRowCounts = new Dictionary<CsvLine, int>();
        private int rowCount;
        private int errorCount;
        private int warningCount;
        private int infoCount;

        private SearchField searchField;
        private string searchText = string.Empty;
        private bool showOnlyProblems;
        private bool showOnlyEmptyCells;
        private readonly HashSet<string> collapsedSections = new HashSet<string>();
        private readonly List<CsvLine> visibleLines = new List<CsvLine>();

        private Vector2 tableScroll;
        private Vector2 detailScroll;
        private Vector2 issueScroll;
        private Rect lastContentRect;
        private CsvLine selectedLine;
        private int selectedColumn;
        private CsvLine scrollTarget;

        private CsvLine editingLine;
        private int editingColumn = -1;
        private string editBuffer;
        private bool isEditorFocusPending;
        private string detailUndoKey;

        private float[] columnWidths = new float[0];
        private int resizingColumn = -1;
        private float resizeStartMouseX;
        private float resizeStartWidth;

        private PopupWindowContent pendingPrompt;
        private Rect pendingPromptRect;

        private GUIStyle cellStyle;
        private GUIStyle lineNumberStyle;
        private GUIStyle columnHeaderStyle;
        private GUIStyle issueButtonStyle;
        private GUIStyle pathStyle;

        [MenuItem(EditorMenus.Root + "데이터/CSV 편집기")]
        private static void Open()
        {
            GetWindow<CsvDataEditorWindow>().minSize = new Vector2(720f, 420f);
        }

        [MenuItem("Assets/CSV 편집기로 열기", true)]
        private static bool CanOpenSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return CsvTableCatalog.FindAll().Any(candidate => candidate.AssetPath == path);
        }

        [MenuItem("Assets/CSV 편집기로 열기", false, 30)]
        private static void OpenSelected()
        {
            CsvDataEditorWindow window = GetWindow<CsvDataEditorWindow>();
            window.minSize = new Vector2(720f, 420f);
            window.SwitchTo(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        /// <summary>Unity 전체 저장(Ctrl+S) 대신 이 창이 포커스일 때는 CSV를 저장하도록 창 전용 단축키로 둔다.</summary>
        [Shortcut("GameSettingTools/CSV 편집기 저장", typeof(CsvDataEditorWindow), KeyCode.S, ShortcutModifiers.Action)]
        private static void SaveShortcut(ShortcutArguments arguments)
        {
            (arguments.context as CsvDataEditorWindow)?.TrySave();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("CSV 편집기", EditorGUIUtility.IconContent("TextAsset Icon").image);
            saveChangesMessage = "CSV 편집기에 저장하지 않은 변경이 있습니다. 저장할까요?";
            searchField = new SearchField();
            Undo.undoRedoPerformed += OnUndoRedo;

            profiles = CsvTableCatalog.FindAll();
            string startPath = !string.IsNullOrEmpty(selectedPath) ? selectedPath : profiles.FirstOrDefault()?.AssetPath;
            if (startPath != null)
                Load(startPath, keepWorkingText: true);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        /// <summary>다른 프로그램(엑셀, MapRegistrySync 등)이 파일을 바꿨으면 편집 내용이 없을 때만 조용히 다시 읽는다.</summary>
        private void OnFocus()
        {
            profiles = CsvTableCatalog.FindAll();
            if (document == null || !File.Exists(document.AssetPath))
                return;
            if (File.GetLastWriteTimeUtc(document.AssetPath) == loadedWriteTime)
                return;

            if (hasUnsavedChanges)
                isChangedOnDisk = true;
            else
                Load(document.AssetPath, keepWorkingText: false);
        }

        private void OnLostFocus()
        {
            CommitCellEdit();
        }

        public override void SaveChanges()
        {
            if (TrySave())
                base.SaveChanges();
        }

        #region 파일 열기·저장

        /// <summary>파일을 바꾸면 이전 파일의 되돌리기 기록이 새 파일에 적용되지 않도록 기록을 비우고 연다.</summary>
        private void SwitchTo(string path)
        {
            if (document != null && document.AssetPath == path)
                return;
            if (!ConfirmLeaveUnsaved())
                return;

            Undo.ClearUndo(this);
            workingText = null;
            Load(path, keepWorkingText: false);
        }

        private void Load(string path, bool keepWorkingText)
        {
            CommitCellEdit();
            profile = profiles.Find(candidate => candidate.AssetPath == path);
            if (profile == null || !File.Exists(path))
            {
                profile = null;
                document = null;
                selectedPath = null;
                return;
            }

            CsvTableDocument diskDocument = CsvTableDocument.Load(path);
            savedText = diskDocument.ToText();
            loadedWriteTime = File.GetLastWriteTimeUtc(path);
            isChangedOnDisk = false;
            bool restoresEdits = keepWorkingText && workingText != null && workingText != savedText && selectedPath == path;
            document = restoresEdits ? CsvTableDocument.Parse(path, workingText, diskDocument.HasBom) : diskDocument;
            selectedPath = path;

            selectedLine = null;
            selectedColumn = 0;
            tableScroll = Vector2.zero;
            collapsedSections.Clear();
            LoadColumnWidths();
            AfterDocumentChanged();
        }

        /// <summary>오류가 있어도 저장은 허용하되, 게임이 해당 줄을 버린다는 것을 알고 저장하도록 한 번 묻는다.</summary>
        private bool TrySave()
        {
            if (document == null)
                return false;
            CommitCellEdit();
            if (!hasUnsavedChanges)
                return true;

            if (errorCount > 0 && !EditorUtility.DisplayDialog("CSV 저장",
                    $"오류가 {errorCount}개 있습니다. 게임은 해당 줄을 무시하거나 코드 기본값을 씁니다.\n그래도 저장할까요?", "저장", "취소"))
                return false;

            string text = document.ToText();
            document.Save(text);
            AssetDatabase.ImportAsset(document.AssetPath);
            savedText = text;
            loadedWriteTime = File.GetLastWriteTimeUtc(document.AssetPath);
            isChangedOnDisk = false;
            hasUnsavedChanges = false;
            ShowNotification(new GUIContent("저장했습니다"), 1f);
            return true;
        }

        private bool ConfirmLeaveUnsaved()
        {
            CommitCellEdit();
            if (!hasUnsavedChanges)
                return true;

            int choice = EditorUtility.DisplayDialogComplex("저장하지 않은 변경",
                $"{profile?.DisplayName}에 저장하지 않은 변경이 있습니다.", "저장", "취소", "저장 안 함");
            return choice switch
            {
                0 => TrySave(),
                2 => true,
                _ => false,
            };
        }

        /// <summary>저장한 상태로 돌아가는 것도 실수일 수 있어 Ctrl+Z로 되돌릴 수 있게 기록한다.</summary>
        private void RevertToSaved()
        {
            if (document == null || !hasUnsavedChanges)
                return;
            if (!EditorUtility.DisplayDialog("되돌리기", "마지막으로 저장한 내용으로 되돌릴까요? (Ctrl+Z로 다시 가져올 수 있습니다)", "되돌리기", "취소"))
                return;

            CancelCellEdit();
            RecordUndo("CSV 되돌리기");
            document = CsvTableDocument.Parse(document.AssetPath, savedText, document.HasBom);
            selectedLine = null;
            AfterDocumentChanged();
        }

        #endregion

        #region 문서 변경 공통 처리

        private void RecordUndo(string actionName)
        {
            detailUndoKey = null;
            Undo.RegisterCompleteObjectUndo(this, actionName);
        }

        /// <summary>어떤 방법으로 바꿨든 저장 여부·검사 결과·보이는 줄이 같은 기준으로 갱신되도록 변경 뒤 처리를 한곳에 모은다.</summary>
        private void AfterDocumentChanged()
        {
            workingText = document.ToText();
            hasUnsavedChanges = workingText != savedText;
            Revalidate();
            RebuildVisibleLines();
            EnsureColumnWidths();
            Repaint();
        }

        private void OnUndoRedo()
        {
            if (document == null || workingText == null || workingText == document.ToText())
                return;

            int selectedIndex = selectedLine == null ? -1 : document.Lines.IndexOf(selectedLine);
            CancelCellEdit();
            GUIUtility.keyboardControl = 0;
            document = CsvTableDocument.Parse(document.AssetPath, workingText, document.HasBom);
            selectedLine = selectedIndex >= 0 && selectedIndex < document.Lines.Count ? document.Lines[selectedIndex] : null;
            AfterDocumentChanged();
        }

        private void Revalidate()
        {
            lineNumbers.Clear();
            for (int i = 0; i < document.Lines.Count; i++)
                lineNumbers[document.Lines[i]] = i + 1;
            rowCount = document.Rows.Count();

            issues = profile.Validate(document);
            issuesByLine.Clear();
            errorCount = warningCount = infoCount = 0;
            foreach (CsvIssue issue in issues)
            {
                switch (issue.Level)
                {
                    case CsvIssueLevel.Error: errorCount++; break;
                    case CsvIssueLevel.Warning: warningCount++; break;
                    default: infoCount++; break;
                }
                if (issue.Line == null)
                    continue;
                if (!issuesByLine.TryGetValue(issue.Line, out List<CsvIssue> lineIssues))
                    issuesByLine[issue.Line] = lineIssues = new List<CsvIssue>();
                lineIssues.Add(issue);
            }
        }

        /// <summary>
        /// 검색·필터 중에는 접힘을 무시하고 맞는 줄과 그 구역 제목만 보여 주어, 찾은 줄이 어느 구역인지 알 수 있게 한다.
        /// </summary>
        private void RebuildVisibleLines()
        {
            visibleLines.Clear();
            sectionRowCounts.Clear();
            int headerIndex = document.HeaderIndex;
            if (headerIndex < 0)
                return;

            bool isFiltering = searchText.Length > 0 || showOnlyProblems || showOnlyEmptyCells;
            CsvLine currentSection = null;
            bool isSectionShown = false;

            for (int i = headerIndex + 1; i < document.Lines.Count; i++)
            {
                CsvLine line = document.Lines[i];
                if (line.Kind == CsvLineKind.Blank)
                    continue;

                if (line.Kind == CsvLineKind.Comment)
                {
                    currentSection = line;
                    sectionRowCounts[line] = 0;
                    isSectionShown = !isFiltering;
                    if (isSectionShown)
                        visibleLines.Add(line);
                    continue;
                }

                if (currentSection != null)
                    sectionRowCounts[currentSection]++;

                if (isFiltering)
                {
                    if (!MatchesFilter(line))
                        continue;
                    if (currentSection != null && !isSectionShown)
                    {
                        visibleLines.Add(currentSection);
                        isSectionShown = true;
                    }
                    visibleLines.Add(line);
                }
                else if (currentSection == null || !collapsedSections.Contains(currentSection.Text))
                {
                    visibleLines.Add(line);
                }
            }
        }

        private bool MatchesFilter(CsvLine line)
        {
            if (showOnlyProblems && !HasProblem(line))
                return false;
            if (showOnlyEmptyCells && !Enumerable.Range(0, document.ColumnCount).Any(column => line.Get(column).Length == 0))
                return false;
            if (searchText.Length > 0 && !line.Values.Any(value => value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                return false;
            return true;
        }

        private bool HasProblem(CsvLine line)
        {
            return issuesByLine.TryGetValue(line, out List<CsvIssue> lineIssues) && lineIssues.Any(issue => issue.Level != CsvIssueLevel.Info);
        }

        #endregion

        #region 편집 동작

        private void ApplyCellEdit(CsvLine line, int column, string value)
        {
            if (line == null || line.Get(column) == value)
                return;
            RecordUndo("CSV 칸 수정");
            line.Set(column, value);
            AfterDocumentChanged();
        }

        /// <summary>상세 칸은 글자마다 바뀌므로, 같은 칸을 이어서 고치는 동안은 되돌리기 기록을 한 번만 남긴다.</summary>
        private void ApplyDetailEdit(CsvLine line, int column, string value)
        {
            string undoKey = $"{lineNumbers[line]}:{column}";
            if (undoKey != detailUndoKey)
            {
                RecordUndo("CSV 칸 수정");
                detailUndoKey = undoKey;
            }
            line.Set(column, value);
            AfterDocumentChanged();
        }

        private void ApplyCommentEdit(CsvLine line, string body)
        {
            string undoKey = $"{lineNumbers[line]}:comment";
            if (undoKey != detailUndoKey)
            {
                RecordUndo("CSV 구역 이름 수정");
                detailUndoKey = undoKey;
            }
            bool wasCollapsed = collapsedSections.Remove(line.Text);
            line.CommentBody = body;
            if (wasCollapsed)
                collapsedSections.Add(line.Text);
            AfterDocumentChanged();
        }

        private void AddRow(CsvLine anchor)
        {
            CommitCellEdit();
            RecordUndo("CSV 줄 추가");
            CsvLine row = document.InsertRowAfter(anchor);
            AfterDocumentChanged();
            EnsureVisible(row);
            Select(row, 0);
            BeginCellEdit();
        }

        private void AddRowAbove(CsvLine line)
        {
            int index = document.Lines.IndexOf(line);
            AddRow(index > 0 ? document.Lines[index - 1] : null);
        }

        private void DuplicateRow(CsvLine line)
        {
            if (line == null || line.Kind != CsvLineKind.Row)
                return;
            CommitCellEdit();
            RecordUndo("CSV 줄 복제");
            CsvLine copy = document.DuplicateRow(line);
            AfterDocumentChanged();
            EnsureVisible(copy);
            Select(copy, selectedColumn);
        }

        private void RemoveLine(CsvLine line)
        {
            CommitCellEdit();
            int visibleIndex = visibleLines.IndexOf(line);
            RecordUndo("CSV 줄 삭제");
            if (!document.Remove(line))
                return;
            AfterDocumentChanged();
            if (visibleLines.Count > 0)
                Select(visibleLines[Mathf.Clamp(visibleIndex, 0, visibleLines.Count - 1)], selectedColumn);
            else
                selectedLine = null;
        }

        private void MoveLine(CsvLine line, int direction)
        {
            CommitCellEdit();
            RecordUndo("CSV 줄 이동");
            if (document.Move(line, direction))
            {
                AfterDocumentChanged();
                EnsureVisible(line);
                Select(line, selectedColumn);
            }
        }

        private void AddSection(string title)
        {
            RecordUndo("CSV 구역 추가");
            CsvLine section = document.AddSection(title, null);
            AfterDocumentChanged();
            EnsureVisible(section);
            Select(section, 0);
        }

        private void AddColumn(int column, string name)
        {
            if (document.IndexOfColumn(name) >= 0)
            {
                ShowNotification(new GUIContent($"'{name}' 열이 이미 있습니다"));
                return;
            }
            RecordUndo("CSV 열 추가");
            document.InsertColumn(column, name);
            AfterDocumentChanged();
            LoadColumnWidths();
        }

        private void RenameColumn(int column, string name)
        {
            if (document.IndexOfColumn(name) >= 0)
            {
                ShowNotification(new GUIContent($"'{name}' 열이 이미 있습니다"));
                return;
            }
            RecordUndo("CSV 열 이름 바꾸기");
            document.Header.Set(column, name);
            AfterDocumentChanged();
            LoadColumnWidths();
        }

        private void RemoveColumn(int column)
        {
            string name = document.ColumnName(column);
            if (!EditorUtility.DisplayDialog("열 삭제", $"'{name}' 열과 그 열의 모든 값을 지울까요? (Ctrl+Z로 되돌릴 수 있습니다)", "삭제", "취소"))
                return;
            RecordUndo("CSV 열 삭제");
            document.RemoveColumn(column);
            selectedColumn = Mathf.Clamp(selectedColumn, 0, Mathf.Max(0, document.ColumnCount - 1));
            AfterDocumentChanged();
            LoadColumnWidths();
        }

        private void ApplyFix(CsvIssue issue)
        {
            RecordUndo("CSV 자동 수정");
            issue.Fix(document);
            AfterDocumentChanged();
        }

        /// <summary>같은 줄 아래에 여러 줄을 넣으면 순서가 뒤집히므로 뒤 항목부터 넣어 코드 순서를 유지한다.</summary>
        private void ApplyAllFixes()
        {
            List<CsvIssue> fixable = issues.Where(issue => issue.Fix != null).ToList();
            if (fixable.Count == 0)
                return;
            RecordUndo("CSV 자동 수정 (모두)");
            for (int i = fixable.Count - 1; i >= 0; i--)
                fixable[i].Fix(document);
            AfterDocumentChanged();
        }

        /// <summary>엑셀에서 여러 칸을 복사해 붙여 넣어도 되도록 탭·줄바꿈으로 나눈 값을 선택한 칸부터 차례로 채운다.</summary>
        private void PasteFromClipboard()
        {
            string text = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text) || selectedLine == null || selectedLine.Kind != CsvLineKind.Row)
                return;

            string[] pastedRows = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            if (pastedRows.Length == 1 && !pastedRows[0].Contains('\t'))
            {
                ApplyCellEdit(selectedLine, selectedColumn, pastedRows[0]);
                return;
            }

            List<CsvLine> visibleRows = visibleLines.Where(line => line.Kind == CsvLineKind.Row).ToList();
            int startRow = visibleRows.IndexOf(selectedLine);
            RecordUndo("CSV 붙여넣기");
            for (int rowOffset = 0; rowOffset < pastedRows.Length && startRow + rowOffset < visibleRows.Count; rowOffset++)
            {
                string[] cells = pastedRows[rowOffset].Split('\t');
                for (int cellOffset = 0; cellOffset < cells.Length; cellOffset++)
                {
                    int column = selectedColumn + cellOffset;
                    if (column < document.ColumnCount)
                        visibleRows[startRow + rowOffset].Set(column, cells[cellOffset]);
                }
            }
            AfterDocumentChanged();
        }

        #endregion

        #region 선택·칸 편집

        /// <summary>상세 칸이 이전 줄의 글을 붙든 채 남지 않도록 선택이 바뀌면 입력 포커스를 푼다.</summary>
        private void Select(CsvLine line, int column)
        {
            if (line != selectedLine || column != selectedColumn)
            {
                GUIUtility.keyboardControl = 0;
                detailUndoKey = null;
            }
            selectedLine = line;
            selectedColumn = Mathf.Clamp(column, 0, Mathf.Max(0, document.ColumnCount - 1));
            scrollTarget = line;
            Repaint();
        }

        private void BeginCellEdit()
        {
            if (selectedLine == null || selectedLine.Kind != CsvLineKind.Row)
                return;
            editingLine = selectedLine;
            editingColumn = selectedColumn;
            editBuffer = CsvLineFormatter.EscapeNewLines(selectedLine.Get(selectedColumn));
            isEditorFocusPending = true;
            scrollTarget = selectedLine;
            Repaint();
        }

        private void CommitCellEdit()
        {
            if (editingLine == null)
                return;
            CsvLine line = editingLine;
            int column = editingColumn;
            string value = CsvLineFormatter.UnescapeNewLines(editBuffer);
            CancelCellEdit();
            if (document != null && document.Lines.Contains(line))
                ApplyCellEdit(line, column, value);
        }

        private void CancelCellEdit()
        {
            editingLine = null;
            editingColumn = -1;
            editBuffer = null;
            isEditorFocusPending = false;
            GUIUtility.keyboardControl = 0;
        }

        private void MoveSelection(int lineDelta, int columnDelta)
        {
            if (visibleLines.Count == 0)
                return;
            int index = selectedLine == null ? 0 : Mathf.Clamp(visibleLines.IndexOf(selectedLine) + lineDelta, 0, visibleLines.Count - 1);
            Select(visibleLines[index], selectedColumn + columnDelta);
        }

        /// <summary>문제 목록에서 고른 줄이 접힌 구역이나 필터에 가려 있으면 가림을 풀어 실제로 보이게 한다.</summary>
        private void EnsureVisible(CsvLine line)
        {
            if (line == null || visibleLines.Contains(line))
                return;

            CsvLine section = FindSection(line);
            if (section != null)
                collapsedSections.Remove(section.Text);
            RebuildVisibleLines();
            if (visibleLines.Contains(line))
                return;

            searchText = string.Empty;
            showOnlyProblems = false;
            showOnlyEmptyCells = false;
            RebuildVisibleLines();
        }

        private CsvLine FindSection(CsvLine line)
        {
            int index = document.Lines.IndexOf(line);
            for (int i = index - 1; i > document.HeaderIndex; i--)
            {
                if (document.Lines[i].Kind == CsvLineKind.Comment)
                    return document.Lines[i];
            }
            return null;
        }

        private void JumpTo(CsvIssue issue)
        {
            if (issue.Line == null)
                return;
            CommitCellEdit();
            EnsureVisible(issue.Line);
            Select(issue.Line, issue.Column >= 0 ? issue.Column : selectedColumn);
        }

        #endregion

        #region 화면 그리기

        private void OnGUI()
        {
            EnsureStyles();
            HandleCommands();
            DrawToolbar();

            if (document == null)
            {
                EditorGUILayout.HelpBox(profiles.Count == 0
                    ? "Assets/_Project/Resources 아래에 CSV 파일이 없습니다."
                    : "위 목록에서 편집할 CSV 파일을 고르세요.", MessageType.Info);
                ShowPendingPrompt();
                return;
            }

            DrawDiskChangeBanner();
            DrawFileNotes();

            Rect contentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Layout)
                lastContentRect = contentRect;

            Rect statusRect = new Rect(lastContentRect.x, lastContentRect.yMax - StatusBarHeight, lastContentRect.width, StatusBarHeight);
            float bottomHeight = showBottomPanel ? Mathf.Min(BottomPanelHeight, lastContentRect.height * 0.5f) : 0f;
            Rect bottomRect = new Rect(lastContentRect.x, statusRect.y - bottomHeight, lastContentRect.width, bottomHeight);
            Rect tableRect = new Rect(lastContentRect.x, lastContentRect.y, lastContentRect.width, Mathf.Max(0f, bottomRect.y - lastContentRect.y));

            // 표는 보이는 줄 수에 따라 컨트롤 개수가 바뀌므로, 아래 패널 입력칸의 포커스가 흔들리지 않게 맨 마지막에 그린다.
            if (showBottomPanel)
                DrawBottomPanel(bottomRect);
            DrawStatusBar(statusRect);
            DrawTable(tableRect);
            ShowPendingPrompt();
        }

        private void EnsureStyles()
        {
            if (cellStyle != null)
                return;
            cellStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, padding = new RectOffset(4, 4, 2, 2) };
            lineNumberStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, padding = new RectOffset(2, 6, 0, 0) };
            columnHeaderStyle = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            issueButtonStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
            pathStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperRight };
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int currentIndex = profiles.IndexOf(profile);
                string[] names = profiles.Select(candidate => candidate.DisplayName).ToArray();
                int newIndex = EditorGUILayout.Popup(currentIndex, names, EditorStyles.toolbarPopup, GUILayout.Width(210f));
                if (newIndex != currentIndex && newIndex >= 0)
                    SwitchTo(profiles[newIndex].AssetPath);

                using (new EditorGUI.DisabledScope(document == null))
                {
                    using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                    {
                        if (GUILayout.Button(new GUIContent("저장", "Ctrl+S"), EditorStyles.toolbarButton))
                            TrySave();
                        if (GUILayout.Button(new GUIContent("되돌리기", "마지막으로 저장한 내용으로"), EditorStyles.toolbarButton))
                            RevertToSaved();
                    }

                    GUILayout.Space(8f);
                    if (GUILayout.Button(new GUIContent("줄 추가", "선택한 줄 아래(없으면 맨 끝)에 추가"), EditorStyles.toolbarButton))
                        AddRow(selectedLine);
                    if (GUILayout.Button(new GUIContent("구역 추가", "# 제목 줄로 묶음을 만듭니다"), EditorStyles.toolbarButton))
                        RequestPrompt(GUILayoutUtility.GetLastRect(), new CsvTextPrompt("새 구역 이름", string.Empty, AddSection));
                    if (profile != null && profile.AllowColumnEdit
                        && GUILayout.Button(new GUIContent("열 추가", "맨 오른쪽에 열 추가 (strings는 새 언어 코드)"), EditorStyles.toolbarButton))
                    {
                        int column = document.ColumnCount;
                        RequestPrompt(GUILayoutUtility.GetLastRect(), new CsvTextPrompt("새 열 이름 (예: ja)", string.Empty, name => AddColumn(column, name)));
                    }
                    if (GUILayout.Button(new GUIContent("파일 위치", "Project 창에서 파일 보기"), EditorStyles.toolbarButton))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(document.AssetPath));
                    if (GUILayout.Button(new GUIContent("모두 접기"), EditorStyles.toolbarButton))
                        SetAllSectionsCollapsed(true);
                    if (GUILayout.Button(new GUIContent("모두 펴기"), EditorStyles.toolbarButton))
                        SetAllSectionsCollapsed(false);

                    GUILayout.FlexibleSpace();

                    bool problemsOnly = GUILayout.Toggle(showOnlyProblems, new GUIContent("문제 있는 줄만", "오류·경고가 있는 줄"), EditorStyles.toolbarButton);
                    bool emptyOnly = GUILayout.Toggle(showOnlyEmptyCells, new GUIContent("빈 칸 있는 줄만", "번역이 빠진 문장 찾기 등"), EditorStyles.toolbarButton);
                    string search = searchField.OnToolbarGUI(searchText, GUILayout.MinWidth(120f), GUILayout.MaxWidth(240f)) ?? string.Empty;
                    if (problemsOnly != showOnlyProblems || emptyOnly != showOnlyEmptyCells || search != searchText)
                    {
                        showOnlyProblems = problemsOnly;
                        showOnlyEmptyCells = emptyOnly;
                        searchText = search;
                        if (document != null)
                            RebuildVisibleLines();
                    }

                    showBottomPanel = GUILayout.Toggle(showBottomPanel, new GUIContent("상세", "선택한 줄 편집과 문제 목록"), EditorStyles.toolbarButton);
                }
            }
        }

        private void SetAllSectionsCollapsed(bool isCollapsed)
        {
            collapsedSections.Clear();
            if (isCollapsed)
            {
                foreach (CsvLine line in document.Lines.Where(line => line.Kind == CsvLineKind.Comment))
                    collapsedSections.Add(line.Text);
            }
            RebuildVisibleLines();
        }

        private void DrawDiskChangeBanner()
        {
            if (!isChangedOnDisk)
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("다른 프로그램이 이 파일을 바꿨습니다. 여기서 저장하면 그 변경을 덮어씁니다.", MessageType.Warning);
                if (GUILayout.Button("파일 다시 읽기\n(내 변경 버림)", GUILayout.Width(110f), GUILayout.Height(38f)))
                {
                    Undo.ClearUndo(this);
                    Load(document.AssetPath, keepWorkingText: false);
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>파일 맨 위 주석은 편집 규칙 설명이라 표 대신 접을 수 있는 안내로 보여 준다.</summary>
        private void DrawFileNotes()
        {
            showFileNotes = EditorGUILayout.Foldout(showFileNotes, "파일 안내", true);
            if (!showFileNotes)
                return;

            EditorGUILayout.HelpBox(profile.Summary, MessageType.Info);
            int headerIndex = document.HeaderIndex;
            List<string> topComments = document.Lines
                .Take(headerIndex < 0 ? document.Lines.Count : headerIndex)
                .Where(line => line.Kind == CsvLineKind.Comment)
                .Select(line => line.CommentBody)
                .ToList();
            if (topComments.Count > 0)
                EditorGUILayout.LabelField(string.Join("\n", topComments), EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawStatusBar(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            string state = hasUnsavedChanges ? "  ·  저장 안 됨" : string.Empty;
            string filter = visibleLines.Count(line => line.Kind == CsvLineKind.Row) != rowCount ? $"  ·  표시 {visibleLines.Count(line => line.Kind == CsvLineKind.Row)}줄" : string.Empty;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width * 0.6f, rect.height - 2f),
                $"데이터 {rowCount}줄{filter}  ·  오류 {errorCount}  ·  경고 {warningCount}  ·  정보 {infoCount}{state}", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + rect.width * 0.6f, rect.y + 2f, rect.width * 0.4f - 6f, rect.height - 2f),
                document.AssetPath, pathStyle);
        }

        #endregion

        #region 표

        private void DrawTable(Rect rect)
        {
            if (rect.height < HeaderHeight + RowHeight)
                return;

            EnsureColumnWidths();
            HandleColumnResize();
            HandleEditingKeys();
            HandleNavigationKeys();

            float totalWidth = LineNumberWidth + columnWidths.Sum() + ActionButtonWidth * ActionButtonCount + 8f;
            Rect bodyRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, rect.height - HeaderHeight);
            ApplyScrollTarget(bodyRect);

            DrawColumnHeader(new Rect(rect.x, rect.y, rect.width, HeaderHeight));

            bool isMouseDownInBody = Event.current.type == EventType.MouseDown && Event.current.button == 0 && bodyRect.Contains(Event.current.mousePosition);
            Rect contentRect = new Rect(0f, 0f, Mathf.Max(totalWidth, bodyRect.width - ScrollBarAllowance), visibleLines.Count * RowHeight);
            Vector2 newScroll = GUI.BeginScrollView(bodyRect, tableScroll, contentRect);
            if (newScroll != tableScroll)
            {
                // 편집 중인 칸이 화면 밖으로 나가면 입력칸이 사라져 글이 사라지므로 먼저 반영한다.
                CommitCellEdit();
                tableScroll = newScroll;
            }

            int firstIndex = Mathf.Max(0, Mathf.FloorToInt(tableScroll.y / RowHeight));
            int lastIndex = Mathf.Min(visibleLines.Count, Mathf.CeilToInt((tableScroll.y + bodyRect.height) / RowHeight) + 1);
            // 칸을 누르는 순간 이전 편집이 반영되어 목록이 다시 만들어질 수 있어 개수를 매번 확인한다.
            for (int i = firstIndex; i < lastIndex && i < visibleLines.Count; i++)
            {
                Rect lineRect = new Rect(0f, i * RowHeight, contentRect.width, RowHeight);
                if (visibleLines[i].Kind == CsvLineKind.Comment)
                    DrawSectionLine(visibleLines[i], lineRect);
                else
                    DrawRowLine(visibleLines[i], lineRect, i);
            }

            if (visibleLines.Count == 0)
                GUI.Label(new Rect(8f, 8f, bodyRect.width - 16f, RowHeight), "표시할 줄이 없습니다. 검색어나 필터를 확인하세요.", EditorStyles.centeredGreyMiniLabel);

            // 빈 곳을 누르면 편집을 끝내 단축키가 다시 표에 오게 한다.
            if (isMouseDownInBody && Event.current.type == EventType.MouseDown)
            {
                CommitCellEdit();
                GUIUtility.keyboardControl = 0;
            }
            GUI.EndScrollView();
        }

        private void DrawColumnHeader(Rect rect)
        {
            Event current = Event.current;
            float x = rect.x - tableScroll.x;
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            GUI.Label(new Rect(x, rect.y, LineNumberWidth, rect.height), "줄", lineNumberStyle);
            x += LineNumberWidth;

            for (int column = 0; column < columnWidths.Length; column++)
            {
                Rect cellRect = new Rect(x, rect.y, columnWidths[column], rect.height);
                Rect handleRect = new Rect(cellRect.xMax - ResizeHandleWidth * 0.5f, rect.y, ResizeHandleWidth, rect.height);
                EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);

                if (current.type == EventType.MouseDown && current.button == 0 && handleRect.Contains(current.mousePosition))
                {
                    if (current.clickCount == 2)
                    {
                        columnWidths[column] = profile.GetDefaultWidth(document.ColumnName(column));
                        SaveColumnWidth(column);
                    }
                    else
                    {
                        resizingColumn = column;
                        resizeStartMouseX = current.mousePosition.x;
                        resizeStartWidth = columnWidths[column];
                    }
                    current.Use();
                }
                else if (current.type == EventType.MouseDown && current.button == 1 && cellRect.Contains(current.mousePosition))
                {
                    ShowColumnMenu(column, cellRect);
                    current.Use();
                }

                string tooltip = profile.AllowColumnEdit ? "오른쪽 클릭: 열 추가·이름 바꾸기·삭제 / 경계 더블클릭: 너비 초기화" : "경계를 끌어 너비 조절, 더블클릭으로 초기화";
                GUI.Label(cellRect, new GUIContent(document.ColumnName(column), tooltip), columnHeaderStyle);
                x += columnWidths[column];
            }
        }

        private void HandleColumnResize()
        {
            if (resizingColumn < 0 || resizingColumn >= columnWidths.Length)
                return;

            Event current = Event.current;
            if (current.type == EventType.MouseDrag)
            {
                columnWidths[resizingColumn] = Mathf.Max(MinColumnWidth, resizeStartWidth + current.mousePosition.x - resizeStartMouseX);
                current.Use();
                Repaint();
            }
            else if (current.rawType == EventType.MouseUp)
            {
                SaveColumnWidth(resizingColumn);
                resizingColumn = -1;
                current.Use();
            }
        }

        private void DrawSectionLine(CsvLine line, Rect rect)
        {
            bool isSelected = line == selectedLine;
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.09f));
                if (isSelected)
                    EditorGUI.DrawRect(rect, SelectedRowColor);
            }

            float actionsX = LineNumberWidth + columnWidths.Sum();
            sectionRowCounts.TryGetValue(line, out int count);
            bool isCollapsed = collapsedSections.Contains(line.Text);
            string label = count > 0 ? $"{line.CommentBody}   ({count}줄)" : line.CommentBody;
            Rect foldRect = new Rect(rect.x + 6f, rect.y, Mathf.Max(120f, actionsX - 10f), rect.height);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                Select(line, selectedColumn);
                ShowLineMenu(line);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && foldRect.Contains(Event.current.mousePosition))
            {
                CommitCellEdit();
                Select(line, selectedColumn);
            }

            bool isExpanded = EditorGUI.Foldout(foldRect, !isCollapsed, label, true, EditorStyles.foldoutHeader);
            if (isExpanded == isCollapsed && searchText.Length == 0 && !showOnlyProblems && !showOnlyEmptyCells)
            {
                if (isExpanded)
                    collapsedSections.Remove(line.Text);
                else
                    collapsedSections.Add(line.Text);
                RebuildVisibleLines();
            }

            DrawLineActions(line, new Rect(actionsX, rect.y, ActionButtonWidth * ActionButtonCount, rect.height));
        }

        private void DrawRowLine(CsvLine line, Rect rect, int visibleIndex)
        {
            Event current = Event.current;
            bool isSelectedLine = line == selectedLine;
            issuesByLine.TryGetValue(line, out List<CsvIssue> lineIssues);

            if (current.type == EventType.Repaint)
            {
                if (visibleIndex % 2 == 1)
                    EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.025f) : new Color(0f, 0f, 0f, 0.03f));
                if (isSelectedLine)
                    EditorGUI.DrawRect(rect, SelectedRowColor);
            }

            Rect numberRect = new Rect(rect.x, rect.y, LineNumberWidth, rect.height);
            CsvIssue rowIssue = FindWorstIssue(lineIssues, -1);
            if (rowIssue != null && current.type == EventType.Repaint)
                EditorGUI.DrawRect(numberRect, ColorOf(rowIssue.Level));
            GUI.Label(numberRect, new GUIContent(lineNumbers[line].ToString(), DescribeIssues(lineIssues, -1)), lineNumberStyle);
            HandleLineMouse(line, numberRect, selectedColumn);

            float x = rect.x + LineNumberWidth;
            for (int column = 0; column < columnWidths.Length; column++)
            {
                Rect cellRect = new Rect(x, rect.y, columnWidths[column], rect.height);
                x += columnWidths[column];

                if (line == editingLine && column == editingColumn)
                {
                    DrawCellEditor(cellRect);
                    continue;
                }

                CsvIssue cellIssue = FindWorstIssue(lineIssues, column);
                if (current.type == EventType.Repaint)
                {
                    if (cellIssue != null)
                        EditorGUI.DrawRect(cellRect, ColorOf(cellIssue.Level));
                    if (isSelectedLine && column == selectedColumn)
                        DrawOutline(cellRect, SelectedCellColor);
                }

                string shown = CsvLineFormatter.EscapeNewLines(line.Get(column));
                GUI.Label(cellRect, new GUIContent(shown, DescribeIssues(lineIssues, column) ?? (shown.Length > 40 ? shown : null)), cellStyle);
                HandleLineMouse(line, cellRect, column);
            }

            DrawLineActions(line, new Rect(x, rect.y, ActionButtonWidth * ActionButtonCount, rect.height));
        }

        /// <summary>엑셀처럼 한 번 누르면 선택, 두 번 누르면 편집, 오른쪽 클릭이면 줄 메뉴가 뜨게 한다.</summary>
        private void HandleLineMouse(CsvLine line, Rect rect, int column)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || !rect.Contains(current.mousePosition))
                return;

            CommitCellEdit();
            GUIUtility.keyboardControl = 0;
            Select(line, column);
            if (current.button == 0 && current.clickCount == 2)
                BeginCellEdit();
            else if (current.button == 1)
                ShowLineMenu(line);
            current.Use();
        }

        private void DrawCellEditor(Rect rect)
        {
            GUI.SetNextControlName(CellEditorControlName);
            editBuffer = EditorGUI.TextField(rect, editBuffer);
            if (isEditorFocusPending)
            {
                EditorGUI.FocusTextInControl(CellEditorControlName);
                isEditorFocusPending = false;
                Repaint();
            }
        }

        /// <summary>줄 순서가 바뀐 채 남은 줄을 마저 그리면 목록이 어긋나므로, 버튼을 누르면 이번 그리기를 끝낸다.</summary>
        private void DrawLineActions(CsvLine line, Rect rect)
        {
            float x = rect.x;
            Action action = null;
            if (GUI.Button(new Rect(x, rect.y + 1f, ActionButtonWidth, rect.height - 2f), new GUIContent("▲", "위로"), EditorStyles.miniButtonLeft))
                action = () => MoveLine(line, -1);
            x += ActionButtonWidth;
            if (GUI.Button(new Rect(x, rect.y + 1f, ActionButtonWidth, rect.height - 2f), new GUIContent("▼", "아래로"), EditorStyles.miniButtonMid))
                action = () => MoveLine(line, 1);
            x += ActionButtonWidth;
            if (GUI.Button(new Rect(x, rect.y + 1f, ActionButtonWidth, rect.height - 2f), new GUIContent("+", "아래에 줄 추가"), EditorStyles.miniButtonMid))
                action = () => AddRow(line);
            x += ActionButtonWidth;
            if (GUI.Button(new Rect(x, rect.y + 1f, ActionButtonWidth, rect.height - 2f), new GUIContent("×", "줄 삭제"), EditorStyles.miniButtonRight))
                action = () => RemoveLine(line);

            if (action == null)
                return;
            action();
            GUIUtility.ExitGUI();
        }

        private void ShowLineMenu(CsvLine line)
        {
            var menu = new GenericMenu();
            bool isRow = line.Kind == CsvLineKind.Row;
            menu.AddItem(new GUIContent("위에 줄 추가"), false, () => AddRowAbove(line));
            menu.AddItem(new GUIContent("아래에 줄 추가"), false, () => AddRow(line));
            if (isRow)
                menu.AddItem(new GUIContent("줄 복제 (Ctrl+D)"), false, () => DuplicateRow(line));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("위로 이동"), false, () => MoveLine(line, -1));
            menu.AddItem(new GUIContent("아래로 이동"), false, () => MoveLine(line, 1));
            menu.AddSeparator(string.Empty);
            if (isRow)
            {
                menu.AddItem(new GUIContent("키 복사"), false, () => EditorGUIUtility.systemCopyBuffer = line.Get(0));
                menu.AddItem(new GUIContent("줄 전체 복사 (탭 구분)"), false, () => EditorGUIUtility.systemCopyBuffer = string.Join("\t", line.Values));
                menu.AddSeparator(string.Empty);
            }
            menu.AddItem(new GUIContent(isRow ? "줄 삭제" : "구역 제목 줄 삭제 (안의 줄은 남음)"), false, () => RemoveLine(line));
            menu.ShowAsContext();
        }

        private void ShowColumnMenu(int column, Rect headerRect)
        {
            var menu = new GenericMenu();
            Rect promptRect = new Rect(headerRect.x, headerRect.y, headerRect.width, headerRect.height);
            if (profile.AllowColumnEdit)
            {
                menu.AddItem(new GUIContent("오른쪽에 열 추가"), false,
                    () => RequestPrompt(promptRect, new CsvTextPrompt("새 열 이름 (예: ja)", string.Empty, name => AddColumn(column + 1, name))));
                if (column > 0)
                {
                    menu.AddItem(new GUIContent("열 이름 바꾸기"), false,
                        () => RequestPrompt(promptRect, new CsvTextPrompt("열 이름", document.ColumnName(column), name => RenameColumn(column, name))));
                    menu.AddItem(new GUIContent("열 삭제"), false, () => RemoveColumn(column));
                }
                menu.AddSeparator(string.Empty);
            }
            menu.AddItem(new GUIContent("열 너비 초기화"), false, () =>
            {
                columnWidths[column] = profile.GetDefaultWidth(document.ColumnName(column));
                SaveColumnWidth(column);
                Repaint();
            });
            menu.ShowAsContext();
        }

        private void ApplyScrollTarget(Rect bodyRect)
        {
            if (scrollTarget == null || Event.current.type == EventType.Layout)
                return;

            int index = visibleLines.IndexOf(scrollTarget);
            scrollTarget = null;
            if (index < 0)
                return;

            float top = index * RowHeight;
            float viewHeight = bodyRect.height - ScrollBarAllowance;
            if (top < tableScroll.y)
                tableScroll.y = top;
            else if (top + RowHeight > tableScroll.y + viewHeight)
                tableScroll.y = top + RowHeight - viewHeight;
        }

        private static CsvIssue FindWorstIssue(List<CsvIssue> lineIssues, int column)
        {
            if (lineIssues == null)
                return null;
            CsvIssue worst = null;
            foreach (CsvIssue issue in lineIssues)
            {
                if (issue.Column == column && (worst == null || issue.Level > worst.Level))
                    worst = issue;
            }
            return worst;
        }

        private static string DescribeIssues(List<CsvIssue> lineIssues, int column)
        {
            if (lineIssues == null)
                return null;
            List<string> messages = lineIssues.Where(issue => issue.Column == column).Select(issue => issue.Message).ToList();
            return messages.Count == 0 ? null : string.Join("\n", messages);
        }

        private static Color ColorOf(CsvIssueLevel level)
        {
            return level switch
            {
                CsvIssueLevel.Error => ErrorColor,
                CsvIssueLevel.Warning => WarningColor,
                _ => InfoColor,
            };
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        #endregion

        #region 키보드·명령

        /// <summary>입력칸의 기본 동작보다 먼저 Enter·Tab·Esc를 가로채야 편집을 끝내고 옆 칸으로 옮길 수 있다.</summary>
        private void HandleEditingKeys()
        {
            Event current = Event.current;
            if (editingLine == null || current.type != EventType.KeyDown)
                return;

            switch (current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CommitCellEdit();
                    MoveSelection(1, 0);
                    current.Use();
                    break;
                case KeyCode.Tab:
                    CommitCellEdit();
                    MoveSelection(0, current.shift ? -1 : 1);
                    current.Use();
                    break;
                case KeyCode.Escape:
                    CancelCellEdit();
                    current.Use();
                    Repaint();
                    break;
            }
        }

        /// <summary>다른 입력칸에 글을 쓰는 중에는 방향키를 빼앗지 않도록 포커스가 비었을 때만 표 이동에 쓴다.</summary>
        private void HandleNavigationKeys()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || editingLine != null || GUIUtility.keyboardControl != 0 || selectedLine == null)
                return;

            switch (current.keyCode)
            {
                case KeyCode.UpArrow: MoveSelection(-1, 0); break;
                case KeyCode.DownArrow: MoveSelection(1, 0); break;
                case KeyCode.LeftArrow: MoveSelection(0, -1); break;
                case KeyCode.RightArrow: MoveSelection(0, 1); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.F2:
                    BeginCellEdit();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (selectedLine.Kind == CsvLineKind.Row)
                        ApplyCellEdit(selectedLine, selectedColumn, string.Empty);
                    break;
                default:
                    return;
            }
            current.Use();
        }

        /// <summary>Ctrl+C·V·D는 Unity가 명령 이벤트로 보내므로, 표에 포커스가 있을 때만 받아 칸 복사·붙여넣기·줄 복제로 쓴다.</summary>
        private void HandleCommands()
        {
            Event current = Event.current;
            if (current.type != EventType.ValidateCommand && current.type != EventType.ExecuteCommand)
                return;
            if (document == null || editingLine != null || GUIUtility.keyboardControl != 0 || selectedLine == null || selectedLine.Kind != CsvLineKind.Row)
                return;

            string command = current.commandName;
            if (command != "Copy" && command != "Paste" && command != "Duplicate" && command != "Delete" && command != "SoftDelete")
                return;

            if (current.type == EventType.ExecuteCommand)
            {
                switch (command)
                {
                    case "Copy":
                        EditorGUIUtility.systemCopyBuffer = selectedLine.Get(selectedColumn);
                        break;
                    case "Paste":
                        PasteFromClipboard();
                        break;
                    case "Duplicate":
                        DuplicateRow(selectedLine);
                        break;
                    default:
                        ApplyCellEdit(selectedLine, selectedColumn, string.Empty);
                        break;
                }
            }
            current.Use();
        }

        #endregion

        #region 아래 패널 (상세 편집·문제 목록)

        private void DrawBottomPanel(Rect rect)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0f, 0f, 0f, 0.4f));
            float detailWidth = rect.width * 0.56f;
            GUILayout.BeginArea(new Rect(rect.x + 4f, rect.y + 4f, detailWidth - 8f, rect.height - 6f));
            DrawDetail(detailWidth - 8f);
            GUILayout.EndArea();

            EditorGUI.DrawRect(new Rect(rect.x + detailWidth, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.3f));
            GUILayout.BeginArea(new Rect(rect.x + detailWidth + 6f, rect.y + 4f, rect.width - detailWidth - 10f, rect.height - 6f));
            DrawIssueList();
            GUILayout.EndArea();
        }

        /// <summary>표의 한 줄 칸으로는 긴 문장·줄바꿈을 보기 어려워, 선택한 줄을 여러 줄 입력칸으로 펼쳐 고치게 한다.</summary>
        private void DrawDetail(float width)
        {
            if (selectedLine == null || !document.Lines.Contains(selectedLine))
            {
                EditorGUILayout.LabelField("선택한 줄", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("줄을 누르면 여기서 긴 문장을 여러 줄로 고칠 수 있습니다.\n" +
                                        "표: 더블클릭/Enter/F2 편집 · Enter 아래로 · Tab 옆으로 · Esc 취소 · Del 지우기\n" +
                                        "Ctrl+C/V 복사·붙여넣기(엑셀 여러 칸 가능) · Ctrl+D 줄 복제 · Ctrl+Z 되돌리기 · Ctrl+S 저장", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField($"선택한 줄 ({lineNumbers[selectedLine]}번째 줄)", EditorStyles.boldLabel);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

            if (selectedLine.Kind == CsvLineKind.Comment)
            {
                string body = EditorGUILayout.TextField("구역 이름 (# 주석)", selectedLine.CommentBody);
                if (body != selectedLine.CommentBody)
                    ApplyCommentEdit(selectedLine, body);
            }
            else
            {
                issuesByLine.TryGetValue(selectedLine, out List<CsvIssue> lineIssues);
                const float labelWidth = 110f;
                float fieldWidth = Mathf.Max(100f, width - labelWidth - 24f);
                for (int column = 0; column < document.ColumnCount; column++)
                {
                    string value = selectedLine.Get(column);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(document.ColumnName(column), GUILayout.Width(labelWidth));
                        float height = Mathf.Clamp(EditorStyles.textArea.CalcHeight(new GUIContent(value), fieldWidth), 18f, 90f);
                        string newValue = EditorGUILayout.TextArea(value, GUILayout.Height(height));
                        if (newValue != value)
                            ApplyDetailEdit(selectedLine, column, newValue);
                    }

                    string message = DescribeIssues(lineIssues, column);
                    if (message != null)
                        EditorGUILayout.HelpBox(message, ToMessageType(FindWorstIssue(lineIssues, column).Level));
                }

                string rowMessage = DescribeIssues(lineIssues, -1);
                if (rowMessage != null)
                    EditorGUILayout.HelpBox(rowMessage, ToMessageType(FindWorstIssue(lineIssues, -1).Level));
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawIssueList()
        {
            List<CsvIssue> fixable = issues.Where(issue => issue.Fix != null).ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"문제 {errorCount + warningCount}개 · 정보 {infoCount}개", EditorStyles.boldLabel);
                showInfoIssues = GUILayout.Toggle(showInfoIssues, "정보 표시", EditorStyles.miniButton, GUILayout.Width(64f));
                if (fixable.Count > 1 && GUILayout.Button($"모두 줄 추가 ({fixable.Count})", EditorStyles.miniButton, GUILayout.Width(110f)))
                {
                    ApplyAllFixes();
                    GUIUtility.ExitGUI();
                }
            }

            IEnumerable<CsvIssue> shown = issues
                .Where(issue => showInfoIssues || issue.Level != CsvIssueLevel.Info)
                .OrderByDescending(issue => issue.Level)
                .ThenBy(issue => issue.Line == null ? 0 : lineNumbers[issue.Line]);

            issueScroll = EditorGUILayout.BeginScrollView(issueScroll);
            bool isEmpty = true;
            foreach (CsvIssue issue in shown)
            {
                isEmpty = false;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(IconOf(issue.Level), GUILayout.Width(18f), GUILayout.Height(18f));
                    string where = issue.Line == null
                        ? "파일"
                        : issue.Column >= 0 ? $"{lineNumbers[issue.Line]}줄 {document.ColumnName(issue.Column)}" : $"{lineNumbers[issue.Line]}줄";
                    if (GUILayout.Button($"[{where}] {issue.Message}", issueButtonStyle))
                        JumpTo(issue);
                    if (issue.Fix != null && GUILayout.Button(issue.FixLabel, EditorStyles.miniButton, GUILayout.Width(60f)))
                    {
                        ApplyFix(issue);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            if (isEmpty)
                EditorGUILayout.LabelField("문제가 없습니다.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private static GUIContent IconOf(CsvIssueLevel level)
        {
            return EditorGUIUtility.IconContent(level switch
            {
                CsvIssueLevel.Error => "console.erroricon.sml",
                CsvIssueLevel.Warning => "console.warnicon.sml",
                _ => "console.infoicon.sml",
            });
        }

        private static MessageType ToMessageType(CsvIssueLevel level)
        {
            return level switch
            {
                CsvIssueLevel.Error => MessageType.Error,
                CsvIssueLevel.Warning => MessageType.Warning,
                _ => MessageType.Info,
            };
        }

        #endregion

        #region 열 너비·입력 창

        /// <summary>사람마다 보기 편한 너비가 달라 파일·열 이름별로 이 PC에만 기억한다.</summary>
        private void LoadColumnWidths()
        {
            int count = document?.ColumnCount ?? 0;
            columnWidths = new float[count];
            for (int column = 0; column < count; column++)
            {
                string name = document.ColumnName(column);
                columnWidths[column] = EditorPrefs.GetFloat(WidthPrefsKey(name), profile.GetDefaultWidth(name));
            }
        }

        private void EnsureColumnWidths()
        {
            if (document != null && columnWidths.Length != document.ColumnCount)
                LoadColumnWidths();
        }

        private void SaveColumnWidth(int column)
        {
            EditorPrefs.SetFloat(WidthPrefsKey(document.ColumnName(column)), columnWidths[column]);
        }

        private string WidthPrefsKey(string columnName) => $"{WidthPrefsPrefix}{document.AssetPath}/{columnName}";

        /// <summary>메뉴 콜백에서는 GUI 문맥이 없어 팝업을 바로 띄울 수 없으므로 다음 OnGUI에서 띄운다.</summary>
        private void RequestPrompt(Rect activatorRect, PopupWindowContent prompt)
        {
            pendingPrompt = prompt;
            pendingPromptRect = activatorRect;
            Repaint();
        }

        private void ShowPendingPrompt()
        {
            if (pendingPrompt == null || Event.current.type != EventType.Repaint)
                return;
            PopupWindowContent prompt = pendingPrompt;
            pendingPrompt = null;
            PopupWindow.Show(pendingPromptRect, prompt);
        }

        #endregion
    }
}
