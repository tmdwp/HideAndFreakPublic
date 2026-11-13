using System.IO;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>UI 프리팹을 GUID 그대로 되돌릴 수 있도록 .prefab과 .meta를 함께 보관하는 기준본 저장소.</summary>
    public static class UiPrefabDefaults
    {
        /// <summary>기준본이 에셋으로 중복 등록되지 않도록 Assets 밖에 둔다.</summary>
        public const string Folder = "UiDefaults";

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// 메뉴가 "초기화" 항목을 켤지 정할 수 있도록 기준본이 있는지 알려 준다.
        /// </summary>
        public static bool Exists(string prefabPath)
        {
            return File.Exists(GetDefaultPath(prefabPath));
        }

        /// <summary>
        /// GUID가 같아야 씬의 인스턴스가 끊기지 않으므로 .meta까지 함께 복사한다.
        /// </summary>
        /// <returns>프리팹이 있어 저장했으면 true.</returns>
        public static bool Save(string prefabPath)
        {
            string sourcePath = Path.Combine(ProjectRoot, prefabPath);
            if (!File.Exists(sourcePath))
                return false;

            Directory.CreateDirectory(Path.Combine(ProjectRoot, Folder));
            File.Copy(sourcePath, GetDefaultPath(prefabPath), true);
            File.Copy(sourcePath + ".meta", GetDefaultPath(prefabPath) + ".meta", true);
            return true;
        }

        /// <summary>Unity가 쓰는 GUID와 어긋나지 않도록 .meta가 없을 때만 기준본의 것을 복사한다.</summary>
        /// <returns>기준본이 있어 복원했으면 true.</returns>
        public static bool Restore(string prefabPath)
        {
            string sourcePath = GetDefaultPath(prefabPath);
            if (!File.Exists(sourcePath))
                return false;

            string targetPath = Path.Combine(ProjectRoot, UiPrefabLibrary.EnsureFolder(prefabPath));
            File.Copy(sourcePath, targetPath, true);
            if (!File.Exists(targetPath + ".meta") && File.Exists(sourcePath + ".meta"))
                File.Copy(sourcePath + ".meta", targetPath + ".meta");
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        /// <summary>
        /// 지금 모습을 새 기준으로 삼기 전에 낡은 기준본이 섞이지 않도록 통째로 지운다.
        /// </summary>
        public static void DeleteAll()
        {
            string folderPath = Path.Combine(ProjectRoot, Folder);
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);
        }

        /// <summary>
        /// UI 프리팹은 이름이 겹치지 않으므로 하위 폴더 없이 파일 이름만으로 기준본을 둔다.
        /// </summary>
        private static string GetDefaultPath(string prefabPath)
        {
            return Path.Combine(ProjectRoot, Folder, Path.GetFileName(prefabPath));
        }
    }
}
