using System;
using GuardVsMannequin.Core;
using GuardVsMannequin.Room;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>모두에게 값이 보이고 호스트만 조정할 수 있는 게임 설정 한 줄.</summary>
    public class SettingRowView : MonoBehaviour
    {
        [SerializeField] private GameSettingId settingId;
        [SerializeField] private Text labelText;
        [SerializeField] private Text valueText;

        [Tooltip("값을 키보드로 입력하는 칸. 비우면 -/+ 버튼으로만 조정한다.")]
        [SerializeField] private InputField valueInput;

        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;

        private Action<GameSettingId, float> onSetValue;

        public GameSettingId SettingId => settingId;
        private GameSettings lastSettings;
        private bool wasFocused;

        /// <summary>
        /// 값 변경은 서버를 거쳐야 하므로 이 줄은 직접 바꾸지 않고 바깥에서 받은 함수로 요청만 보낸다.
        /// </summary>
        /// <param name="onAdjust">항목과 방향을 받는 조정 함수.</param>
        /// <param name="setValue">(항목, 입력한 값)을 받는 직접 지정 함수.</param>
        public void Bind(Action<GameSettingId, int> onAdjust, Action<GameSettingId, float> setValue)
        {
            onSetValue = setValue;
            labelText.text = GameSettingDefinitions.Get(settingId).Label;
            decreaseButton.onClick.AddListener(() => onAdjust(settingId, -1));
            increaseButton.onClick.AddListener(() => onAdjust(settingId, 1));

            if (valueInput != null)
            {
                // 숫자 전용 입력 형식은 코드로 넣은 표시 문자열("30초")의 단위까지 지워 버리므로 일반 형식을 쓰고,
                // 입력을 마칠 때 숫자인지 확인한다.
                valueInput.contentType = InputField.ContentType.Standard;
                valueInput.lineType = InputField.LineType.SingleLine;
                valueInput.characterLimit = 10;
                valueInput.onEndEdit.AddListener(OnEndEdit);
            }
        }

        /// <summary>InputField는 포커스 이벤트가 없어 매 프레임 확인하고, 누른 순간 바로 고칠 수 있게 숫자만 남긴다.</summary>
        private void Update()
        {
            if (valueInput == null)
                return;

            bool isFocused = valueInput.isFocused;
            if (isFocused && !wasFocused)
            {
                valueInput.SetTextWithoutNotify(GameSettingDefinitions.FormatNumber(lastSettings, settingId));
                valueInput.selectionAnchorPosition = 0;
                valueInput.selectionFocusPosition = valueInput.text.Length;
            }
            wasFocused = isFocused;
        }

        /// <summary>언어가 바뀌었을 수 있어 이름도 다시 표시하되, 입력 중에는 치던 내용을 덮지 않는다.</summary>
        public void Refresh(GameSettings settings, bool editable)
        {
            lastSettings = settings;
            labelText.text = GameSettingDefinitions.Get(settingId).Label;
            decreaseButton.interactable = editable;
            increaseButton.interactable = editable;

            string formattedValue = GameSettingDefinitions.Format(settings, settingId);
            if (valueInput == null)
            {
                valueText.text = formattedValue;
                return;
            }

            valueInput.interactable = editable;
            if (!valueInput.isFocused)
                valueInput.SetTextWithoutNotify(formattedValue);
        }

        /// <summary>
        /// 잘못 입력해도 이상한 글자가 남지 않도록, 적용 여부와 상관없이 마지막으로 받은 값으로 표시를 되돌린다.
        /// </summary>
        private void OnEndEdit(string text)
        {
            if (GameSettingDefinitions.TryParseInput(text, out float value))
                onSetValue?.Invoke(settingId, value);

            // 서버가 보정한 값은 다음 갱신 때 표시된다.
            valueInput.SetTextWithoutNotify(GameSettingDefinitions.Format(lastSettings, settingId));
        }
    }
}
