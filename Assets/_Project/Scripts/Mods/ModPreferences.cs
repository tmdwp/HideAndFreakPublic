using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Mods
{
    /// <summary>새로 설치한 모드는 켜진 채 시작하도록 끈 모드 목록만 이 PC에 저장한다.</summary>
    public static class ModPreferences
    {
        private const string DisabledKey = "Mods.Disabled";
        private const char Separator = '\n';

        /// <summary>
        /// 켠 목록 대신 끈 목록을 저장해서 새로 설치한 모드가 따로 설정하지 않아도 켜진 채 시작하게 한다.
        /// </summary>
        public static HashSet<string> LoadDisabled()
        {
            string saved = PlayerPrefs.GetString(DisabledKey, string.Empty);
            var result = new HashSet<string>();
            foreach (string modId in saved.Split(Separator))
            {
                if (!string.IsNullOrWhiteSpace(modId))
                    result.Add(modId.Trim());
            }
            return result;
        }

        /// <summary>
        /// 바뀐 것이 없으면 디스크에 쓰지 않도록 실제로 목록이 바뀌었을 때만 저장한다.
        /// </summary>
        public static void SetEnabled(string modId, bool enabled)
        {
            HashSet<string> disabled = LoadDisabled();
            bool isChanged = enabled ? disabled.Remove(modId) : disabled.Add(modId);
            if (!isChanged)
                return;

            PlayerPrefs.SetString(DisabledKey, string.Join(Separator.ToString(), disabled));
            PlayerPrefs.Save();
        }
    }
}
