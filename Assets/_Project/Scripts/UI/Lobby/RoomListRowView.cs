using UnityEngine;
using UnityEngine.UI;

namespace GuardVsMannequin.UI
{
    /// <summary>이름 길이가 달라도 열이 어긋나지 않도록 공개방 한 줄을 칸별 글자로 나눠 보여 준다.</summary>
    public class RoomListRowView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text mapText;
        [SerializeField] private Text playersText;
        [SerializeField] private Text pingText;
        [SerializeField] private Text tagsText;

        [Tooltip("들어갈 수 없는 방(찬 방·모드 없음)의 글자 색.")]
        [SerializeField] private Color unavailableColor = new(0.6f, 0.6f, 0.6f, 1f);

        /// <summary>다른 사람이 적은 방 이름이 화면을 깨지 않도록 모든 칸의 리치 텍스트를 끄고 값을 채운다.</summary>
        /// <param name="isJoinable">바로 들어갈 수 있는지. 아니면 흐리게 보인다.</param>
        public void SetRoom(string roomName, string mapName, string players, string ping, string tags, bool isJoinable)
        {
            Fill(nameText, roomName, isJoinable);
            Fill(mapText, mapName, isJoinable);
            Fill(playersText, players, isJoinable);
            Fill(pingText, ping, isJoinable);
            Fill(tagsText, tags, isJoinable);
        }

        /// <summary>
        /// 손으로 다듬다 칸을 지웠을 수 있어 비어 있으면 건너뛴다.
        /// </summary>
        private void Fill(Text target, string value, bool isJoinable)
        {
            if (target == null)
                return;
            target.supportRichText = false;
            target.text = value;
            if (!isJoinable)
                target.color = unavailableColor;
        }
    }
}
