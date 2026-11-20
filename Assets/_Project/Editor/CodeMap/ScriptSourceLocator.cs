using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;

namespace GuardVsMannequin.Editor.CodeMap
{
    /// <summary>
    /// MonoScript.GetClass()는 파일 이름과 같은 Unity 오브젝트 클래스만 알려 주므로, 일반 클래스·중첩 타입·partial까지 찾으려고 소스 텍스트로 선언 파일을 찾는다.
    /// </summary>
    internal sealed class ScriptSourceLocator
    {
        private readonly Dictionary<Type, List<string>> declaringFilesByType = new Dictionary<Type, List<string>>();
        private List<string> scriptPaths;

        /// <summary>member 선언 줄을 IDE로 연다. 멤버 줄을 못 찾으면 타입 선언으로 연다.</summary>
        public bool TryOpen(CodeMember member)
        {
            List<string> files = FindDeclaringFiles(member.DeclaringType);
            foreach (string file in files)
            {
                int line = SourceLineFinder.FindMember(ReadText(file), member);
                if (line > 0)
                {
                    Open(file, line);
                    return true;
                }
            }

            return TryOpen(member.DeclaringType);
        }

        public bool TryOpen(Type type)
        {
            List<string> files = FindDeclaringFiles(type);
            if (files.Count == 0)
                return false;

            Open(files[0], Math.Max(1, SourceLineFinder.FindType(ReadText(files[0]), type)));
            return true;
        }

        private List<string> FindDeclaringFiles(Type type)
        {
            if (type == null)
                return new List<string>();
            if (declaringFilesByType.TryGetValue(type, out List<string> cached))
                return cached;

            Regex declaration = SourceLineFinder.TypeDeclarationPattern(type);
            string name = CodeTypeNames.StripArity(type.Name);
            List<string> paths = ScriptPaths();

            // 파일 이름이 타입 이름과 같고 partial이 아니면 그 파일 하나로 충분하다(대부분의 경우).
            string sameNamePath = paths.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == name);
            List<string> files;
            if (sameNamePath != null && IsSoleDeclaration(ReadText(sameNamePath), declaration))
                files = new List<string> { sameNamePath };
            else
                files = paths.Where(path => declaration.IsMatch(ReadText(path))).ToList();

            declaringFilesByType[type] = files;
            return files;
        }

        private static bool IsSoleDeclaration(string text, Regex declaration)
        {
            Match match = declaration.Match(text);
            if (!match.Success)
                return false;
            int lineStart = text.LastIndexOf('\n', match.Index) + 1;
            return text.IndexOf("partial", lineStart, match.Index - lineStart, StringComparison.Ordinal) < 0;
        }

        private List<string> ScriptPaths()
        {
            if (scriptPaths != null)
                return scriptPaths;

            scriptPaths = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return scriptPaths;
        }

        private static string ReadText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static void Open(string path, int line)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null)
                AssetDatabase.OpenAsset(script, line);
            else
                InternalEditorUtility.OpenFileAtLineExternal(Path.GetFullPath(path), line);
        }
    }
}
