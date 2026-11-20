using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Compilation;
using ReflectionAssembly = System.Reflection.Assembly;

namespace GuardVsMannequin.Editor.CodeMap
{
    internal enum CodeOrigin
    {
        /// <summary>내 코드 폴더(Assets/_Project) 안의 코드.</summary>
        Own,
        /// <summary>Assets 안의 다른 코드(FishNet 등).</summary>
        Project,
        /// <summary>Unity·.NET·패키지 코드.</summary>
        External,
    }

    /// <summary>
    /// Debug.Log·Vector3 같은 엔진 호출이 그래프를 덮지 않게, 타입이 어느 어셈블리 출신인지 소스 경로로 구분한다.
    /// </summary>
    internal sealed class CodeOriginClassifier
    {
        private const string AssetsFolder = "Assets/";

        private readonly HashSet<string> ownAssemblyNames = new HashSet<string>();
        private readonly HashSet<string> projectAssemblyNames = new HashSet<string>();
        private readonly Dictionary<ReflectionAssembly, CodeOrigin> originByAssembly = new Dictionary<ReflectionAssembly, CodeOrigin>();

        public CodeOriginClassifier(string ownCodeFolder)
        {
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                bool isInAssets = false;
                bool isInOwnFolder = false;
                foreach (string sourceFile in assembly.sourceFiles)
                {
                    string path = sourceFile.Replace('\\', '/');
                    isInAssets |= path.StartsWith(AssetsFolder, StringComparison.Ordinal);
                    if (path.StartsWith(ownCodeFolder, StringComparison.Ordinal))
                    {
                        isInOwnFolder = true;
                        break;
                    }
                }

                if (isInOwnFolder)
                    ownAssemblyNames.Add(assembly.name);
                else if (isInAssets)
                    projectAssemblyNames.Add(assembly.name);
            }
        }

        public CodeOrigin Classify(Type type)
        {
            if (type == null)
                return CodeOrigin.External;

            ReflectionAssembly assembly = type.Assembly;
            if (originByAssembly.TryGetValue(assembly, out CodeOrigin origin))
                return origin;

            string name = assembly.GetName().Name;
            origin = ownAssemblyNames.Contains(name) ? CodeOrigin.Own
                : projectAssemblyNames.Contains(name) ? CodeOrigin.Project
                : CodeOrigin.External;
            originByAssembly[assembly] = origin;
            return origin;
        }

        public CodeOrigin Classify(CodeMember member) => Classify(member.DeclaringType);

        /// <summary>프로젝트 전체에서 사용처를 찾을 때 훑을 어셈블리.</summary>
        public IEnumerable<ReflectionAssembly> LoadedOwnAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(assembly => ownAssemblyNames.Contains(assembly.GetName().Name));
        }
    }
}
