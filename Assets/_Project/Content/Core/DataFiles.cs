using System;
using System.IO;
using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>빌드 후에도 문장·설정을 고칠 수 있도록 외부 파일 → 내장 원본 → 코드 기본값 순으로 읽는다.</summary>
    public static class DataFiles
    {
        /// <summary>StreamingAssets 아래에 두어야 빌드 후에도 파일로 남아 편집할 수 있다.</summary>
        public const string ExternalFolderName = "GameData";

        /// <summary>빌드 후에도 편집할 수 있게 복사하는 Resources 하위 폴더.</summary>
        public static readonly string[] EditableFolders = { "Localization", "Credits", "Data" };

        /// <summary>파일마다 형식이 달라 해석은 호출자가 맡는다.</summary>
        public delegate bool TextParser<T>(string text, out T result);

        public static string ExternalRoot => Path.Combine(Application.streamingAssetsPath, ExternalFolderName);

        /// <summary>고친 파일이 깨져도 게임이 멈추지 않도록 외부 → 내장 순으로 시도하고 둘 다 실패하면 false를 돌려준다.</summary>
        /// <param name="resourcePath">Resources 기준 경로(확장자 제외).</param>
        /// <param name="extension">외부 파일 확장자(예: ".json").</param>
        /// <param name="parser">텍스트 해석 함수.</param>
        /// <param name="result">해석 결과.</param>
        public static bool TryLoad<T>(string resourcePath, string extension, TextParser<T> parser, out T result)
        {
            string externalPath = GetExternalPath(resourcePath, extension);
            if (TryReadExternal(externalPath, out string externalText))
            {
                if (TryParse(parser, externalText, out result))
                    return true;
                Debug.LogWarning($"[DataFiles] 외부 파일을 해석할 수 없어 내장 원본을 사용합니다: {externalPath}");
            }

            if (TryReadBuiltIn(resourcePath, out string builtInText))
            {
                if (TryParse(parser, builtInText, out result))
                    return true;
                Debug.LogError($"[DataFiles] 내장 원본을 해석할 수 없습니다: Resources/{resourcePath}");
            }

            result = default;
            return false;
        }

        /// <summary>빌드 시 복사하는 에디터 스크립트와 같은 경로를 쓰도록 공개한다.</summary>
        public static string GetExternalPath(string resourcePath, string extension)
        {
            return Path.Combine(ExternalRoot, resourcePath.Replace('/', Path.DirectorySeparatorChar) + extension);
        }

        /// <summary>잘못된 인코딩이나 파일 잠금으로 게임이 멈추지 않도록 깨진 글자가 있거나 읽기에 실패하면 경고만 남기고 넘긴다.</summary>
        private static bool TryReadExternal(string path, out string text)
        {
            text = null;
            if (!File.Exists(path))
                return false;

            try
            {
                text = File.ReadAllText(path, System.Text.Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[DataFiles] 외부 파일을 읽을 수 없습니다: {path} ({exception.Message})");
                return false;
            }

            // ANSI로 저장된 파일은 한글이 대체 문자(U+FFFD)로 바뀌므로 이것으로 깨짐을 알아낸다.
            if (text.IndexOf('�') >= 0)
            {
                Debug.LogWarning($"[DataFiles] UTF-8로 저장되지 않은 파일이라 사용하지 않습니다: {path}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 외부 파일이 없거나 깨졌을 때 기댈 곳이 필요해 빌드에 포함된 원본을 읽는다.
        /// </summary>
        private static bool TryReadBuiltIn(string resourcePath, out string text)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            text = asset != null ? asset.text : null;
            return asset != null;
        }

        /// <summary>
        /// 손상된 파일 하나로 게임 전체가 멈추지 않도록 빈 파일이나 형식 예외도 실패로 바꿔 다음 순서로 넘긴다.
        /// </summary>
        private static bool TryParse<T>(TextParser<T> parser, string text, out T result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                return parser(text, out result);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
            {
                Debug.LogWarning($"[DataFiles] 형식 오류: {exception.Message}");
                return false;
            }
        }
    }
}
