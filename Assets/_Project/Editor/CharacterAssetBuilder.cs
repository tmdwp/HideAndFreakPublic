using System.IO;
using System.Linq;
using GuardVsMannequin.Player;
using GuardVsMannequin.Player.Guard;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 캐릭터 외형과 무기 모델을 만든다.
    /// - 마네킹 외형: Blender로 만든 로우폴리 매장 마네킹(Art/Characters/Models/SK_Mannequin.fbx, Humanoid)
    ///   + Kevin Iglesias 이동 애니메이션 (파일 이름 Skin_Dummy·Model_Dummy는 기존 프리팹 참조를 지키려고 그대로 둔다)
    /// - 가드 외형: Blender로 만든 로우폴리 시설 경비원(SK_Guard.fbx, Humanoid) + Kevin 이동 애니메이션, 조준 IK
    /// - 사냥 시간 마네킹 외형: Blender로 만든 로우폴리 구울(SK_Ghoul.fbx, Generic + 자체 애니메이션, 준비는 <see cref="GhoulSkinBuilder"/>)
    /// - 가드 소총: Blender로 만든 로우폴리 소총(SM_GuardRifle.fbx, 총구 +Z, Muzzle·Right Hand·Left Hand 표시 포함)
    ///   (Art/Weapons/Weapon_View.prefab에 덮어써 Guard 프리팹의 참조를 유지한다)
    /// 모델은 스크립트·충돌체를 제거하고, 발을 원점에 맞추고, 정해진 키로 크기를 맞춰 저장한다.
    /// 머티리얼은 URP Lit으로 변환한다. 다시 실행하면 같은 경로에 덮어쓴다.
    /// </summary>
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

        // 사냥 시간 Ghoul은 위압감을 주도록 사람보다 크게 만든다. (기본 자세 경계 높이 기준)
        private const float GhoulHeight = 2.4f;

        // 걷기/달리기 블렌드 트리의 기준 속도(m/s)
        private const float WalkSpeed = 1.6f;
        private const float RunSpeed = 4.5f;

        /// <summary>
        /// 모든 캐릭터 외형과 무기 모델을 만든다.
        /// </summary>
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
        /// 사냥 시간 구울 외형만 다시 만든다. (마네킹·가드·총은 건드리지 않음)
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/구울 외형만 다시 만들기")]
        private static void BuildGhoulSkinFromMenu()
        {
            Directory.CreateDirectory(SkinFolder);
            BuildGhoulSkin();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 사냥 시간 마네킹 외형(Generic 리그, 전용 애니메이터)을 만든다.
        /// 맞는 순간(attackHitDelay)은 Blender 공격 클립의 맞는 프레임으로 정해지므로 다시 만들 때마다 맞춘다.
        /// </summary>
        private static void BuildGhoulSkin()
        {
            AnimatorController ghoul = GhoulSkinBuilder.Prepare();
            if (ghoul == null)
                return;

            CharacterSkin ghoulSkin = CreateSkin("Ghoul", GhoulSkinBuilder.SourcePath, ghoul, GhoulHeight, useAimIK: false, GhoulSkinPath);
            if (ghoulSkin == null)
                return;
            ghoulSkin.attackHitDelay = GhoulSkinBuilder.DefaultAttackHitDelay();
            EditorUtility.SetDirty(ghoulSkin);
            Debug.Log($"[CharacterAssetBuilder] 구울 외형 생성: {GhoulSkinPath} (맞는 순간 {ghoulSkin.attackHitDelay:F3}초)");
        }

        /// <summary>
        /// 마네킹 외형만 다시 만든다. (가드·구울·총은 건드리지 않음)
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/마네킹 외형만 다시 만들기")]
        private static void BuildMannequinSkinFromMenu()
        {
            Directory.CreateDirectory(SkinFolder);
            BuildMannequinSkin();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 로우폴리 마네킹 모델을 Humanoid로 임포트·검사하고, 이동 + 감정 표현·상호작용 컨트롤러(가드와 따로)로 외형을 만든다.
        /// </summary>
        private static void BuildMannequinSkin()
        {
            var report = new System.Collections.Generic.List<string>();
            if (!LowPolyCharacterCheck.Check(MannequinSource, report))
            {
                Debug.LogError($"[CharacterAssetBuilder] 마네킹 모델 검사 실패: {MannequinSource}\n{string.Join("\n", report)}");
                return;
            }

            AnimatorController mannequin = MannequinAnimatorBuilder.CreateController();
            CharacterSkin skin = CreateSkin("Dummy", MannequinSource, mannequin, HumanHeight, useAimIK: false, DummySkinPath);
            if (skin != null)
            {
                skin.displayName = "Mannequin";
                EditorUtility.SetDirty(skin);
            }
            Debug.Log($"[CharacterAssetBuilder] 마네킹 외형 생성: {DummySkinPath}\n{string.Join("\n", report)}");
        }

        /// <summary>
        /// 가드 외형과 총만 다시 만든다. (마네킹·구울은 건드리지 않음)
        /// </summary>
        [MenuItem(EditorMenus.Root + "캐릭터 생성/가드 외형·총만 다시 만들기")]
        private static void BuildGuardSkinFromMenu()
        {
            Directory.CreateDirectory(SkinFolder);
            Directory.CreateDirectory(WeaponFolder);
            BuildGuardSkin();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 로우폴리 경비원 모델을 Humanoid로 임포트·검사하고, 공용 이동 컨트롤러 + 조준 IK로 가드 외형을 만든 뒤 총 프리팹을 만든다.
        /// 총·손 위치 조정값(CharacterSkin.weaponGrip)은 외형 에셋에 그대로 남는다.
        /// </summary>
        private static void BuildGuardSkin()
        {
            var report = new System.Collections.Generic.List<string>();
            if (!LowPolyCharacterCheck.Check(GuardSource, report))
            {
                Debug.LogError($"[CharacterAssetBuilder] 가드 모델 검사 실패: {GuardSource}\n{string.Join("\n", report)}");
                return;
            }

            AnimatorController humanoid = CreateHumanoidLocomotionController(HumanoidControllerPath);
            CreateSkin("Guard", GuardSource, humanoid, HumanHeight, useAimIK: true, GuardSkinPath);
            CreateGun();
            Debug.Log($"[CharacterAssetBuilder] 가드 외형·총 생성: {GuardSkinPath}, {WeaponViewPath}\n{string.Join("\n", report)}");
        }

        #region 애니메이터

        /// <summary>
        /// 휴머노이드 공용 이동 컨트롤러를 만든다.
        /// 로컬 이동 속도(MoveX, MoveZ)로 대기/걷기/달리기 8방향을 섞는 2D 블렌드 트리 하나로 구성한다.
        /// 가드의 손 조준 IK를 위해 IK Pass를 켠다. 같은 경로에 있으면 지우고 새로 만든다.
        /// </summary>
        /// <param name="path">저장할 컨트롤러 경로.</param>
        internal static AnimatorController CreateHumanoidLocomotionController(string path)
        {
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            controller.layers[0].stateMachine.defaultState = state;
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";

            AddClip(tree, "Idles/HumanM@Idle01", Vector2.zero);
            string[] directions = { "Forward", "Backward", "Left", "Right", "ForwardLeft", "ForwardRight", "BackwardLeft", "BackwardRight" };
            foreach (string direction in directions)
            {
                Vector2 dir = DirectionVector(direction);
                AddClip(tree, $"Movement/Walk/HumanM@Walk01_{direction}", dir * WalkSpeed);
                AddClip(tree, $"Movement/Run/HumanM@Run01_{direction}", dir * RunSpeed);
            }

            // IK Pass는 레이어 배열을 복사해 바꾼 뒤 다시 넣어야 저장된다.
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// 방향 이름을 (x, z) 단위 벡터로 바꾼다.
        /// </summary>
        private static Vector2 DirectionVector(string direction)
        {
            float x = direction.Contains("Left") ? -1f : direction.Contains("Right") ? 1f : 0f;
            float z = direction.Contains("Forward") ? 1f : direction.Contains("Backward") ? -1f : 0f;
            return new Vector2(x, z).normalized;
        }

        /// <summary>
        /// Kevin Iglesias 남성 애니메이션 파일의 클립을 블렌드 트리에 추가한다.
        /// </summary>
        private static void AddClip(BlendTree tree, string relativePath, Vector2 position)
        {
            string path = $"{KevinMaleAnimations}/{relativePath}.fbx";
            DisableBakeIntoPoseXZ(path);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null)
            {
                Debug.LogWarning($"[CharacterAssetBuilder] 애니메이션을 찾을 수 없습니다: {path}");
                return;
            }
            tree.AddChild(clip, position);
        }

        /// <summary>
        /// 클립의 "Root Transform Position (XZ) Bake Into Pose"를 끈다.
        /// 켜져 있으면 루트 모션을 끈 캐릭터가 클립 동안 앞으로 나아갔다가 반복될 때 제자리로 돌아온다.
        /// 끄면 수평 이동이 루트 모션으로 분리되고, 루트 모션을 쓰지 않으므로 제자리 애니메이션이 된다.
        /// </summary>
        private static void DisableBakeIntoPoseXZ(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.lockRootPositionXZ)
                    continue;
                clip.lockRootPositionXZ = false;
                changed = true;
            }
            if (!changed)
                return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        #endregion

        #region 외형

        /// <summary>
        /// 원본 모델로 외형 프리팹과 <see cref="CharacterSkin"/> 에셋을 만들고 반환한다. 원본이 없으면 null.
        /// </summary>
        /// <param name="stripInactiveParts">꺼져 있는 파츠(교체용 장비 등)를 지울지 여부. 켜 두면 크기 맞춤 경계에서도 빠진다.</param>
        internal static CharacterSkin CreateSkin(string skinName, string sourcePath, RuntimeAnimatorController controller,
            float targetHeight, bool useAimIK, string skinAssetPath, bool stripInactiveParts = false)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[CharacterAssetBuilder] 원본 모델을 찾을 수 없습니다: {sourcePath}");
                return null;
            }

            GameObject model = InstantiateClean(source);
            if (stripInactiveParts)
                RemoveInactiveParts(model);
            UrpMaterialConverter.ConvertRenderers(model.GetComponentsInChildren<Renderer>(true));
            EnsureAnimator(model, sourcePath);
            WarnIfNotHumanoid(model, skinName, useAimIK);

            GameObject root = NormalizeToFeet(model, $"Model_{skinName}", targetHeight);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{SkinFolder}/Model_{skinName}.prefab");
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

        /// <summary>
        /// 모델에 애니메이터가 없으면 원본 모델 파일의 Avatar로 추가한다. 루트 모션은 끈다.
        /// </summary>
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

        /// <summary>
        /// 꺼져 있는 오브젝트를 지운다. 켜진 스킨드 메시가 뼈로 쓰는 트랜스폼이 들어 있으면 남긴다.
        /// </summary>
        private static void RemoveInactiveParts(GameObject model)
        {
            var usedBones = new System.Collections.Generic.HashSet<Transform>();
            foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                foreach (Transform bone in skinned.bones)
                {
                    if (bone != null)
                        usedBones.Add(bone);
                }
                if (skinned.rootBone != null)
                    usedBones.Add(skinned.rootBone);
            }

            Transform[] inactive = model.GetComponentsInChildren<Transform>(true)
                .Where(t => t != model.transform && !t.gameObject.activeSelf)
                .ToArray();
            foreach (Transform part in inactive)
            {
                // 이미 지운 부모 아래에 있었거나, 쓰이는 뼈를 품고 있으면 건너뛴다.
                if (part == null || part.GetComponentsInChildren<Transform>(true).Any(usedBones.Contains))
                    continue;
                Object.DestroyImmediate(part.gameObject);
            }
        }

        /// <summary>
        /// 조준 IK를 쓰는 외형인데 휴머노이드 Avatar가 아니면 경고한다. (손 뼈를 찾지 못해 총이 손에 붙지 않는다)
        /// </summary>
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
        /// 원본(프리팹이면 그 안의 모델 파일)에서 Avatar를 찾는다.
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

        /// <summary>
        /// Blender로 만든 로우폴리 소총(총구 +Z, 원점 = 권총 손잡이)으로 총 프리팹을 만든다.
        /// 모델에 함께 들어 있는 Muzzle·Right Hand·Left Hand 표시를 루트 바로 아래로 옮긴다. (GuardWeaponView가 Muzzle을 바로 아래 자식에서 찾음)
        /// 손·총구 위치를 바꾸려면 generate_guard_rifle.py의 위치 값을 고친 뒤 다시 만든다.
        /// </summary>
        private static void CreateGun()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GunSource);
            if (source == null)
            {
                Debug.LogError($"[CharacterAssetBuilder] 총 모델을 찾을 수 없습니다: {GunSource}");
                return;
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(WeaponViewPath));
            GameObject model = InstantiateClean(source);
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

        /// <summary>
        /// 원본을 씬에 풀어서 만들고, 게임에 필요 없는 스크립트·충돌체·물리·카메라·조명·오디오를 제거한다.
        /// </summary>
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

        /// <summary>
        /// 자식까지 포함해 해당 종류의 컴포넌트를 모두 제거한다.
        /// </summary>
        private static void RemoveAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
                Object.DestroyImmediate(component);
        }

        /// <summary>
        /// 모델을 감싸는 루트를 만들고, 높이를 맞춘 뒤 발이 원점·중심이 XZ 원점에 오도록 옮긴다.
        /// </summary>
        private static GameObject NormalizeToFeet(GameObject model, string rootName, float targetHeight)
        {
            var root = new GameObject(rootName);
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Bounds bounds = GetRendererBounds(root);
            if (bounds.size.y > 0.001f)
                model.transform.localScale *= targetHeight / bounds.size.y;

            bounds = GetRendererBounds(root);
            model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return root;
        }

        /// <summary>
        /// 자식 렌더러 전체의 월드 경계. (스킨드 메시는 기본 포즈 경계)
        /// </summary>
        internal static Bounds GetRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer target in renderers)
                bounds.Encapsulate(target.bounds);
            return bounds;
        }

        #endregion
    }
}
