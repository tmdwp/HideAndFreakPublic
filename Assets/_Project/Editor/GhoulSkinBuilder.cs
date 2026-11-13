using System.Collections.Generic;
using System.Linq;
using GuardVsMannequin.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>사냥 시간 구울 외형의 임포트 설정과 전용 애니메이터를 준비한다.</summary>
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

        // 한 번만 재생하는 클립(나머지는 반복).
        private static readonly HashSet<string> OneShotClips = new() { AttackClip, ShriekClip };

        // 공격 지연을 맞추려고 generate_ghoul.py의 FPS와 맞는 프레임을 옮겨 둔다.
        private const float ClipFps = 24f;
        private const int AttackHitFrame = 11;

        // 이동 속도(m/s) 기준.
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

        // 어두운 사냥 시간에도 눈만 보이게 하는 발광.
        private static readonly Color EyeColor = new(1f, 0.28f, 0.12f);
        private const float EyeEmission = 4f;

        /// <summary>피해 판정이 손이 닿는 순간과 맞도록 맞는 프레임으로 공격 지연을 정한다.</summary>
        public static float DefaultAttackHitDelay()
        {
            return AttackEnterDuration + AttackHitFrame / ClipFps;
        }

        /// <summary>모델을 다시 만들면 임포트 설정이 초기화될 수 있어 매번 맞춘 뒤 컨트롤러를 만든다.</summary>
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

        /// <summary>사람 뼈 구조가 아니라 Generic으로 임포트하고, 코드가 테이크 이름에 의존하지 않도록 클립 이름을 짧게 바꾼다.</summary>
        private static void ConfigureImport(ModelImporter importer)
        {
            bool isChanged = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                isChanged = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                isChanged = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                isChanged = true;
            }
            // 1인칭 팔 복제 메시를 만들려면 실행 중 메시를 읽을 수 있어야 한다.
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                isChanged = true;
            }
            if (isChanged)
                importer.SaveAndReimport();

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            bool isClipChanged = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string shortName = ShortName(clip.takeName);
                bool isLooping = !OneShotClips.Contains(shortName);
                if (clip.name == shortName && clip.loopTime == isLooping)
                    continue;
                clip.name = shortName;
                clip.loopTime = isLooping;
                isClipChanged = true;
            }
            if (!isClipChanged)
                return;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        /// <summary>아마추어 이름이 바뀌어도 클립 이름이 같도록 "Armature|" 뒷부분만 남긴다.</summary>
        private static string ShortName(string takeName)
        {
            int separatorIndex = takeName.LastIndexOf('|');
            return separatorIndex >= 0 ? takeName.Substring(separatorIndex + 1) : takeName;
        }

        /// <summary>어두운 사냥 시간에 눈만 보여야 공포감이 살아서 눈 머티리얼만 발광 머티리얼로 바꿔 끼운다.</summary>
        private static void RemapEyeMaterial(ModelImporter importer)
        {
            Material eyeMaterial = new MapMaterialFactory(CharacterAssetBuilder.SkinFolder)
                .Emissive("M_GhoulEye", EyeColor, EyeColor * EyeEmission);
            var sourceIdentifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), EyeSourceMaterial);
            Dictionary<AssetImporter.SourceAssetIdentifier, Object> currentMap = importer.GetExternalObjectMap();
            if (currentMap.TryGetValue(sourceIdentifier, out Object mappedObject) && mappedObject == eyeMaterial)
                return;
            importer.AddRemap(sourceIdentifier, eyeMaterial);
            importer.SaveAndReimport();
        }

        /// <summary>손으로 고친 전환이 코드 설정과 섞이지 않도록 컨트롤러를 지우고 새로 만든다.</summary>
        private static AnimatorController CreateController()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string clipName in new[] { IdleClip, StalkClip, CrawlClip, AttackClip, ShriekClip, TwitchClip })
            {
                AnimationClip clip = FindClip(clipName);
                if (clip == null)
                {
                    Debug.LogError($"[GhoulSkinBuilder] 클립을 찾을 수 없습니다: {clipName} ({SourcePath})");
                    return null;
                }
                clips[clipName] = clip;
            }

            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(CharacterAnimatorParameters.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimatorParameters.Attack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimatorParameters.Shriek, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = clips[IdleClip];
            AnimatorState stalkState = stateMachine.AddState("Stalk");
            stalkState.motion = clips[StalkClip];
            AnimatorState crawlState = stateMachine.AddState("Crawl");
            crawlState.motion = clips[CrawlClip];
            stateMachine.defaultState = idleState;

            AddSpeedTransition(idleState, stalkState, AnimatorConditionMode.Greater, StalkSpeed, StartDuration);
            AddSpeedTransition(idleState, crawlState, AnimatorConditionMode.Greater, CrawlSpeed, GaitChangeDuration);
            AddSpeedTransition(stalkState, idleState, AnimatorConditionMode.Less, StalkSpeed, StopDuration);
            AddSpeedTransition(stalkState, crawlState, AnimatorConditionMode.Greater, CrawlSpeed, GaitChangeDuration);
            AddSpeedTransition(crawlState, stalkState, AnimatorConditionMode.Less, CrawlExitSpeed, GaitChangeDuration);
            AddSpeedTransition(crawlState, idleState, AnimatorConditionMode.Less, StalkSpeed, StopDuration);

            AddOneShot(stateMachine, idleState, "Attack", clips[AttackClip], CharacterAnimatorParameters.Attack);
            AddOneShot(stateMachine, idleState, "Shriek", clips[ShriekClip], CharacterAnimatorParameters.Shriek);

            AddTwitchLayer(controller, clips[TwitchClip]);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// 걸음 전환이 클립 끝을 기다리면 반응이 늦어 보이므로 종료 시간 없이 Speed 조건만으로 바로 넘어가게 한다.
        /// </summary>
        private static void AddSpeedTransition(AnimatorState fromState, AnimatorState toState, AnimatorConditionMode mode, float threshold, float duration)
        {
            AnimatorStateTransition transition = fromState.AddTransition(toState);
            transition.AddCondition(mode, threshold, CharacterAnimatorParameters.Speed);
            transition.hasExitTime = false;
            transition.duration = duration;
        }

        /// <summary>공격은 걷든 기든 바로 나가도록 Any State에서 들어가고, 연타해도 처음부터 다시 시작하지 않게 한다.</summary>
        private static void AddOneShot(AnimatorStateMachine stateMachine, AnimatorState idleState, string stateName, AnimationClip clip, string trigger)
        {
            AnimatorState oneShotState = stateMachine.AddState(stateName);
            oneShotState.motion = clip;

            AnimatorStateTransition enterTransition = stateMachine.AddAnyStateTransition(oneShotState);
            enterTransition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enterTransition.hasExitTime = false;
            enterTransition.duration = AttackEnterDuration;
            enterTransition.canTransitionToSelf = false;

            AnimatorStateTransition exitTransition = oneShotState.AddTransition(idleState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = OneShotExitTime;
            exitTransition.duration = OneShotExitDuration;
        }

        /// <summary>어떤 동작 중에도 경련이 섞여 보이도록 덧입히기 레이어로 계속 반복한다.</summary>
        private static void AddTwitchLayer(AnimatorController controller, AnimationClip twitchClip)
        {
            controller.AddLayer("Twitch");
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer twitchLayer = layers[layers.Length - 1];
            twitchLayer.blendingMode = AnimatorLayerBlendingMode.Additive;
            twitchLayer.defaultWeight = 1f;
            AnimatorState twitchState = twitchLayer.stateMachine.AddState("Twitch");
            twitchState.motion = twitchClip;
            twitchLayer.stateMachine.defaultState = twitchState;
            controller.layers = layers;
        }

        /// <summary>
        /// FBX 안에는 클립 외에도 미리보기용 클립 등이 섞여 있어 이름이 정확히 같은 것만 고른다.
        /// </summary>
        private static AnimationClip FindClip(string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == clipName);
        }
    }
}
