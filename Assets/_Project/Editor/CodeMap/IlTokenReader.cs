using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>IL 명령 하나가 가리키는 멤버 토큰.</summary>
    internal readonly struct IlMemberToken
    {
        public IlMemberToken(OpCode opCode, int token)
        {
            OpCode = opCode;
            Token = token;
        }

        public OpCode OpCode { get; }
        public int Token { get; }
    }

    /// <summary>
    /// Mono.Cecil 같은 외부 라이브러리 없이 메소드 본문이 참조하는 필드·메소드를 알아내려고, IL 바이트에서 멤버 토큰만 골라 읽는다.
    /// </summary>
    internal static class IlTokenReader
    {
        private const byte TwoBytePrefix = 0xFE;

        private static readonly OpCode[] singleByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] twoByteOpCodes = new OpCode[0x100];
        private static readonly bool[] isSingleByteKnown = new bool[0x100];
        private static readonly bool[] isTwoByteKnown = new bool[0x100];

        /// <summary>런타임이 아는 모든 OpCode를 값으로 찾을 수 있게 표로 만든다.</summary>
        static IlTokenReader()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(field.GetValue(null) is OpCode opCode))
                    continue;

                ushort value = unchecked((ushort)opCode.Value);
                if (opCode.Size == 1)
                {
                    singleByteOpCodes[value] = opCode;
                    isSingleByteKnown[value] = true;
                }
                else
                {
                    twoByteOpCodes[value & 0xFF] = opCode;
                    isTwoByteKnown[value & 0xFF] = true;
                }
            }
        }

        /// <summary>필드·메소드 토큰을 가진 명령만 순서대로 results에 담는다.</summary>
        public static void ReadMemberTokens(byte[] il, List<IlMemberToken> results)
        {
            int position = 0;
            while (position < il.Length)
            {
                if (!TryReadOpCode(il, ref position, out OpCode opCode))
                    return;

                switch (opCode.OperandType)
                {
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                    case OperandType.InlineTok:
                        if (position + 4 > il.Length)
                            return;
                        results.Add(new IlMemberToken(opCode, BitConverter.ToInt32(il, position)));
                        position += 4;
                        break;
                    case OperandType.InlineSwitch:
                        if (position + 4 > il.Length)
                            return;
                        int targetCount = BitConverter.ToInt32(il, position);
                        position += 4 + targetCount * 4;
                        break;
                    default:
                        position += OperandSize(opCode.OperandType);
                        break;
                }
            }
        }

        /// <summary>모르는 바이트를 만나면 이후 해석이 전부 어긋나므로 그 자리에서 멈춘다.</summary>
        private static bool TryReadOpCode(byte[] il, ref int position, out OpCode opCode)
        {
            byte first = il[position++];
            if (first != TwoBytePrefix)
            {
                opCode = singleByteOpCodes[first];
                return isSingleByteKnown[first];
            }

            if (position >= il.Length)
            {
                opCode = default;
                return false;
            }

            byte second = il[position++];
            opCode = twoByteOpCodes[second];
            return isTwoByteKnown[second];
        }

        /// <summary>토큰이 아닌 피연산자는 건너뛰기만 하면 되므로 크기만 안다.</summary>
        private static int OperandSize(OperandType operandType)
        {
            switch (operandType)
            {
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineI:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                default:
                    return 0;
            }
        }
    }
}
