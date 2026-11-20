using System.Collections.Generic;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 연구소를 다시 만들지 않고 시험해 볼 수 있도록, 이미 있는 연구소 씬에 Quaternius Sci-Fi 팩 소품을 따로 묶어 놓는다.
    /// </summary>
    public static class LabSciFiDecorator
    {
        private static string LabScenePath => $"{MapCatalog.CustomMapFolder}/Map_Laboratory.unity";
        private const string PackFolder = "Assets/ThirdParty/Quaternius/SciFiMegaKit";
        private const string GroupName = "SciFiProps";
        private const string UndoName = "연구소 Sci-Fi 소품";

        // 스폰·전원 장치가 시작부터 막히지 않도록 비워 둘 거리(m).
        private const float MarkerClearance = 1.5f;

        /// <summary>
        /// 같은 자리에 다시 놓을 수 있도록 원하는 위치를 고정해 두고, 실제 위치는 빈자리 찾기가 근처로 옮긴다.
        /// </summary>
        private readonly struct Placement
        {
            public readonly string model;
            public readonly Vector3 position;
            public readonly float yaw;
            public readonly bool transformable;
            public readonly float fitHeight;

            public Placement(string model, float x, float y, float z, float yaw, bool transformable = true, float fitHeight = 0f)
            {
                this.model = model;
                position = new Vector3(x, y, z);
                this.yaw = yaw;
                this.transformable = transformable;
                this.fitHeight = fitHeight;
            }
        }

        // 중앙 실험실(가운데 ±8m, 천장 6m, 북쪽 2m 발코니)과 동쪽 서버실.
        private static readonly Placement[] Placements =
        {
            new Placement("Props/Prop_Computer", 6.8f, 0f, -2.5f, 270f),
            new Placement("Props/Prop_AccessPoint", 7.4f, 0f, -4.6f, 270f),
            new Placement("Props/Prop_Chest", -7f, 0f, 0.8f, 90f),
            new Placement("Props/Prop_Crate3", -2.2f, 0f, -7.2f, 0f),
            new Placement("Props/Prop_Crate4", -3.6f, 0f, -7.2f, 15f),
            new Placement("Props/Prop_Barrel_Large", 6.9f, 0f, -7.1f, 0f),
            new Placement("Props/Prop_Barrel_Large", 5.8f, 0f, -7.4f, 40f),
            new Placement("Props/Prop_Light_Floor", -1.8f, 0f, -2.6f, 0f),
            new Placement("Props/Prop_ItemHolder", 1.8f, 0f, -2.6f, 180f),
            new Placement("Props/Prop_Computer", -6.8f, 2f, 7.4f, 180f),
            new Placement("Columns/Column_Pipes", -7.4f, 0f, -7.4f, 0f, transformable: false, fitHeight: 6f),
            new Placement("Columns/Column_Pipes", -7.4f, 0f, 3.2f, 0f, transformable: false, fitHeight: 6f),
            new Placement("Props/Prop_Computer", 22.8f, 0f, 4.5f, 270f),
            new Placement("Props/Prop_Computer", 22.8f, 0f, -4.5f, 270f),
            new Placement("Props/Prop_Crate4", 21.5f, 0f, 0.5f, 0f),
        };

        /// <summary>메뉴 하나로 끝나도록 연구소 씬을 직접 열어 놓고 저장까지 한다.</summary>
        [MenuItem(EditorMenus.Root + "맵 생성/연구소 Sci-Fi 소품 배치 (Map_Laboratory)")]
        private static void PlaceInLab()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != LabScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                scene = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
            }

            GameObject environment = MapOptimizer.FindByPath(scene, MapOptimizer.EnvironmentPath);
            if (environment == null)
            {
                EditorUtility.DisplayDialog(UndoName, $"'{MapOptimizer.EnvironmentPath}'를 찾지 못했습니다.", "확인");
                return;
            }

            var placer = new MapPiecePlacer(SciFiMaterials.LoadOrCreate(PackFolder));
            Transform group = RecreateGroup(environment.transform);
            List<Vector3> markers = CollectMarkers(scene);
            int propCount = 0, decorCount = 0, skippedCount = 0;
            foreach (Placement placement in Placements)
            {
                GameObject placed = Place(placer, group, placement, markers);
                if (placed == null)
                {
                    skippedCount++;
                    continue;
                }
                if (placement.transformable && CanDisguiseAs(placed) && PropRegistrar.Register(placed, absorbChildren: true))
                    propCount++;
                else
                {
                    MapSceneBuilder.AddMissingColliders(placed.transform);
                    decorCount++;
                }
                // 다음 소품의 빈자리 찾기가 방금 놓은 소품을 피하도록 충돌체 위치를 바로 반영한다.
                Physics.SyncTransforms();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            string summary = $"변신 사물 {propCount}개, 장식 {decorCount}개를 놓았습니다. 건너뜀 {skippedCount}개.";
            Debug.Log($"[LabSciFiDecorator] {summary} ({scene.path})");
            EditorUtility.DisplayDialog(UndoName, $"{summary}\n위치가 마음에 들지 않으면 '{GroupName}' 묶음 안에서 옮기거나 지운 뒤 저장하세요.\n다시 실행하면 묶음을 지우고 새로 놓습니다.", "확인");
        }

        /// <summary>다시 실행할 때 소품이 두 벌이 되지 않도록 이전 묶음을 지운다.</summary>
        private static Transform RecreateGroup(Transform environment)
        {
            Transform oldGroup = environment.Find(GroupName);
            if (oldGroup != null)
                Undo.DestroyObjectImmediate(oldGroup.gameObject);

            var group = new GameObject(GroupName);
            group.transform.SetParent(environment, false);
            Undo.RegisterCreatedObjectUndo(group, UndoName);
            return group.transform;
        }

        /// <summary>기존 연구소 소품과 겹치지 않도록 크기만큼 빈자리를 찾아 옮기고, 스폰·전원 장치 옆이면 놓지 않는다.</summary>
        private static GameObject Place(MapPiecePlacer placer, Transform group, Placement placement, List<Vector3> markers)
        {
            string assetPath = $"{PackFolder}/Models/{placement.model}.fbx";
            GameObject placed = placer.Place(assetPath, group, placement.position, placement.yaw, placement.fitHeight, remap: true);
            if (placed == null)
                return null;
            Undo.RegisterCreatedObjectUndo(placed, UndoName);

            // 바닥까지 닿는 기둥은 구석에 붙여 세우는 장식이라 빈자리를 찾지 않는다.
            if (placement.transformable)
            {
                Bounds bounds = MapPiecePlacer.GetBounds(placed);
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                Vector3 freeSpot = MapPiecePlacer.FindFreeSpot(placement.position, radius, 0f, placed.name);
                placed.transform.position += freeSpot - placement.position;
            }

            if (IsNearMarker(placed.transform.position, markers))
            {
                Debug.LogWarning($"[LabSciFiDecorator] {placed.name}: 스폰·전원 장치 위치와 가까워 놓지 않습니다.");
                Undo.DestroyObjectImmediate(placed);
                return null;
            }
            return placed;
        }

        /// <summary>너무 큰 물체는 변신할 수 없어 장식으로만 둔다.</summary>
        private static bool CanDisguiseAs(GameObject placed)
        {
            Vector3 size = MapPiecePlacer.GetBounds(placed).size;
            return Mathf.Max(size.x, size.y, size.z) <= GameTuning.DisguiseMaxSize;
        }

        /// <summary>
        /// 스폰·전원 장치 근처에 소품이 있으면 시작부터 길을 막거나 장치를 가리므로 그 위치를 모은다.
        /// </summary>
        private static List<Vector3> CollectMarkers(Scene scene)
        {
            var markerPositions = new List<Vector3>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (SpawnPoint spawnPoint in rootObject.GetComponentsInChildren<SpawnPoint>(true))
                    markerPositions.Add(spawnPoint.transform.position);
                foreach (GeneratorSpawnPoint generatorPoint in rootObject.GetComponentsInChildren<GeneratorSpawnPoint>(true))
                    markerPositions.Add(generatorPoint.transform.position);
            }
            return markerPositions;
        }

        /// <summary>위층 표식과 겹친 것으로 보지 않도록 높이를 뺀 수평 거리로 잰다.</summary>
        private static bool IsNearMarker(Vector3 position, List<Vector3> markers)
        {
            foreach (Vector3 marker in markers)
            {
                if (Mathf.Abs(marker.y - position.y) < 1.5f
                    && new Vector2(marker.x - position.x, marker.z - position.z).sqrMagnitude < MarkerClearance * MarkerClearance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 팩의 FBX 재질은 내장(읽기 전용)이고 ORM 텍스처가 URP Lit 형식과 달라서, 이름이 같은 URP 재질을 따로 만들어 바꿔 끼운다.
        /// </summary>
        private static class SciFiMaterials
        {
            private enum Kind { Textured, Cutout, Plain, Emissive, Glass }

            // 재질 이름 → (베이스 텍스처, 노멀 텍스처, 종류, 색). 이름은 팩 FBX의 재질 이름과 같아야 바뀐다.
            private static readonly (string name, string baseMap, string normalMap, Kind kind, Color color)[] Specs =
            {
                ("MI_Trim_01", "T_Trim_01_BaseColor_Red", "T_Trim_01_Normal", Kind.Textured, Color.white),
                ("MI_Trim_01_Floor", "T_Trim_01_BaseColor_Red", "T_Trim_01_Normal", Kind.Textured, Color.white),
                ("MI_Trim_01_Stairs", "T_Trim_01_BaseColor_Red", "T_Trim_01_Normal", Kind.Textured, Color.white),
                ("MI_Trim_02", "T_Trim_02_BaseColor_Red", "T_Trim_02_Normal", Kind.Textured, Color.white),
                ("MI_Trim_02_Floor", "T_Trim_02_BaseColor", "T_Trim_02_Normal", Kind.Textured, Color.white),
                ("MI_Trim_02_Rails", "T_Trim_02_BaseColor_Red", "T_Trim_02_Normal", Kind.Textured, Color.white),
                ("MI_Trim_02_Variation", "T_Trim_02_BaseColor", "T_Trim_02_Normal", Kind.Textured, Color.white),
                ("MI_Trim_03", "T_Trim_03_BaseColor", "T_Trim_03_Normal", Kind.Textured, Color.white),
                ("MI_Trim_03_Cables", "T_Trim_03_Cables", "T_Trim_03_Normal", Kind.Textured, Color.white),
                ("MI_Trim_03_Cables_Blue", "T_Trim_03_Cables", "T_Trim_03_Normal", Kind.Textured, new Color(0.6f, 0.75f, 1f)),
                ("MI_Trim_03_Dark", "T_Trim_03_Cables", "T_Trim_03_Normal", Kind.Textured, new Color(0.45f, 0.45f, 0.45f)),
                ("MI_PaddedWall", "T_PaddedWall_BaseColor", "T_PaddedWall_Normal", Kind.Textured, Color.white),
                ("M_Decal_Screen", "T_Decals", null, Kind.Textured, Color.white),
                ("MI_Decal_Grey", "T_Decals", null, Kind.Textured, new Color(0.6f, 0.6f, 0.6f)),
                ("MI_Decal_Red", "T_Decals", null, Kind.Textured, new Color(0.85f, 0.2f, 0.15f)),
                ("M_Decal_White", "T_Decals", null, Kind.Cutout, Color.white),
                ("MI_Decal_Line", null, null, Kind.Plain, new Color(0.8f, 0.19f, 0f)),
                ("M_Black", null, null, Kind.Plain, new Color(0.02f, 0.02f, 0.02f)),
                ("M_Light", null, null, Kind.Emissive, new Color(0.75f, 0.9f, 1f)),
                ("M_LightFade_Red", null, null, Kind.Emissive, new Color(1f, 0.15f, 0.1f)),
                ("M_Glass", null, null, Kind.Glass, new Color(0.7f, 0.85f, 0.9f, 0.25f)),
            };

            // ORM은 URP 금속성 맵 형식(R 금속성, A 매끄러움)이 아니라 쓰지 않고 값으로 대신한다.
            private const float TrimMetallic = 0.4f;
            private const float TrimSmoothness = 0.35f;

            /// <summary>재질이 이미 있으면 값만 다시 맞춰, 다시 실행해도 같은 재질을 쓰게 한다.</summary>
            public static Dictionary<string, Material> LoadOrCreate(string packFolder)
            {
                var factory = new MapMaterialFactory($"{packFolder}/Materials");
                var remap = new Dictionary<string, Material>();
                foreach (var spec in Specs)
                {
                    Material material = spec.kind switch
                    {
                        Kind.Emissive => factory.Emissive(spec.name, spec.color, spec.color * 2f),
                        Kind.Glass => factory.Transparent(spec.name, spec.color),
                        Kind.Plain => factory.Opaque(spec.name, spec.color, 0.2f, 0.4f),
                        _ => factory.Opaque(spec.name, spec.color, TrimMetallic, TrimSmoothness),
                    };
                    SetTexture(material, "_BaseMap", LoadTexture(packFolder, spec.baseMap, isNormalMap: false));
                    Texture normalMap = LoadTexture(packFolder, spec.normalMap, isNormalMap: true);
                    SetTexture(material, "_BumpMap", normalMap);
                    if (normalMap != null)
                        material.EnableKeyword("_NORMALMAP");
                    if (spec.kind == Kind.Cutout)
                    {
                        material.SetFloat("_AlphaClip", 1f);
                        material.SetFloat("_Cutoff", 0.5f);
                        material.EnableKeyword("_ALPHATEST_ON");
                    }
                    EditorUtility.SetDirty(material);
                    remap[spec.name] = material;
                }
                AssetDatabase.SaveAssets();
                return remap;
            }

            /// <summary>노멀맵을 일반 텍스처로 읽으면 표면이 뒤틀려 보여서, 처음 쓸 때 가져오기 형식을 노멀맵으로 바꾼다.</summary>
            private static Texture LoadTexture(string packFolder, string textureName, bool isNormalMap)
            {
                if (string.IsNullOrEmpty(textureName))
                    return null;
                string path = $"{packFolder}/Textures/{textureName}.png";
                if (isNormalMap && AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                    Debug.LogWarning($"[LabSciFiDecorator] 텍스처를 찾지 못했습니다: {path}");
                return texture;
            }

            private static void SetTexture(Material material, string property, Texture texture)
            {
                if (material.HasProperty(property))
                    material.SetTexture(property, texture);
            }
        }
    }
}
