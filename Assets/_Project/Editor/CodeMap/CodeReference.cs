using System;

namespace GuardVsMannequin.Editor.CodeMap
{
    [Flags]
    internal enum ReferenceUsage
    {
        None = 0,
        Call = 1,
        Read = 2,
        Write = 4,
    }

    /// <summary>한 멤버가 다른 멤버를 어떻게(호출·읽기·쓰기), 몇 번 쓰는지.</summary>
    internal sealed class CodeReference
    {
        public CodeReference(CodeMember target)
        {
            Target = target;
        }

        public CodeReference(CodeMember target, ReferenceUsage usage, int count)
        {
            Target = target;
            Usage = usage;
            Count = count;
        }

        public CodeMember Target { get; }
        public ReferenceUsage Usage { get; private set; }
        public int Count { get; private set; }

        public void Add(ReferenceUsage usage)
        {
            Usage |= usage;
            Count++;
        }

        public string UsageLabel
        {
            get
            {
                bool reads = (Usage & ReferenceUsage.Read) != 0;
                bool writes = (Usage & ReferenceUsage.Write) != 0;
                if (reads && writes)
                    return "읽기·쓰기";
                if (writes)
                    return "쓰기";
                if (reads)
                    return "읽기";
                return "호출";
            }
        }
    }
}
