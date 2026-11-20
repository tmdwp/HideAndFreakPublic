namespace GuardVsMannequin.Editor.CodeMap
{
    internal enum CodeMapAction
    {
        Focus,
        OpenSource,
    }

    /// <summary>목록·그래프 화면이 창에 알리는 사용자 요청. 화면은 요청만 만들고 처리는 창이 맡는다.</summary>
    internal readonly struct CodeMapInteraction
    {
        public CodeMapInteraction(CodeMember member, CodeMapAction action)
        {
            Member = member;
            Action = action;
        }

        public CodeMember Member { get; }
        public CodeMapAction Action { get; }
    }
}
