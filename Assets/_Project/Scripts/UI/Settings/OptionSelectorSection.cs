using GuardVsMannequin.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>◀ ▶로 넘기는 개인 설정 한 줄의 공통 동작. 항목마다 값 읽기·쓰기·표시만 정하면 된다.</summary>
    public abstract class OptionSelectorSection : MonoBehaviour, ISettingsSection
    {
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Text valueText;

        /// <summary>목록을 넘기는 순서. 저장 번호와 보이는 순서가 다를 수 있어 항목이 정한다.</summary>
        protected abstract int[] Order { get; }

        /// <summary>
        /// 저장된 값을 보정한 번호로 읽는다.
        /// </summary>
        protected abstract int GetMode(GameOptions options);

        /// <summary>
        /// 번호를 설정에 넣는다. 저장은 공통 코드가 한다.
        /// </summary>
        protected abstract void SetMode(ref GameOptions options, int mode);

        /// <summary>
        /// 화면에 보일 문구.
        /// </summary>
        protected abstract string GetLabel(int mode);

        /// <summary>
        /// 설정 화면이 처음 열릴 때 한 번만 연결해 이벤트가 중복되지 않게 한다.
        /// </summary>
        public void Initialize()
        {
            previousButton.onClick.AddListener(() => Change(-1));
            nextButton.onClick.AddListener(() => Change(1));
        }

        /// <summary>
        /// 다른 곳에서 바뀌었을 수 있어 저장된 값을 다시 표시한다.
        /// </summary>
        public void Refresh()
        {
            valueText.text = GetLabel(GetMode(OptionsStore.Current));
        }

        /// <summary>
        /// 모든 항목의 기본값이 0번이라 그 값으로 되돌린다.
        /// </summary>
        public void ResetToDefaults()
        {
            Apply(0);
        }

        /// <summary>버튼 두 개로 목록을 순환하도록 끝에서 처음으로 돌아간다.</summary>
        private void Change(int direction)
        {
            int[] order = Order;
            int position = System.Array.IndexOf(order, GetMode(OptionsStore.Current));
            if (position < 0)
                position = 0;
            Apply(order[(position + direction + order.Length) % order.Length]);
        }

        /// <summary>바로 게임에 반영되도록 즉시 저장하고 다시 표시한다.</summary>
        private void Apply(int mode)
        {
            GameOptions options = OptionsStore.Current;
            SetMode(ref options, mode);
            OptionsStore.SetOptions(options);
            Refresh();
        }

        /// <summary>순서가 저장 번호와 같은 항목이 쓰도록 0부터 개수까지의 순서를 만든다.</summary>
        protected static int[] Sequential(int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;
            return order;
        }
    }
}
