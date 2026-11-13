using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>이동 어셈블리가 ECM2 유무에 맞춰 컴파일되도록 GVM_ECM2 기호를 켜고 끈다.</summary>
    [InitializeOnLoad]
    public static class Ecm2DefineSync
    {
        public const string Define = "GVM_ECM2";

        private const string Ecm2AssemblyName = "ECM2";

        /// <summary>
        /// 정적 생성자 안에서는 어셈블리 목록이 아직 준비되지 않았을 수 있어 다음 에디터 업데이트로 미룬다.
        /// </summary>
        static Ecm2DefineSync()
        {
            EditorApplication.delayCall += Sync;
        }

        /// <summary>기호를 바꾸면 전체 재컴파일이 일어나므로 이미 맞으면 건드리지 않는다.</summary>
        private static void Sync()
        {
            bool hasEcm2 = CompilationPipeline.GetAssemblies().Any(assembly => assembly.name == Ecm2AssemblyName);
            NamedBuildTarget buildTarget = NamedBuildTarget.Standalone;
            string[] defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget).Split(';').Where(define => define.Length > 0).ToArray();
            bool hasDefine = defines.Contains(Define);
            if (hasEcm2 == hasDefine)
                return;

            string[] updatedDefines = hasEcm2 ? defines.Append(Define).ToArray() : defines.Where(define => define != Define).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(";", updatedDefines));
            Debug.Log(hasEcm2
                ? $"[Ecm2DefineSync] ECM2가 있어 {Define} 기호를 켰습니다. 이동 어셈블리가 컴파일됩니다."
                : $"[Ecm2DefineSync] ECM2가 없어 {Define} 기호를 껐습니다. 아바타 이동은 동작하지 않습니다.");
        }
    }
}
