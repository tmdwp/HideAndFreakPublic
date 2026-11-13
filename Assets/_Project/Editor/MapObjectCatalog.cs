using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>같은 물체가 여러 프리팹으로 갈라지지 않도록 어떤 모델을 어떤 맵 물체 프리팹으로 만들고 합칠지 정한다.</summary>
    internal static class MapObjectCatalog
    {
        public const string ObjFolder = "Assets/_Project/Prefabs/Obj";

        /// <summary>손으로 만든 프리팹을 덮어쓰지 않도록 이 도구가 만든 프리팹에 붙이는 라벨.</summary>
        public const string Label = "MapObject";

        private const string ModelRoot = "Assets/_Project/Art/MapKit/Models";

        /// <summary>모델 폴더 → 프리팹 하위 폴더, 이름에서 뗄 접두어. 앞에 있는 접두어부터 맞춰 본다.</summary>
        private static readonly (string modelFolder, string prefabFolder, string prefix)[] Folders =
        {
            ("Prop", "Kit/Prop", "SM_"),
            ("Structure", "Kit/Structure", "SM_"),
            ("Subway", "Subway", "SM_Sub_"),
            ("City", "City", "SM_City_"),
            ("GiantRoom", "GiantRoom/Small", "SM_GR_Small_"),
            ("GiantRoom", "GiantRoom", "SM_GR_"),
            ("Nature", "Nature", "SM_"),
        };

        /// <summary>크기 표시를 이름에서 떼려고 기본 프리팹 이름을 바꾼 모델.</summary>
        private static readonly Dictionary<string, string> Renames = new Dictionary<string, string>
        {
            { "SM_Crate_1m", "Crate" },
            { "SM_Floor_4x4", "Floor" },
            { "SM_Wall_4m", "Wall" },
        };

        /// <summary>크기만 다른 같은 물체를 기본 프리팹과 크기 배율로 대신하는 목록.</summary>
        private static readonly Dictionary<string, (string prefab, Vector3 scale)> Unified = new Dictionary<string, (string, Vector3)>
        {
            { "SM_Crate_06m", (KitProp("Crate"), Vector3.one * 0.6f) },
            { "SM_Floor_2x2", (KitStructure("Floor"), new Vector3(0.5f, 1f, 0.5f)) },
            { "SM_Wall_2m", (KitStructure("Wall"), new Vector3(0.5f, 1f, 1f)) },
            { "SM_Wall_1m", (KitStructure("Wall"), new Vector3(0.25f, 1f, 1f)) },
            { "SM_Locker_Row3", (KitProp("Locker_Row3"), Vector3.one) },
            { "SM_Box_Cardboard_Stack", (KitProp("Box_Cardboard_Stack"), Vector3.one) },
        };

        /// <summary>기본 프리팹을 겹쳐 만든 묶음 프리팹의 부품 배치(바닥 중앙 기준).</summary>
        public static readonly (string path, (string part, Vector3 position, float yaw, Vector3 scale)[] parts)[] Composites =
        {
            (KitProp("Locker_Row3"), new[]
            {
                (KitProp("Locker"), new Vector3(-0.5f, 0f, 0f), 0f, Vector3.one),
                (KitProp("Locker"), new Vector3(0f, 0f, 0f), 0f, Vector3.one),
                (KitProp("Locker"), new Vector3(0.5f, 0f, 0f), 0f, Vector3.one),
            }),
            (KitProp("Box_Cardboard_Stack"), new[]
            {
                (KitProp("Box_Cardboard"), new Vector3(-0.28f, 0f, 0f), -4f, Vector3.one),
                (KitProp("Box_Cardboard"), new Vector3(0.27f, 0f, -0.02f), 6f, Vector3.one),
                (KitProp("Box_Cardboard"), new Vector3(0f, 0.35f, -0.01f), -12f, new Vector3(0.9f, 0.857f, 0.95f)),
            }),
        };

        /// <summary>에셋 팩 프리팹을 감싼 맵 물체 프리팹 목록.</summary>
        public static readonly (string source, string path)[] PackPrefabs =
        {
            ("Assets/Modern Vending Machine/Modern Vending Machine.prefab", PrefabPath("Subway", "VendingMachine")),
        };

        /// <summary>같은 물체가 둘로 갈라지지 않도록 크기만 다르거나 묶음으로 바뀐 옛 모델은 뺀다.</summary>
        public static IEnumerable<(string model, string prefab)> ModelPrefabs()
        {
            foreach (string modelPath in AllModels())
            {
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                if (Unified.ContainsKey(modelName) || !TryMapModel(modelPath, out string prefabPath))
                    continue;
                yield return (modelPath, prefabPath);
            }
        }

        /// <summary>옛 씬을 교체할 때 모양이 유지되도록 크기만 다른 모델은 기본 프리팹 + 배율로 바꾼다.</summary>
        public static bool TryGetReplacement(string modelPath, out string prefab, out Vector3 scale)
        {
            string modelName = Path.GetFileNameWithoutExtension(modelPath);
            if (Unified.TryGetValue(modelName, out (string prefab, Vector3 scale) unified))
            {
                prefab = unified.prefab;
                scale = unified.scale;
                return true;
            }
            scale = Vector3.one;
            return TryMapModel(modelPath, out prefab);
        }

        /// <summary>
        /// 맵 빌더가 이 폴더의 프리팹은 연결을 풀지 않고 두어야 프리팹 수정이 모든 맵에 반영되므로 판단 기준을 한곳에 둔다.
        /// </summary>
        public static bool IsObjectPrefab(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && assetPath.Replace('\\', '/').StartsWith(ObjFolder + "/");

        /// <summary>경로 규칙이 여러 곳에서 어긋나지 않도록 한곳에서 만든다.</summary>
        public static string PrefabPath(string folder, string name) => $"{ObjFolder}/{folder}/{name}.prefab";

        /// <summary>목록 정의를 짧게 쓰려고 둔 키트 소품 경로 도우미.</summary>
        private static string KitProp(string name) => PrefabPath("Kit/Prop", name);

        /// <summary>목록 정의를 짧게 쓰려고 둔 키트 구조물 경로 도우미.</summary>
        private static string KitStructure(string name) => PrefabPath("Kit/Structure", name);

        /// <summary>로그와 diff를 비교하기 쉽도록 경로를 정렬해 매번 같은 순서로 만든다.</summary>
        private static IEnumerable<string> AllModels()
        {
            if (!Directory.Exists(ModelRoot))
                return Enumerable.Empty<string>();
            return Directory.GetFiles(ModelRoot, "*.fbx", SearchOption.AllDirectories)
                .Select(modelPath => modelPath.Replace('\\', '/'))
                .OrderBy(modelPath => modelPath);
        }

        /// <summary>한 폴더에 접두어가 다른 모델이 섞일 수 있어 폴더와 접두어를 함께 맞춘다.</summary>
        private static bool TryMapModel(string modelPath, out string prefab)
        {
            prefab = null;
            string normalizedPath = modelPath.Replace('\\', '/');
            string modelName = Path.GetFileNameWithoutExtension(normalizedPath);
            string modelFolderName = Path.GetFileName(Path.GetDirectoryName(normalizedPath));
            foreach ((string modelFolder, string prefabFolder, string prefix) in Folders)
            {
                if (modelFolderName != modelFolder || !modelName.StartsWith(prefix))
                    continue;
                string prefabName = Renames.TryGetValue(modelName, out string renamedName) ? renamedName : modelName.Substring(prefix.Length);
                prefab = PrefabPath(prefabFolder, prefabName);
                return true;
            }
            return false;
        }
    }
}
