using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 사냥 시간 마네킹 외형 구울(Blender로 만든 SK_Ghoul.fbx, Generic 리그)의 임포트 설정과 애니메이터를 준비한다. (Phase 11-4·11-5)
    /// - 임포트: Generic + 자체 Avatar, 클립 이름을 짧게(Armature|Ghoul_Idle → Ghoul_Idle), 공격·비명 외 클립은 반복
    /// - 머티리얼: 눈(M_Char_GhoulEye)만 어둠에서도 빛나는 URP 발광 머티리얼로 바꿔 끼운다. 나머지는 모델 색 그대로.
    /// - 애니메이터: Speed에 따라 대기 → 다가가기 → 기어가기, Attack·Shriek 트리거, 덧입히는 경련 레이어
    /// 외형 프리팹·<see cref="CharacterSkin"/> 저장은 <see cref="CharacterAssetBuilder"/>가 한다.
    /// 클립 길이·맞는 프레임은 Art/Characters/Source~/generate_ghoul.py와 맞춘다.
    /// </summary>
    public static class GhoulSkinBuilder
    {
        public const string SourcePath = LowPolyCharacterCheck.ModelFolder + "/SK_Ghoul.fbx";
        private const string ControllerPath = CharacterAssetBuilder.SkinFolder + "/AC_Ghoul.controller";
        private const string EyeSourceMaterial = "M_Char_GhoulEye";

        private const string IdleClip = "Ghoul_Idle";
        private const string StalkClip = "Ghoul_Stalk";
        private const string CrawlClip = "Ghoul_Crawl";
        private const string AttackClip = "Ghoul_Attack";
        private const string ShriekClip = "Ghoul_Shriek";
        private const string TwitchClip = "Ghoul_Twitch";

        // 한 번만 재생하는 클립. 나머지는 반복한다.
        private static readonly HashSet<string> OneShotClips = new() { AttackClip, ShriekClip };

        // generate_ghoul.py의 FPS와 ATTACK_HIT_FRAME (손이 앞으로 내려와 맞는 프레임)
        private const float ClipFps = 24f;
        private const int AttackHitFrame = 11;

        // 이동 속도(m/s) 기준. 구울 걷기 약 2.1, 달리기 약 3.4 (game_tuning.csv)
        private const float StalkSpeed = 0.3f;
        private const float CrawlSpeed = 2.7f;
        private const float CrawlExitSpeed = 2.5f;

        // 멈출 때는 아주 짧게 넘어가 "딱 멈추는" 느낌을 낸다.
        private const float StopDuration = 0.05f;
        private const float StartDuration = 0.12f;
        private const float GaitChangeDuration = 0.2f;

        // 공격 진입은 즉시, 복귀는 클립이 거의 끝날 때
        private const float AttackEnterDuration = 0.05f;
        private const float OneShotExitTime = 0.92f;
        private const float OneShotExitDuration = 0.12f;

        // 눈 발광 (어두운 사냥 시간에도 눈만 보이게)
        private static readonly Color EyeColor = new(1f, 0.28f, 0.12f);
        private const float EyeEmission = 4f;

        /// <summary>
        /// 공격 동작 시작부터 맞는 순간까지의 시간(초). (전환 시간 + 맞는 프레임)
        /// </summary>
        public static float DefaultAttackHitDelay()
        {
            return AttackEnterDuration + AttackHitFrame / ClipFps;
        }

        /// <summary>
        /// 임포트 설정·눈 머티리얼을 맞추고 애니메이터 컨트롤러를 만들어 반환한다. 원본이나 클립이 없으면 null.
        /// </summary>
        public static AnimatorController Prepare()
        {
            if (AssetImporter.GetAtPath(SourcePath) is not ModelImporter importer)
            {
                Debug.LogError($"[GhoulSkinBuilder] 원본 모델을 찾을 수 없습니다: {SourcePath} (generate_ghoul.py를 먼저 실행)");
                return null;
            }

            ConfigureImport(importer);
            RemapEyeMaterial(importer);
            return CreateController();
        }

        /// <summary>
        /// Generic 리그와 자체 Avatar로 임포트하고, 클립 이름·반복 여부를 정한다. 바뀐 것이 있을 때만 다시 임포트한다.
        /// </summary>
        private static void ConfigureImport(ModelImporter importer)
        {
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }
            // 1인칭에서 휘두르는 팔만 남긴 복제 메시를 만들려면 실행 중 메시를 읽을 수 있어야 한다. (FirstPersonBodyView)
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }
            if (changed)
                importer.SaveAndReimport();

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool clipsChanged = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string shortName = ShortName(clip.takeName);
                bool loop = !OneShotClips.Contains(shortName);
                if (clip.name == shortName && clip.loopTime == loop)
                    continue;
                clip.name = shortName;
                clip.loopTime = loop;
                clipsChanged = true;
            }
            if (!clipsChanged)
                return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// "Armature|Ghoul_Idle"처럼 아마추어 이름이 붙은 테이크 이름에서 클립 이름만 남긴다.
        /// </summary>
        private static string ShortName(string takeName)
        {
            int separator = takeName.LastIndexOf('|');
            return separator >= 0 ? takeName.Substring(separator + 1) : takeName;
        }

        /// <summary>
        /// 눈 머티리얼을 URP 발광 머티리얼(Art/Skins/M_GhoulEye)로 바꿔 끼운다. 바뀐 연결이 있을 때만 다시 임포트한다.
        /// </summary>
        private static void RemapEyeMaterial(ModelImporter importer)
        {
            Material eye = new MapMaterialFactory(CharacterAssetBuilder.SkinFolder)
                .Emissive("M_GhoulEye", EyeColor, EyeColor * EyeEmission);
            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), EyeSourceMaterial);
            Dictionary<AssetImporter.SourceAssetIdentifier, Object> current = importer.GetExternalObjectMap();
            if (current.TryGetValue(identifier, out Object mapped) && mapped == eye)
                return;
            importer.AddRemap(identifier, eye);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 구울 애니메이터를 만든다. 같은 경로에 있으면 지우고 새로 만든다.
        /// - 기본 레이어: 대기(기본) ↔ 다가가기 ↔ 기어가기(Speed), 어디서든 Attack·Shriek 트리거 → 끝나면 대기
        /// - 경련 레이어: 덧입히기(Additive)로 경련 클립을 계속 반복
        /// </summary>
        private static AnimatorController CreateController()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string name in new[] { IdleClip, StalkClip, CrawlClip, AttackClip, ShriekClip, TwitchClip })
            {
                AnimationClip clip = FindClip(name);
                if (clip == null)
                {
                    Debug.LogError($"[GhoulSkinBuilder] 클립을 찾을 수 없습니다: {name} ({SourcePath})");
                    return null;
                }
                clips[name] = clip;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(CharacterAnimatorParameters.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimatorParameters.Shriek, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = machine.AddState("Idle");
            idle.motion = clips[IdleClip];
            AnimatorState stalk = machine.AddState("Stalk");
            stalk.motion = clips[StalkClip];
            AnimatorState crawl = machine.AddState("Crawl");
            crawl.motion = clips[CrawlClip];
            machine.defaultState = idle;

            AddSpeedTransition(idle, stalk, AnimatorConditionMode.Greater, StalkSpeed, StartDuration);
            AddSpeedTransition(idle, crawl, AnimatorConditionMode.Greater, CrawlSpeed, GaitChangeDuration);
            AddSpeedTransition(stalk, idle, AnimatorConditionMode.Less, StalkSpeed, StopDuration);
            AddSpeedTransition(stalk, crawl, AnimatorConditionMode.Greater, CrawlSpeed, GaitChangeDuration);
            AddSpeedTransition(crawl, stalk, AnimatorConditionMode.Less, CrawlExitSpeed, GaitChangeDuration);
            AddSpeedTransition(crawl, idle, AnimatorConditionMode.Less, StalkSpeed, StopDuration);

            AddOneShot(machine, idle, "Attack", clips[AttackClip], CharacterAnimatorParameters.Attack);
            AddOneShot(machine, idle, "Shriek", clips[ShriekClip], CharacterAnimatorParameters.Shriek);

            AddTwitchLayer(controller, clips[TwitchClip]);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Speed 조건 하나로 넘어가는 전환을 만든다. (기다리지 않고 바로)
        /// </summary>
        private static void AddSpeedTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.AddCondition(mode, threshold, CharacterAnimatorParameters.Speed);
            transition.hasExitTime = false;
            transition.duration = duration;
        }

        /// <summary>
        /// 어느 상태에서든 트리거로 들어가 한 번 재생하고 대기로 돌아가는 상태를 만든다.
        /// </summary>
        private static void AddOneShot(AnimatorStateMachine machine, AnimatorState idle, string stateName, AnimationClip clip, string trigger)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = AttackEnterDuration;
            enter.canTransitionToSelf = false;

            AnimatorStateTransition exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = OneShotExitTime;
            exit.duration = OneShotExitDuration;
        }

        /// <summary>
        /// 경련 클립을 계속 반복하는 덧입히기 레이어를 더한다. (클립 첫 프레임이 기준이라 평소에는 아무 변화가 없다)
        /// </summary>
        private static void AddTwitchLayer(AnimatorController controller, AnimationClip twitch)
        {
            controller.AddLayer("Twitch");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.blendingMode = AnimatorLayerBlendingMode.Additive;
            layer.defaultWeight = 1f;
            AnimatorState state = layer.stateMachine.AddState("Twitch");
            state.motion = twitch;
            layer.stateMachine.defaultState = state;
            controller.layers = layers;
        }

        /// <summary>
        /// 원본 모델에서 이름이 일치하는 클립을 찾는다.
        /// </summary>
        private static AnimationClip FindClip(string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == clipName);
        }
    }
}
