using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>빌드 후에도 고칠 수 있도록 방을 만들 때의 초기 게임 설정을 JSON에서 읽는다.</summary>
    public static class DefaultGameSettings
    {
        private const string FilePath = "Data/default_game_settings";
        private const string Extension = ".json";

        private static GameSettings? cached;

        /// <summary>
        /// 자주 조회하는 곳에서 파일을 매번 읽지 않도록 처음 한 번만 읽어 둔다.
        /// </summary>
        public static GameSettings Cached => cached ??= Load();

        /// <summary>
        /// 데이터 파일이 깨져도 방을 만들 수 있어야 하므로, 둘 다 못 쓰면 코드 기본값으로 물러난다.
        /// </summary>
        public static GameSettings Load()
        {
            if (DataFiles.TryLoad<GameSettings>(FilePath, Extension, TryParse, out GameSettings settings))
                return settings;

            Debug.LogWarning($"[DefaultGameSettings] {FilePath}{Extension}을 사용할 수 없어 코드 기본값을 사용합니다.");
            return GameSettings.CreateDefault();
        }

        /// <summary>새 항목이 추가돼도 기존 파일이 깨지지 않도록 코드 기본값 위에 JSON에 적힌 항목만 덮어쓴다.</summary>
        private static bool TryParse(string json, out GameSettings settings)
        {
            // 구조체는 박싱한 복사본을 덮어쓴 뒤 다시 꺼내야 값이 반영된다.
            object boxed = GameSettings.CreateDefault();
            JsonUtility.FromJsonOverwrite(json, boxed);
            settings = (GameSettings)boxed;
            return true;
        }
    }
}
