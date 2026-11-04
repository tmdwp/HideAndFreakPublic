namespace GuardVsMannequin.Mods
{
    /// <summary>
    /// 고른 맵을 플레이하는 데 필요한 모드 하나. 호스트가 만들어 게스트에게 동기화한다.
    /// 네트워크로 받은 값은 호스트가 마음대로 바꿀 수 있으므로, 화면에 보여 주거나 열기 전에 받는 쪽이 검사한다.
    /// </summary>
    public struct RequiredMod
    {
        public string id;
        public string version;

        /// <summary>번들 내용 지문. (같은 파일인지 비교)</summary>
        public string contentHash;

        /// <summary>번들 파일 크기의 합. (복사가 덜 된 파일을 걸러냄)</summary>
        public long bundleSize;

        /// <summary>표시 이름. (설치되지 않은 사람에게 보여 줌)</summary>
        public string name;

        /// <summary>모드를 받을 수 있는 주소. 없으면 빈 문자열.</summary>
        public string url;
    }
}
