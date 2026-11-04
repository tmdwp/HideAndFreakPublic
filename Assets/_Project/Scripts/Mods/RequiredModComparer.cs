using System;
using System.Collections.Generic;
using GuardVsMannequin.Core;

namespace GuardVsMannequin.Mods
{
    /// <summary>필요한 모드를 쓸 수 없는 이유.</summary>
    public enum MissingModReason : byte
    {
        /// <summary>설치되어 있지 않음.</summary>
        NotInstalled,

        /// <summary>설치했지만 버전이 다름.</summary>
        VersionDiffers,

        /// <summary>버전은 같지만 번들 파일 내용이 다름.</summary>
        FilesDiffer,

        /// <summary>설치했지만 오류로 쓸 수 없음. (필요한 모드 없음, Unity 버전 다름 등)</summary>
        Unusable
    }

    /// <summary>
    /// 필요한 모드 중 내 PC에서 쓸 수 없는 모드 하나. 호스트가 보낸 값은 검사를 마친 상태로 들어 있다.
    /// </summary>
    public readonly struct MissingMod
    {
        public readonly string id;

        /// <summary>표시 이름. (리치 텍스트·제어 문자를 지우고 길이를 줄임)</summary>
        public readonly string displayName;

        public readonly string hostVersion;
        public readonly MissingModReason reason;

        /// <summary>내 PC의 모드 정보. 설치되지 않았으면 null.</summary>
        public readonly ModInfo local;

        /// <summary><see cref="ModLinkPolicy"/>를 통과한 다운로드 주소. 없거나 허용되지 않으면 빈 문자열.</summary>
        public readonly string safeUrl;

        /// <summary>항목을 만든다.</summary>
        public MissingMod(string id, string displayName, string hostVersion, MissingModReason reason, ModInfo local, string safeUrl)
        {
            this.id = id;
            this.displayName = displayName;
            this.hostVersion = hostVersion;
            this.reason = reason;
            this.local = local;
            this.safeUrl = safeUrl;
        }
    }

    /// <summary>
    /// 호스트가 알려 준 필요한 모드 목록을 내 PC에 설치된 모드와 비교한다.
    /// 목록은 호스트가 바꿔 보낼 수 있으므로 개수·길이를 제한하고, 표시 문자열과 링크를 여기서 검사한다.
    /// </summary>
    public static class RequiredModComparer
    {
        /// <summary>한 번에 처리하는 필요한 모드의 최대 개수. (터무니없이 긴 목록으로 화면을 채우는 것을 막음)</summary>
        public const int MaxRequiredMods = 32;

        private const int MaxIdLength = 64;
        private const int MaxNameLength = 48;
        private const int MaxVersionLength = 24;

        /// <summary>
        /// 쓸 수 없는 모드를 찾는다. 내가 꺼 둔 모드는 이번 접속 동안 켜지므로 문제로 보지 않는다.
        /// </summary>
        /// <param name="required">호스트가 보낸 필요한 모드 목록.</param>
        /// <param name="findLocal">ID로 내 PC의 모드를 찾는 함수. 없으면 null.</param>
        /// <param name="getBundleSize">내 PC의 모드 번들 크기를 구하는 함수.</param>
        public static List<MissingMod> FindMissing(IReadOnlyList<RequiredMod> required, Func<string, ModInfo> findLocal, Func<ModInfo, long> getBundleSize)
        {
            var missing = new List<MissingMod>();
            if (required == null)
                return missing;

            int count = Math.Min(required.Count, MaxRequiredMods);
            for (int i = 0; i < count; i++)
            {
                RequiredMod entry = required[i];
                ModInfo local = findLocal(entry.id);
                if (TryGetProblem(entry, local, getBundleSize, out MissingModReason reason))
                    missing.Add(CreateMissing(entry, reason, local));
            }
            return missing;
        }

        /// <summary>
        /// 모드 하나를 쓸 수 없는 이유를 찾는다. 쓸 수 있으면 false.
        /// </summary>
        private static bool TryGetProblem(RequiredMod entry, ModInfo local, Func<ModInfo, long> getBundleSize, out MissingModReason reason)
        {
            reason = MissingModReason.NotInstalled;
            if (local == null || local.Manifest == null)
                return true;

            if (local.Manifest.version != entry.version)
                reason = MissingModReason.VersionDiffers;
            else if ((local.Manifest.contentHash ?? string.Empty) != (entry.contentHash ?? string.Empty) || getBundleSize(local) != entry.bundleSize)
                reason = MissingModReason.FilesDiffer;
            else if (local.Status != ModStatus.Active)
                reason = MissingModReason.Unusable;
            else
                return false;
            return true;
        }

        /// <summary>
        /// 호스트가 보낸 문자열을 검사해 화면에 보여 줄 항목을 만든다. (설치되어 있으면 내 PC의 이름을 쓴다)
        /// </summary>
        private static MissingMod CreateMissing(RequiredMod entry, MissingModReason reason, ModInfo local)
        {
            string id = DisplayTextSanitizer.Clean(entry.id, MaxIdLength);
            string name = local != null && local.Manifest != null
                ? DisplayTextSanitizer.Clean(local.DisplayName, MaxNameLength)
                : DisplayTextSanitizer.Clean(entry.name, MaxNameLength);
            ModLinkPolicy.TryGetSafeUrl(entry.url, out string safeUrl);
            return new MissingMod(id, name.Length > 0 ? name : id, DisplayTextSanitizer.Clean(entry.version, MaxVersionLength), reason, local, safeUrl);
        }
    }
}
