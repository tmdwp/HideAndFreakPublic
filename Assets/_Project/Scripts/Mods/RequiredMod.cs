namespace GuardVsMannequin.Mods
{
    /// <summary>호스트가 바꿔 보낼 수 있어 받는 쪽이 검사해야 하는 맵에 필요한 모드 하나.</summary>
    public struct RequiredMod
    {
        public string id;
        public string version;

        /// <summary>같은 파일인지 비교하는 번들 내용 지문.</summary>
        public string contentHash;

        /// <summary>복사가 덜 된 파일을 거르는 번들 크기 합.</summary>
        public long bundleSize;

        /// <summary>설치하지 않은 사람에게 보여 줄 표시 이름.</summary>
        public string name;

        /// <summary>모드를 받을 수 있는 주소.</summary>
        public string url;
    }
}
