using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>마우스 감도와 키 설정을 편집용 복제본에서 바꾸고 저장하는 조작 설정.</summary>
    public class ControlSettingsSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text sensitivityValueText;
        [SerializeField] private RebindRowView[] rebindRows;

        private InputActionAsset editingActions;

        /// <summary>원본 에셋이 바뀌지 않도록 복제본을 편집하고, 감도 범위는 저장 보정 범위와 맞춘다.</summary>
        public void Initialize()
        {
            editingActions = Instantiate(inputActions);
            OptionsStore.ApplyBindings(editingActions);

            // 프리팹에 줄을 나중에 추가해도 맞도록 액션 이름으로 짝짓는다.
            foreach (RebindRowView row in rebindRows)
            {
                if (KeyBindingCatalog.TryFind(row.ActionName, row.PartName, out KeyBindingCatalog.Entry entry))
                    row.Bind(editingActions, entry.labelKey, OnBindingEdited);
                else
                    Debug.LogWarning($"[ControlSettingsSection] 키 목록에 없는 줄입니다: {row.ActionName} {row.PartName}", row);
            }

            sensitivitySlider.minValue = GameOptions.MinSensitivity;
            sensitivitySlider.maxValue = GameOptions.MaxSensitivity;
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        /// <summary>
        /// Instantiate로 만든 에셋은 씬과 함께 지워지지 않으므로 직접 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (editingActions != null)
                Destroy(editingActions);
        }

        /// <summary>다른 화면에서 키가 바뀌었을 수 있어 열 때마다 저장된 키를 복제본에 다시 적용한다.</summary>
        public void Refresh()
        {
            GameOptions options = OptionsStore.Current;
            sensitivitySlider.SetValueWithoutNotify(options.mouseSensitivity);
            sensitivityValueText.text = options.mouseSensitivity.ToString("0.00");

            if (editingActions == null)
                return;
            OptionsStore.ApplyBindings(editingActions);
            foreach (RebindRowView row in rebindRows)
                row.Refresh();
        }

        /// <summary>
        /// 다른 묶음의 설정을 건드리지 않도록 감도와 키만 기본값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            editingActions.RemoveAllBindingOverrides();
            OptionsStore.SaveBindings(editingActions);

            GameOptions options = OptionsStore.Current;
            options.mouseSensitivity = GameOptions.CreateDefault().mouseSensitivity;
            OptionsStore.SetOptions(options);
            Refresh();
        }

        /// <summary>움직이는 즉시 게임에 반영되도록 바로 저장하고 보정된 값을 보여 준다.</summary>
        private void OnSensitivityChanged(float value)
        {
            GameOptions options = OptionsStore.Current;
            options.mouseSensitivity = value;
            OptionsStore.SetOptions(options);
            sensitivityValueText.text = OptionsStore.Current.mouseSensitivity.ToString("0.00");
        }

        /// <summary>
        /// 한 키가 두 동작에 묶이면 하나가 먹히지 않으므로 겹친 동작과 키를 맞바꾸고, 게임 중인 입력에도 적용되도록 바로 저장한다.
        /// </summary>
        private void OnBindingEdited(RebindRowView changedRow, string previousPath)
        {
            if (KeyBindingCatalog.TryFind(changedRow.ActionName, changedRow.PartName, out KeyBindingCatalog.Entry changedEntry)
                && BindingConflictResolver.TrySwap(editingActions, changedEntry, previousPath, out KeyBindingCatalog.Entry swappedEntry))
            {
                RebindRowView swappedRow = System.Array.Find(rebindRows, row =>
                    row.ActionName == swappedEntry.actionName && (string.IsNullOrEmpty(row.PartName) ? null : row.PartName) == swappedEntry.partName);
                if (swappedRow != null)
                    swappedRow.FlashSwapped();
            }
            OptionsStore.SaveBindings(editingActions);
        }
    }
}
