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

        /// <summary>설치했지만 오류로 쓸 수 없는 상태.</summary>
        Unusable
    }

    /// <summary>필요한 모드 중 내 PC에서 쓸 수 없는 모드 하나.</summary>
    public readonly struct MissingMod
    {
        public readonly string id;

        /// <summary>리치 텍스트·제어 문자를 지우고 길이를 줄인 값이라 그대로 화면에 써도 된다.</summary>
        public readonly string displayName;

        public readonly string hostVersion;
        public readonly MissingModReason reason;

        /// <summary>버전이 다를 때 보여 줄 내 버전.</summary>
        public readonly ModInfo local;

        /// <summary>허용 정책을 통과한 다운로드 주소.</summary>
        public readonly string safeUrl;

        /// <summary>검사를 마친 값만 담기도록 만드는 곳을 비교기 하나로 모은다.</summary>
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

    /// <summary>호스트가 바꿔 보낼 수 있으므로 개수·길이를 제한하며 필요한 모드 목록을 내 모드와 비교한다.</summary>
    public static class RequiredModComparer
    {
        /// <summary>터무니없이 긴 목록으로 화면을 채우는 것을 막는 개수 상한.</summary>
        public const int MaxRequiredMods = 32;

        private const int MaxIdLength = 64;
        private const int MaxNameLength = 48;
        private const int MaxVersionLength = 24;

        /// <summary>꺼 둔 모드는 이번 접속 동안 켜지므로 실제로 쓸 수 없는 모드만 찾는다.</summary>
        /// <param name="findLocal">서비스 없이 테스트하도록 받는 내 PC 모드 찾기 함수.</param>
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
                ModInfo localMod = findLocal(entry.id);
                if (TryGetProblem(entry, localMod, getBundleSize, out MissingModReason reason))
                    missing.Add(CreateMissing(entry, reason, localMod));
            }
            return missing;
        }

        /// <summary>버전이 같아도 파일이 다르면 맵이 다르게 열리므로 지문과 크기까지 비교한다.</summary>
        private static bool TryGetProblem(RequiredMod entry, ModInfo localMod, Func<ModInfo, long> getBundleSize, out MissingModReason reason)
        {
            reason = MissingModReason.NotInstalled;
            if (localMod == null || localMod.Manifest == null)
                return true;

            if (localMod.Manifest.version != entry.version)
                reason = MissingModReason.VersionDiffers;
            else if ((localMod.Manifest.contentHash ?? string.Empty) != (entry.contentHash ?? string.Empty) || getBundleSize(localMod) != entry.bundleSize)
                reason = MissingModReason.FilesDiffer;
            else if (localMod.Status != ModStatus.Active)
                reason = MissingModReason.Unusable;
            else
                return false;
            return true;
        }

        /// <summary>호스트 문자열은 조작될 수 있어 정리하고, 설치돼 있으면 내 PC의 이름을 쓴다.</summary>
        private static MissingMod CreateMissing(RequiredMod entry, MissingModReason reason, ModInfo localMod)
        {
            string id = DisplayTextSanitizer.Clean(entry.id, MaxIdLength);
            string displayName = localMod != null && localMod.Manifest != null
                ? DisplayTextSanitizer.Clean(localMod.DisplayName, MaxNameLength)
                : DisplayTextSanitizer.Clean(entry.name, MaxNameLength);
            ModLinkPolicy.TryGetSafeUrl(entry.url, out string safeUrl);
            return new MissingMod(id, displayName.Length > 0 ? displayName : id, DisplayTextSanitizer.Clean(entry.version, MaxVersionLength), reason, localMod, safeUrl);
        }
    }
}
