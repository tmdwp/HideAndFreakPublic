using GuardVsMannequin.Mods;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 모드 번들의 Unity 버전 허용 범위(같은 라인의 같거나 옛 패치)를 확인한다.
    /// </summary>
    public class UnityVersionRulesTests
    {
        private const string GameUnity = "6000.4.2f1";

        /// <summary>같은 버전과 같은 라인의 옛 패치는 허용되는지 확인한다.</summary>
        [TestCase("6000.4.2f1")]
        [TestCase("6000.4.0f1")]
        [TestCase("6000.4.2b3")]
        public void IsCompatible_SameLineOlderOrEqual_Allowed(string modUnity)
        {
            Assert.IsTrue(UnityVersionRules.IsCompatible(modUnity, GameUnity, out string problem));
            Assert.IsNull(problem);
        }

        /// <summary>게임보다 새 패치나 다른 라인은 번들을 읽지 못할 수 있어 이유와 함께 거부되는지 확인한다.</summary>
        [TestCase("6000.4.3f1")]
        [TestCase("6000.4.2f2")]
        [TestCase("6000.3.9f1")]
        [TestCase("6001.4.2f1")]
        public void IsCompatible_NewerOrOtherLine_Rejected(string modUnity)
        {
            Assert.IsFalse(UnityVersionRules.IsCompatible(modUnity, GameUnity, out string problem));
            Assert.IsNotEmpty(problem);
        }

        /// <summary>문자열 비교로는 10이 2보다 작게 나오므로 숫자로 비교하는지 확인한다.</summary>
        [Test]
        public void IsCompatible_ComparesPatchNumerically()
        {
            Assert.IsFalse(UnityVersionRules.IsCompatible("6000.4.10f1", GameUnity, out _));
            Assert.IsTrue(UnityVersionRules.IsCompatible("6000.4.2f1", "6000.4.10f1", out _));
        }

        /// <summary>모드 버전을 읽을 수 없으면 막고, 게임 버전을 읽을 수 없으면 확인을 건너뛰는지 확인한다.</summary>
        [Test]
        public void IsCompatible_UnparsableVersions()
        {
            Assert.IsFalse(UnityVersionRules.IsCompatible("unknown", GameUnity, out string problem));
            Assert.IsNotEmpty(problem);
            Assert.IsTrue(UnityVersionRules.IsCompatible("6000.4.2f1", null, out _));
        }

        /// <summary>모드 제작자 안내 문구가 라인 시작부터 게임 버전까지인지 확인한다.</summary>
        [Test]
        public void DescribeRange_FromLineStartToGame()
        {
            Assert.AreEqual("6000.4.0 ~ 6000.4.2f1", UnityVersionRules.DescribeRange(GameUnity));
        }
    }
}
