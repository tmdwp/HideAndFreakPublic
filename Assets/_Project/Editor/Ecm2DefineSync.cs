using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// ECM2(캐릭터 이동 라이브러리)가 프로젝트에 있는지에 맞춰 GVM_ECM2 기호를 켜고 끈다.
    /// 이동 어셈블리(GuardVsMannequin.Movement.ECM2, .Editor)는 이 기호가 있을 때만 컴파일된다.
    /// - ECM2를 설치했는데 기호가 없으면: 켠다. (이동 어셈블리가 컴파일되어 아바타가 움직임)
    /// - ECM2가 없는데 기호가 있으면: 끈다. 단, 이때는 이동 어셈블리 컴파일 오류로 이 코드가 실행되지 못할 수 있으므로
    ///   ECM2 없이 받은 프로젝트(public 브랜치)는 처음부터 기호가 없는 설정으로 둔다.
    /// </summary>
    [InitializeOnLoad]
    public static class Ecm2DefineSync
    {
        /// <summary>이동 어셈블리를 켜는 기호.</summary>
        public const string Define = "GVM_ECM2";

        private const string Ecm2AssemblyName = "ECM2";

        /// <summary>
        /// 에디터가 스크립트를 불러온 뒤 확인한다.
        /// </summary>
        static Ecm2DefineSync()
        {
            EditorApplication.delayCall += Sync;
        }

        /// <summary>
        /// ECM2 어셈블리가 있는지 보고 Standalone 빌드 대상의 기호를 맞춘다.
        /// </summary>
        private static void Sync()
        {
            bool hasEcm2 = CompilationPipeline.GetAssemblies().Any(assembly => assembly.name == Ecm2AssemblyName);
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            string[] defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0).ToArray();
            bool hasDefine = defines.Contains(Define);
            if (hasEcm2 == hasDefine)
                return;

            string[] updated = hasEcm2 ? defines.Append(Define).ToArray() : defines.Where(d => d != Define).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", updated));
            Debug.Log(hasEcm2
                ? $"[Ecm2DefineSync] ECM2가 있어 {Define} 기호를 켰습니다. 이동 어셈블리가 컴파일됩니다."
                : $"[Ecm2DefineSync] ECM2가 없어 {Define} 기호를 껐습니다. 아바타 이동은 동작하지 않습니다.");
        }
    }
}
