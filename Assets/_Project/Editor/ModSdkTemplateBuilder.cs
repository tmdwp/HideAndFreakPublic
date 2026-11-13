using System.Collections.Generic;
using System.IO;
using GuardVsMannequin.Core;
using GuardVsMannequin.Map;
using GuardVsMannequin.Objectives;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GuardVsMannequin.Editor
{
    /// <summary>모드 제작자가 받자마자 내보내기까지 시험하도록 기본 도형만 쓴 예제 모드 템플릿을 만든다.</summary>
    public static class ModSdkTemplateBuilder
    {
        private const string SceneName = "Example_TemplateMap";
        private const float HalfSize = 18f;
        private const float WallHeight = 4f;
        private const float WallThickness = 0.4f;

        public static string ScenePath => $"{ModSdkSettings.TemplateFolder}/{SceneName}.unity";

        // 변신 사물 배치 (종류, 위치, 크기)
        private static readonly (PrimitiveType type, Vector3 position, Vector3 scale)[] PropLayout =
        {
            (PrimitiveType.Cube, new Vector3(-8f, 0.5f, 6f), new Vector3(1f, 1f, 1f)),
            (PrimitiveType.Cube, new Vector3(-5f, 0.6f, -9f), new Vector3(2f, 1.2f, 0.8f)),
            (PrimitiveType.Cube, new Vector3(7f, 0.4f, -5f), new Vector3(1.4f, 0.8f, 0.8f)),
            (PrimitiveType.Cylinder, new Vector3(9f, 0.6f, 7f), new Vector3(0.6f, 0.6f, 0.6f)),
            (PrimitiveType.Cylinder, new Vector3(-12f, 0.9f, -12f), new Vector3(0.9f, 0.9f, 0.9f)),
            (PrimitiveType.Sphere, new Vector3(2f, 0.4f, 2f), new Vector3(0.8f, 0.8f, 0.8f)),
            (PrimitiveType.Cube, new Vector3(13f, 0.9f, -13f), new Vector3(1.2f, 1.8f, 0.6f)),
            (PrimitiveType.Capsule, new Vector3(-2f, 1f, 12f), new Vector3(0.7f, 1f, 0.7f))
        };

        /// <summary>작업하던 씬을 잃지 않도록 저장 여부를 먼저 묻고 예제 맵·설정을 한 번에 만든다.</summary>
        [MenuItem(EditorMenus.Root + "모드/모드 SDK 예제 템플릿 다시 만들기")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string startScenePath = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory(ModSdkSettings.TemplateFolder);
            AssetDatabase.Refresh();

            Dictionary<string, Material> materials = CreateMaterials();
            BuildScene(materials);
            CreateModProject();

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(startScenePath))
                EditorSceneManager.OpenScene(startScenePath, OpenSceneMode.Single);
            Debug.Log($"[ModSdkTemplateBuilder] 예제 모드 템플릿을 만들었습니다: {ModSdkSettings.TemplateFolder}");
        }

        /// <summary>맵 검사를 통과하는 최소 구성을 보여 주는 예제 씬을 만든다.</summary>
        private static void BuildScene(Dictionary<string, Material> materials)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mapRoot = new GameObject("Map");
            MapInfo mapInfo = mapRoot.AddComponent<MapInfo>();
            Transform environment = CreateChild(mapRoot.transform, "Environment");

            CreateBlock(environment, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(HalfSize * 2f, 0.5f, HalfSize * 2f), materials["Floor"]);
            float wallCenterY = WallHeight * 0.5f;
            float sideLength = HalfSize * 2f;
            CreateBlock(environment, "Wall_North", new Vector3(0f, wallCenterY, HalfSize), new Vector3(sideLength, WallHeight, WallThickness), materials["Wall"]);
            CreateBlock(environment, "Wall_South", new Vector3(0f, wallCenterY, -HalfSize), new Vector3(sideLength, WallHeight, WallThickness), materials["Wall"]);
            CreateBlock(environment, "Wall_East", new Vector3(HalfSize, wallCenterY, 0f), new Vector3(WallThickness, WallHeight, sideLength), materials["Wall"]);
            CreateBlock(environment, "Wall_West", new Vector3(-HalfSize, wallCenterY, 0f), new Vector3(WallThickness, WallHeight, sideLength), materials["Wall"]);
            // 가운데 칸막이: 시야를 가려 숨을 곳을 만든다.
            CreateBlock(environment, "Divider", new Vector3(0f, 1.25f, -3f), new Vector3(10f, 2.5f, 0.3f), materials["Wall"]);

            Transform propGroup = CreateChild(environment, "Props");
            for (int i = 0; i < PropLayout.Length; i++)
            {
                GameObject prop = GameObject.CreatePrimitive(PropLayout[i].type);
                prop.name = $"Prop_{i:00}";
                prop.transform.SetParent(propGroup, false);
                prop.transform.localPosition = PropLayout[i].position;
                prop.transform.localScale = PropLayout[i].scale;
                prop.GetComponent<Renderer>().sharedMaterial = materials["Prop"];
                prop.isStatic = true;
                PropRegistrar.Register(prop, absorbChildren: true);
            }

            // 투명 천장: 벽을 타고 오른 마네킹이 맵 밖으로 나가지 않게 한다.
            var ceiling = new GameObject("Ceiling (Invisible)");
            ceiling.transform.SetParent(mapRoot.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, WallHeight + 0.25f, 0f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(sideLength, 0.5f, sideLength);

            CreateLights(mapRoot.transform, materials);
            CreatePoints(mapRoot.transform);

            var overview = new GameObject("CameraOverview");
            overview.transform.SetParent(mapRoot.transform, false);
            overview.transform.SetPositionAndRotation(new Vector3(0f, 26f, -24f), Quaternion.Euler(50f, 0f, 0f));
            var serializedMapInfo = new SerializedObject(mapInfo);
            serializedMapInfo.FindProperty("mapName").stringValue = "Example";
            serializedMapInfo.FindProperty("ceilingHeight").floatValue = WallHeight;
            serializedMapInfo.FindProperty("overviewPoint").objectReferenceValue = overview.transform;
            serializedMapInfo.ApplyModifiedPropertiesWithoutUndo();

            ApplyRealtimeOnlyLighting(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            BakeNavMesh(scene, mapRoot);
            PropIndexer.AssignIndices(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// 사냥 시간에 조명이 꺼지는 모습까지 예제로 보여 주려고, 전구 머티리얼이 함께 바뀌는 MapLight로 천장 조명 4개와 방향광을 만든다.
        /// </summary>
        private static void CreateLights(Transform mapRoot, Dictionary<string, Material> materials)
        {
            Transform lightGroup = CreateChild(mapRoot, "Lights");

            var sunObject = new GameObject("Directional Light");
            sunObject.transform.SetParent(lightGroup, false);
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sunLight = sunObject.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 0.6f;
            sunObject.AddComponent<MapLight>();

            foreach (Vector3 lampPosition in new[] { new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f), new Vector3(-9f, 0f, 9f), new Vector3(9f, 0f, 9f) })
            {
                var lamp = new GameObject("CeilingLamp");
                lamp.transform.SetParent(lightGroup, false);
                lamp.transform.localPosition = lampPosition + Vector3.up * (WallHeight - 0.1f);

                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulb";
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(lamp.transform, false);
                bulb.transform.localScale = Vector3.one * 0.3f;
                Renderer bulbRenderer = bulb.GetComponent<Renderer>();
                bulbRenderer.sharedMaterial = materials["LampOn"];

                var pointObject = new GameObject("Light");
                pointObject.transform.SetParent(lamp.transform, false);
                pointObject.transform.localPosition = Vector3.down * 0.3f;
                Light pointLight = pointObject.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.range = 12f;
                pointLight.intensity = 2f;
                pointLight.lightmapBakeType = LightmapBakeType.Realtime;

                MapLight mapLight = lamp.AddComponent<MapLight>();
                var serializedMapLight = new SerializedObject(mapLight);
                serializedMapLight.FindProperty("bulbRenderer").objectReferenceValue = bulbRenderer;
                serializedMapLight.FindProperty("onMaterial").objectReferenceValue = materials["LampOn"];
                serializedMapLight.FindProperty("offMaterial").objectReferenceValue = materials["LampOff"];
                serializedMapLight.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>예제가 경고 없이 통과하도록 맵 검사의 권장 지점 수를 채운다.</summary>
        private static void CreatePoints(Transform mapRoot)
        {
            Transform mannequinGroup = CreateChild(mapRoot, "MannequinSpawnPoints");
            foreach (Vector3 spawnPosition in Grid(-12f, 12f, 3, -12f, 4f, 3))
                CreateSpawnPoint(mannequinGroup, PlayerRole.Mannequin, spawnPosition);

            Transform guardGroup = CreateChild(mapRoot, "GuardSpawnPoints");
            foreach (Vector3 spawnPosition in new[] { new Vector3(15f, 0f, 15f), new Vector3(-15f, 0f, 15f), new Vector3(15f, 0f, 10f), new Vector3(-15f, 0f, 10f), new Vector3(0f, 0f, 15f) })
                CreateSpawnPoint(guardGroup, PlayerRole.Guard, spawnPosition);

            Transform generatorGroup = CreateChild(mapRoot, "GeneratorSpawnPoints");
            foreach (Vector3 generatorPosition in Grid(-15f, 15f, 4, -15f, 15f, 3))
            {
                var generatorPoint = new GameObject("GeneratorSpawnPoint");
                generatorPoint.transform.SetParent(generatorGroup, false);
                Vector3 toCenter = new Vector3(-generatorPosition.x, 0f, -generatorPosition.z);
                generatorPoint.transform.SetPositionAndRotation(generatorPosition, Quaternion.LookRotation(toCenter.sqrMagnitude > 0.01f ? toCenter : Vector3.forward));
                generatorPoint.AddComponent<GeneratorSpawnPoint>();
            }
        }

        /// <summary>지점을 손으로 하나씩 적지 않도록 범위를 나눈 격자 위치를 만든다.</summary>
        private static IEnumerable<Vector3> Grid(float xMin, float xMax, int xCount, float zMin, float zMax, int zCount)
        {
            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                for (int zIndex = 0; zIndex < zCount; zIndex++)
                {
                    float pointX = xCount == 1 ? xMin : Mathf.Lerp(xMin, xMax, xIndex / (float)(xCount - 1));
                    float pointZ = zCount == 1 ? zMin : Mathf.Lerp(zMin, zMax, zIndex / (float)(zCount - 1));
                    yield return new Vector3(pointX, 0f, pointZ);
                }
            }
        }

        /// <summary>시작하자마자 맵 가운데를 바라보도록 방향을 정한다.</summary>
        private static void CreateSpawnPoint(Transform parent, PlayerRole role, Vector3 position)
        {
            var spawnObject = new GameObject($"SpawnPoint_{role}");
            spawnObject.transform.SetParent(parent, false);
            spawnObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(-position.x, 0f, -position.z + 0.01f)));
            SpawnPoint spawnPoint = spawnObject.AddComponent<SpawnPoint>();
            var serializedSpawnPoint = new SerializedObject(spawnPoint);
            serializedSpawnPoint.FindProperty("role").enumValueIndex = (int)role;
            serializedSpawnPoint.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>게임은 모드 맵의 NavMesh를 실행 중에 구울 수 없으므로 미리 구워 저장한다.</summary>
        private static void BakeNavMesh(Scene scene, GameObject mapRoot)
        {
            NavMeshSurface surface = mapRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (surface.navMeshData == null)
            {
                Debug.LogWarning("[ModSdkTemplateBuilder] NavMesh를 굽지 못했습니다. 예제 씬에서 NavMeshSurface의 Bake를 누르세요.");
                return;
            }

            string navMeshFolder = $"{ModSdkSettings.TemplateFolder}/{scene.name}";
            if (!AssetDatabase.IsValidFolder(navMeshFolder))
                AssetDatabase.CreateFolder(ModSdkSettings.TemplateFolder, scene.name);
            string navMeshPath = $"{navMeshFolder}/NavMesh-Map.asset";
            AssetDatabase.DeleteAsset(navMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navMeshPath);
            EditorUtility.SetDirty(surface);
        }

        /// <summary>
        /// 게임 프로젝트의 조명 설정 에셋은 SDK에 들어가지 않으므로 템플릿 폴더 안에 따로 두어 SDK에서도 같은 설정(GI 끔)이 쓰이게 한다.
        /// </summary>
        private static void ApplyRealtimeOnlyLighting(Scene scene)
        {
            string settingsPath = $"{ModSdkSettings.TemplateFolder}/Lighting_RealtimeOnly.lighting";
            LightingSettings lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if (lightingSettings == null)
            {
                lightingSettings = new LightingSettings { name = "Lighting_RealtimeOnly", bakedGI = false, realtimeGI = false };
                AssetDatabase.CreateAsset(lightingSettings, settingsPath);
            }
            Lightmapping.SetLightingSettingsForScene(scene, lightingSettings);
        }

        /// <summary>제작자가 복사해 쓸 수 있도록 모든 항목을 채운 예제 설정을 만든다.</summary>
        private static void CreateModProject()
        {
            string projectPath = $"{ModSdkSettings.TemplateFolder}/ExampleModProject.asset";
            AssetDatabase.DeleteAsset(projectPath);
            var project = ScriptableObject.CreateInstance<ModProject>();
            project.modId = "example.template";
            project.modName = "Example Template";
            project.version = "1.0.0";
            project.author = "Hide&Freak";
            project.description = "모드 SDK 예제 맵. 이 설정을 복사해 자기 모드를 만드세요.";
            project.minGameVersion = PlayerSettings.bundleVersion;
            project.maps.Add(new ModProject.MapSetting
            {
                scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                mapKey = "example",
                displayName = "Example Map",
                names = new List<ModProject.LocalizedName>
                {
                    new() { language = "ko", text = "예제 맵" },
                    new() { language = "en", text = "Example Map" }
                },
                minPlayers = 0,
                maxPlayers = 0,
                settings = { new ModProject.RangeSetting { id = "GuardCount", value = 1f, min = 1f, max = 2f } }
            });
            AssetDatabase.CreateAsset(project, projectPath);
        }

        /// <summary>에셋 팩 텍스처는 재배포할 수 없어 단색 머티리얼만 쓰고, 이미 있으면 색만 맞춘다.</summary>
        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            var materialColors = new Dictionary<string, Color>
            {
                { "Floor", new Color(0.35f, 0.35f, 0.38f) },
                { "Wall", new Color(0.55f, 0.52f, 0.48f) },
                { "Prop", new Color(0.6f, 0.45f, 0.3f) },
                { "LampOn", Color.white },
                { "LampOff", new Color(0.2f, 0.2f, 0.2f) }
            };

            var materials = new Dictionary<string, Material>();
            foreach (KeyValuePair<string, Color> colorEntry in materialColors)
            {
                string materialPath = $"{ModSdkSettings.TemplateFolder}/M_Example{colorEntry.Key}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(litShader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetColor("_BaseColor", colorEntry.Value);
                if (colorEntry.Key == "LampOn")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.white * 2f);
                }
                EditorUtility.SetDirty(material);
                materials[colorEntry.Key] = material;
            }
            return materials;
        }

        /// <summary>
        /// 그룹 오브젝트를 만드는 코드가 반복되어 한 줄로 쓰려고 둔다.
        /// </summary>
        private static Transform CreateChild(Transform parent, string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// <summary>
        /// 바닥·벽은 걸어 다녀야 하므로 기본 상자의 충돌체를 그대로 두고, 오클루전·NavMesh 대상이 되도록 정적으로 둔다.
        /// </summary>
        private static GameObject CreateBlock(Transform parent, string blockName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            block.isStatic = true;
            return block;
        }
    }
}
