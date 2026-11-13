using GuardVsMannequin.Editor;
using NUnit.Framework;

namespace GuardVsMannequin.Tests
{
    /// <summary>모드 SDK가 게임 그래픽 설정을 읽을 수 있는지 확인한다.</summary>
    public class ModSdkSettingsTests
    {
        /// <summary>Unity 버전이 바뀌어 설정 이름이 달라지면 값을 놓치므로, 모든 항목을 읽는지 확인한다.</summary>
        [Test]
        public void ReadShaderStripping_FindsEveryProperty()
        {
            const int ExpectedPropertyCount = 13;
            Assert.AreEqual(ExpectedPropertyCount, ModSdkSettings.ReadShaderStripping().Count);
        }

        /// <summary>SDK 프로젝트의 그래픽 API가 게임과 다르면 모드 셰이더가 빠지므로, 적을 Windows 그래픽 API를 하나 이상 읽는지 확인한다.</summary>
        [Test]
        public void ReadGraphicsApis_ReturnsAtLeastOneApi()
        {
            Assert.IsNotEmpty(ModSdkSettings.ReadGraphicsApis());
        }
    }
}
