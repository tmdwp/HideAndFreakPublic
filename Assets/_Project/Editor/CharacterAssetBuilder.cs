using System.IO;
using System.Linq;
using GuardVsMannequin.Player;
using GuardVsMannequin.Player.Guard;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>새 프로젝트에서도 메뉴 하나로 준비되도록 캐릭터 외형과 무기 모델을 만든다.</summary>
    public static class CharacterAssetBuilder
    {
        internal const string SkinFolder = "Assets/_Project/Art/Skins";
        private const string WeaponFolder = "Assets/_Project/Art/Weapons";

        public const string DummySkinPath = SkinFolder + "/Skin_Dummy.asset";
        public const string GuardSkinPath = SkinFolder + "/Skin_Guard.asset";
        public const string GhoulSkinPath = SkinFolder + "/Skin_Ghoul.asset";
        public const string WeaponViewPath = WeaponFolder + "/Weapon_View.prefab";

        public const string BulletPrefabPath = "Assets/MasterStylizedProjectiles/Projectiles/OrangeGunShot/Prefabs/Par_OrangeShoot_Bullet.prefab";
        public const string HitPrefabPath = "Assets/MasterStylizedProjectiles/Projectiles/OrangeGunShot/Prefabs/Par_OrangeShoot_Hit.prefab";
        public const string MuzzleFlashPrefabPath = "Assets/MasterStylizedProjectiles/Projectiles/OrangeGunShot/Prefabs/Par_OrangeShoot_Muzzle.prefab";

        private const string HumanoidControllerPath = SkinFolder + "/AC_HumanoidLocomotion.controller";
        private const string KevinRoot = "Assets/Kevin Iglesias/Human Animations";
        private const string MannequinSource = LowPolyCharacterCheck.ModelFolder + "/SK_Mannequin.fbx";
        private const string KevinMaleAnimations = KevinRoot + "/Animations/Male";
        private const string GuardSource = LowPolyCharacterCheck.ModelFolder + "/SK_Guard.fbx";
        private const string GunSource = LowPolyCharacterCheck.ModelFolder + "/SM_GuardRifle.fbx";
        private const string MuzzleName = "Muzzle";

        private const float HumanHeight = 1.8f;

        // 사냥 시간 구울은 위압감을 주도록 사람보다 크게 만든다.
        private const float GhoulHeight = 2.4f;

        // 걷기/달리기 블렌드 트리의 기준 속도(m/s)
        private const float WalkSpeed = 1.6f;
        private const float RunSpeed = 4.5f;

        /// <summary>새 프로젝트에서 메뉴 하나로 모든 외형·총을 준비하도록 한 번에 만든다.</summary>
        [MenuItem(EditorMenus.Root + "캐릭터·무기 에셋 생성")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(SkinFolder);
            Directory.CreateDirectory(WeaponFolder);
            AssetDatabase.Refresh();

            BuildMannequinSkin();
            BuildGuardSkin();
            BuildGhoulSkin();

            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterAssetBuilder] 캐릭터 외형과 무기 모델 생성 완료: " + SkinFolder);
        }

        /// <summary>
        /// 구울 모델만 고쳤을 때 손으로 조정한 가드 총 위치를 덮어쓰지 않도록 구울만 다시 만든다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/구울 외형만 다시 만들기")]
        private static void BuildGhoulSkinFromMenu()
        {
            Directory.CreateDirectory(SkinFolder);
            BuildGhoulSkin();
            AssetDatabase.SaveAssets();
        }

        /// <summary>구울은 사람 뼈 구조가 아니라 전용 애니메이터로 만들고, 공격 지연을 클립에 맞춰 다시 정한다.</summary>
        private static void BuildGhoulSkin()
        {
            AnimatorController ghoulController = GhoulSkinBuilder.Prepare();
            if (ghoulController == null)
                return;

            CharacterSkin ghoulSkin = CreateSkin("Ghoul", GhoulSkinBuilder.SourcePath, ghoulController, GhoulHeight, useAimIK: false, GhoulSkinPath);
            if (ghoulSkin == null)
                return;
            ghoulSkin.attackHitDelay = GhoulSkinBuilder.DefaultAttackHitDelay();
            ghoulSkin.lungeHitDelay = GhoulSkinBuilder.DefaultLungeHitDelay();
            EditorUtility.SetDirty(ghoulSkin);
            Debug.Log($"[CharacterAssetBuilder] 구울 외형 생성: {GhoulSkinPath} " +
                      $"(맞는 순간 공격 {ghoulSkin.attackHitDelay:F3}초, 도약 {ghoulSkin.lungeHitDelay:F3}초)");
        }

        /// <summary>
        /// 마네킹 모델만 고쳤을 때 다른 외형을 다시 만드느라 기다리지 않도록 마네킹만 다시 만든다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/마네킹 외형만 다시 만들기")]
        private static void BuildMannequinSkinFromMenu()
        {
            Directory.CreateDirectory(SkinFolder);
            BuildMannequinSkin();
            AssetDatabase.SaveAssets();
        }

        /// <summary>뼈 연결이 틀리면 게임에서 자세가 깨지므로 먼저 검사한 뒤 마네킹 전용 컨트롤러로 외형을 만든다.</summary>
        private static void BuildMannequinSkin()
        {
            var report = new System.Collections.Generic.List<string>();
            if (!LowPolyCharacterCheck.Check(MannequinSource, report))
            {
                Debug.LogError($"[CharacterAssetBuilder] 마네킹 모델 검사 실패: {MannequinSource}\n{string.Join("\n", report)}");
                return;
            }

            AnimatorController mannequinController = MannequinAnimatorBuilder.CreateController();
            CharacterSkin skin = CreateSkin("Dummy", MannequinSource, mannequinController, HumanHeight, useAimIK: false, DummySkinPath);
            if (skin != null)
            {
                skin.displayName = "Mannequin";
                EditorUtility.SetDirty(skin);
            }
            Debug.Log($"[CharacterAssetBuilder] 마네킹 외형 생성: {DummySkinPath}\n{string.Join("\n", report)}");
        }

        /// <summary>
        /// 가드 모델이나 총만 고쳤을 때 다른 외형을 다시 만드느라 기다리지 않도록 가드와 총만 다시 만든다.
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/가드 외형·총만 다시 만들기")]
        private static void BuildGuardSkinFromMenu()
        {
            Directory.CreateDirectory(SkinFolder);
            Directory.CreateDirectory(WeaponFolder);
            BuildGuardSkin();
            AssetDatabase.SaveAssets();
        }

        /// <summary>뼈 연결이 틀리면 총이 손에 붙지 않으므로 먼저 검사하고, 손으로 다듬은 총 위치는 유지한다.</summary>
        private static void BuildGuardSkin()
        {
            var report = new System.Collections.Generic.List<string>();
            if (!LowPolyCharacterCheck.Check(GuardSource, report))
            {
                Debug.LogError($"[CharacterAssetBuilder] 가드 모델 검사 실패: {GuardSource}\n{string.Join("\n", report)}");
                return;
            }

            AnimatorController humanoidController = CreateHumanoidLocomotionController(HumanoidControllerPath);
            CreateSkin("Guard", GuardSource, humanoidController, HumanHeight, useAimIK: true, GuardSkinPath);
            CreateGun();
            Debug.Log($"[CharacterAssetBuilder] 가드 외형·총 생성: {GuardSkinPath}, {WeaponViewPath}\n{string.Join("\n", report)}");
        }

        #region 애니메이터

        /// <summary>옆·뒤로 걸을 때도 발이 미끄러지지 않도록 로컬 이동 속도로 8방향을 섞는 블렌드 트리를 만든다.</summary>
        /// <param name="path">저장할 컨트롤러 경로.</param>
        internal static AnimatorController CreateHumanoidLocomotionController(string path)
        {
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out BlendTree blendTree, 0);
            controller.layers[0].stateMachine.defaultState = locomotionState;
            blendTree.blendType = BlendTreeType.FreeformDirectional2D;
            blendTree.blendParameter = "MoveX";
            blendTree.blendParameterY = "MoveZ";

            AddClip(blendTree, "Idles/HumanM@Idle01", Vector2.zero);
            string[] directions = { "Forward", "Backward", "Left", "Right", "ForwardLeft", "ForwardRight", "BackwardLeft", "BackwardRight" };
            foreach (string direction in directions)
            {
                Vector2 directionVector = DirectionVector(direction);
                AddClip(blendTree, $"Movement/Walk/HumanM@Walk01_{direction}", directionVector * WalkSpeed);
                AddClip(blendTree, $"Movement/Run/HumanM@Run01_{direction}", directionVector * RunSpeed);
            }

            // IK Pass는 레이어 배열을 복사해 바꾼 뒤 다시 넣어야 저장된다.
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>방향을 하나씩 손으로 적지 않도록 파일 이름의 방향 단어로 블렌드 트리 위치를 정한다.</summary>
        private static Vector2 DirectionVector(string direction)
        {
            float x = direction.Contains("Left") ? -1f : direction.Contains("Right") ? 1f : 0f;
            float z = direction.Contains("Forward") ? 1f : direction.Contains("Backward") ? -1f : 0f;
            return new Vector2(x, z).normalized;
        }

        /// <summary>캐릭터가 캡슐 밖으로 미끄러지지 않도록 클립을 넣기 전에 제자리 재생 설정을 맞춘다.</summary>
        private static void AddClip(BlendTree blendTree, string relativePath, Vector2 position)
        {
            string clipPath = $"{KevinMaleAnimations}/{relativePath}.fbx";
            DisableBakeIntoPoseXZ(clipPath);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .FirstOrDefault(animationClip => !animationClip.name.StartsWith("__preview__"));
            if (clip == null)
            {
                Debug.LogWarning($"[CharacterAssetBuilder] 애니메이션을 찾을 수 없습니다: {clipPath}");
                return;
            }
            blendTree.AddChild(clip, position);
        }

        /// <summary>켜 두면 반복할 때 캐릭터가 제자리로 튀므로 Root Transform Position (XZ) Bake Into Pose를 끈다.</summary>
        private static void DisableBakeIntoPoseXZ(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool isChanged = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.lockRootPositionXZ)
                    continue;
                clip.lockRootPositionXZ = false;
                isChanged = true;
            }
            if (!isChanged)
                return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        #endregion

        #region 외형

        /// <summary>게임은 외형의 발이 원점·정해진 키라고 가정하므로 원본 모델을 그 형태로 맞춰 저장한다.</summary>
        /// <param name="stripInactiveParts">꺼져 있는 교체용 파츠를 지울지 여부.</param>
        internal static CharacterSkin CreateSkin(string skinName, string sourcePath, RuntimeAnimatorController controller,
            float targetHeight, bool useAimIK, string skinAssetPath, bool stripInactiveParts = false)
        {
            GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (sourceModel == null)
            {
                Debug.LogError($"[CharacterAssetBuilder] 원본 모델을 찾을 수 없습니다: {sourcePath}");
                return null;
            }

            GameObject model = InstantiateClean(sourceModel);
            if (stripInactiveParts)
                RemoveInactiveParts(model);
            UrpMaterialConverter.ConvertRenderers(model.GetComponentsInChildren<Renderer>(true));
            EnsureAnimator(model, sourcePath);
            WarnIfNotHumanoid(model, skinName, useAimIK);

            GameObject root = NormalizeToFeet(model, $"Model_{skinName}", targetHeight);
            Debug.Log("[CharacterAssetBuilder] " + SkinPartBaker.Bake(root));

            // 덮어쓰면 손으로 넣은 손전등 지점 등이 사라지므로 기존 모델에서 읽어 새 모델에 되살린다.
            string prefabPath = $"{SkinFolder}/Model_{skinName}.prefab";
            int restoredPoints = ModelAttachPoints.Restore(root, ModelAttachPoints.Capture(prefabPath));
            if (restoredPoints > 0)
                Debug.Log($"[CharacterAssetBuilder] Model_{skinName}: 부착 지점 {restoredPoints}개를 이전 모델에서 옮겼습니다.");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            CharacterSkin skin = AssetDatabase.LoadAssetAtPath<CharacterSkin>(skinAssetPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<CharacterSkin>();
                AssetDatabase.CreateAsset(skin, skinAssetPath);
            }
            skin.displayName = skinName;
            skin.modelPrefab = prefab;
            skin.animatorController = controller;
            skin.useAimIK = useAimIK;
            EditorUtility.SetDirty(skin);
            return skin;
        }

        /// <summary>이동은 캐릭터 컨트롤러가 하므로 루트 모션을 끄고, 화면 밖 캐릭터는 뼈 계산을 멈춘다.</summary>
        private static void EnsureAnimator(GameObject model, string sourcePath)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
                animator.avatar = FindAvatar(sourcePath);
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        /// <summary>꺼진 교체용 장비는 용량만 늘고 크기 맞춤에 섞이므로 지우되, 켜진 메시가 쓰는 뼈는 남긴다.</summary>
        private static void RemoveInactiveParts(GameObject model)
        {
            var usedBones = new System.Collections.Generic.HashSet<Transform>();
            foreach (SkinnedMeshRenderer skinnedRenderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                foreach (Transform bone in skinnedRenderer.bones)
                {
                    if (bone != null)
                        usedBones.Add(bone);
                }
                if (skinnedRenderer.rootBone != null)
                    usedBones.Add(skinnedRenderer.rootBone);
            }

            Transform[] inactiveParts = model.GetComponentsInChildren<Transform>(true)
                .Where(part => part != model.transform && !part.gameObject.activeSelf)
                .ToArray();
            foreach (Transform part in inactiveParts)
            {
                // 이미 지운 부모 아래에 있었거나, 쓰이는 뼈를 품고 있으면 건너뛴다.
                if (part == null || part.GetComponentsInChildren<Transform>(true).Any(usedBones.Contains))
                    continue;
                Object.DestroyImmediate(part.gameObject);
            }
        }

        /// <summary>조준 IK는 휴머노이드 손 뼈로 동작하므로 아니면 만들 때 바로 알린다.</summary>
        private static void WarnIfNotHumanoid(GameObject model, string skinName, bool useAimIK)
        {
            if (!useAimIK)
                return;

            Animator animator = model.GetComponentInChildren<Animator>();
            Avatar avatar = animator != null ? animator.avatar : null;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                Debug.LogError($"[CharacterAssetBuilder] {skinName} 모델의 Avatar가 올바른 휴머노이드가 아닙니다. " +
                               "원본 FBX의 Rig 탭에서 Animation Type을 Humanoid로 두고 Configure로 뼈 연결을 확인하세요.");
        }

        /// <summary>
        /// 에셋 팩은 모델을 프리팹으로 감싸 주는 경우가 많아, 원본에 Avatar가 없으면 의존하는 모델 파일까지 찾아본다.
        /// </summary>
        private static Avatar FindAvatar(string sourcePath)
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null)
                return avatar;

            foreach (string dependency in AssetDatabase.GetDependencies(sourcePath, true))
            {
                avatar = AssetDatabase.LoadAllAssetsAtPath(dependency).OfType<Avatar>().FirstOrDefault();
                if (avatar != null)
                    return avatar;
            }
            return null;
        }

        #endregion

        #region 무기

        /// <summary>GuardWeaponView가 총구·손 표시를 찾을 수 있도록 소총 모델의 표시를 루트 바로 아래로 옮겨 총 프리팹을 만든다.</summary>
        private static void CreateGun()
        {
            GameObject gunModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GunSource);
            if (gunModelAsset == null)
            {
                Debug.LogError($"[CharacterAssetBuilder] 총 모델을 찾을 수 없습니다: {GunSource}");
                return;
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(WeaponViewPath));
            GameObject model = InstantiateClean(gunModelAsset);
            UrpMaterialConverter.ConvertRenderers(model.GetComponentsInChildren<Renderer>(true));
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            foreach (string spotName in new[] { MuzzleName, GunGripSpots.RightHand, GunGripSpots.LeftHand })
            {
                Transform spot = GunGripSpots.Find(model.transform, spotName);
                if (spot == null)
                {
                    Debug.LogError($"[CharacterAssetBuilder] 총 모델에 '{spotName}' 표시가 없습니다: {GunSource}");
                    continue;
                }
                spot.SetParent(root.transform, true);
                spot.name = spotName;
                spot.localRotation = Quaternion.identity;
            }

            foreach (Renderer gunRenderer in root.GetComponentsInChildren<Renderer>(true))
                gunRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            PrefabUtility.SaveAsPrefabAsset(root, WeaponViewPath);
            Object.DestroyImmediate(root);
        }

        #endregion

        #region 공용

        /// <summary>에셋 팩 모델에 붙어 온 스크립트·충돌체·카메라 등이 게임 캐릭터와 충돌하므로 제거한다.</summary>
        internal static GameObject InstantiateClean(GameObject source)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null)
                instance = Object.Instantiate(source);
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            RemoveAll<MonoBehaviour>(instance);
            RemoveAll<Collider>(instance);
            RemoveAll<Rigidbody>(instance);
            RemoveAll<CharacterController>(instance);
            RemoveAll<Camera>(instance);
            RemoveAll<Light>(instance);
            RemoveAll<AudioSource>(instance);
            return instance;
        }

        /// <summary>지울 컴포넌트 종류가 많아 같은 반복을 형식 매개변수 하나로 처리한다.</summary>
        private static void RemoveAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
                Object.DestroyImmediate(component);
        }

        /// <summary>모델마다 원점·크기가 제각각이라 캡슐에 맞도록 키를 맞추고 발을 원점에 옮긴다.</summary>
        private static GameObject NormalizeToFeet(GameObject model, string rootName, float targetHeight)
        {
            var root = new GameObject(rootName);
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Bounds modelBounds = GetRendererBounds(root);
            if (modelBounds.size.y > 0.001f)
                model.transform.localScale *= targetHeight / modelBounds.size.y;

            modelBounds = GetRendererBounds(root);
            model.transform.localPosition -= new Vector3(modelBounds.center.x, modelBounds.min.y, modelBounds.center.z);
            return root;
        }

        /// <summary>원점이 제각각인 모델의 실제 크기를 알려고 자식 렌더러 전체의 월드 경계를 구한다.</summary>
        internal static Bounds GetRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds combinedBounds = renderers[0].bounds;
            foreach (Renderer childRenderer in renderers)
                combinedBounds.Encapsulate(childRenderer.bounds);
            return combinedBounds;
        }

        #endregion
    }
}
