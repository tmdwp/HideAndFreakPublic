using System.Linq;
using GuardVsMannequin.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>걸으면서 전원 장치를 파괴하고 감정 표현을 하도록 마네킹 전용 애니메이터 컨트롤러를 만든다.</summary>
    public static class MannequinAnimatorBuilder
    {
        private const string AnimationFolder = "Assets/_Project/Animation";
        private const string ControllerPath = CharacterAssetBuilder.SkinFolder + "/AC_MannequinHumanoid.controller";
        private const string UpperBodyMaskPath = CharacterAssetBuilder.SkinFolder + "/AM_UpperBody.mask";

        private const string UpperBodyLayerName = "UpperBody";
        private const float EmoteBlendTime = 0.15f;
        private const float EmoteExitTime = 0.92f;
        private const float InteractBlendTime = 0.25f;

        /// <summary>미끄러지는 감정 표현 자세가 보이지 않도록 이 속도(m/s)를 넘으면 전신 감정 표현을 끊는다.</summary>
        private const float EmoteCancelSpeed = 0.3f;

        /// <summary>감정 표현 하나의 애니메이션 정보.</summary>
        private readonly struct EmoteClip
        {
            public readonly int Id;
            public readonly string FileName;
            public readonly bool UpperBodyOnly;
            public readonly bool Loop;

            /// <summary>감정 표현마다 다른 레이어·반복 설정을 목록 한 줄로 정의할 수 있게 값으로 묶는다.</summary>
            public EmoteClip(int id, string fileName, bool upperBodyOnly = false, bool loop = false)
            {
                Id = id;
                FileName = fileName;
                UpperBodyOnly = upperBodyOnly;
                Loop = loop;
            }

            public string AssetPath => $"{AnimationFolder}/{FileName}.fbx";
            public string ClipName => FileName.Replace(" ", string.Empty);
        }

        /// <summary>감정 표현 번호와 클립으로, 6번은 전원 장치 상호작용에도 쓴다.</summary>
        private static readonly EmoteClip[] Emotes =
        {
            new(1, "Salute"),
            new(2, "Thoughtful Head Shake"),
            new(3, "Talking On Phone"),
            new(4, "Situps"),
            new(5, "Backflip"),
            new(6, "Working On Device", upperBodyOnly: true, loop: true)
        };

        private const int InteractEmoteId = 6;

        /// <summary>이동 블렌드 트리는 다른 외형과 같아야 해서 공용 함수로 만든 뒤 마네킹 전용 동작만 더한다.</summary>
        public static AnimatorController CreateController()
        {
            AnimatorController controller = CharacterAssetBuilder.CreateHumanoidLocomotionController(ControllerPath);
            AnimatorState locomotionState = controller.layers[0].stateMachine.defaultState;

            foreach (EmoteClip emote in Emotes)
                controller.AddParameter(CharacterAnimatorParameters.EmoteTrigger(emote.Id), AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimatorParameters.Interact, AnimatorControllerParameterType.Bool);

            AddFullBodyEmotes(controller, locomotionState);
            AddUpperBodyLayer(controller);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        #region 레이어 구성

        /// <summary>걷는 중에도 바로 감정 표현이 나가도록 Any State에서 들어가고, 움직이면 이동 애니메이션으로 돌아간다.</summary>
        private static void AddFullBodyEmotes(AnimatorController controller, AnimatorState locomotionState)
        {
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorStateMachine emoteStateMachine = rootStateMachine.AddStateMachine("Emotes");

            foreach (EmoteClip emote in Emotes.Where(emoteClip => !emoteClip.UpperBodyOnly))
            {
                AnimationClip clip = ImportClip(emote);
                if (clip == null)
                    continue;

                AnimatorState emoteState = emoteStateMachine.AddState(StateName(emote.Id));
                emoteState.motion = clip;

                AnimatorStateTransition enterTransition = rootStateMachine.AddAnyStateTransition(emoteState);
                ConfigureTrigger(enterTransition, emote.Id);

                AnimatorStateTransition finishTransition = emoteState.AddTransition(locomotionState);
                finishTransition.hasExitTime = true;
                finishTransition.exitTime = EmoteExitTime;
                finishTransition.duration = EmoteBlendTime;

                AnimatorStateTransition cancelTransition = emoteState.AddTransition(locomotionState);
                cancelTransition.hasExitTime = false;
                cancelTransition.duration = EmoteBlendTime;
                cancelTransition.AddCondition(AnimatorConditionMode.Greater, EmoteCancelSpeed, CharacterAnimatorParameters.Speed);
            }
        }

        /// <summary>전원 장치를 파괴하면서 걸을 수 있도록 상호작용은 상체 마스크 레이어에서 재생한다.</summary>
        private static void AddUpperBodyLayer(AnimatorController controller)
        {
            controller.AddLayer(UpperBodyLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = layers.Length - 1;
            layers[layerIndex].avatarMask = CreateUpperBodyMask();
            layers[layerIndex].defaultWeight = 1f;
            layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            AnimatorStateMachine upperStateMachine = controller.layers[layerIndex].stateMachine;
            AnimatorState emptyState = upperStateMachine.AddState("Empty");
            upperStateMachine.defaultState = emptyState;

            EmoteClip interactEmote = Emotes.First(emoteClip => emoteClip.Id == InteractEmoteId);
            AnimationClip interactClip = ImportClip(interactEmote);
            if (interactClip != null)
            {
                AnimatorState interactState = upperStateMachine.AddState("Interact");
                interactState.motion = interactClip;

                AnimatorStateTransition beginTransition = emptyState.AddTransition(interactState);
                beginTransition.hasExitTime = false;
                beginTransition.duration = InteractBlendTime;
                beginTransition.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimatorParameters.Interact);

                AnimatorStateTransition endTransition = interactState.AddTransition(emptyState);
                endTransition.hasExitTime = false;
                endTransition.duration = InteractBlendTime;
                endTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, CharacterAnimatorParameters.Interact);
            }

            foreach (EmoteClip emote in Emotes.Where(emoteClip => emoteClip.UpperBodyOnly))
            {
                AnimationClip clip = ImportClip(emote);
                if (clip == null)
                    continue;

                AnimatorState emoteState = upperStateMachine.AddState(StateName(emote.Id));
                emoteState.motion = clip;

                AnimatorStateTransition enterTransition = upperStateMachine.AddAnyStateTransition(emoteState);
                ConfigureTrigger(enterTransition, emote.Id);

                AnimatorStateTransition finishTransition = emoteState.AddTransition(emptyState);
                finishTransition.hasExitTime = true;
                finishTransition.exitTime = EmoteExitTime;
                finishTransition.duration = InteractBlendTime;
            }
        }

        /// <summary>연타하면 덜덜 떨려 보이므로 자기 자신으로는 전환하지 않는다.</summary>
        private static void ConfigureTrigger(AnimatorStateTransition transition, int emoteId)
        {
            transition.hasExitTime = false;
            transition.duration = EmoteBlendTime;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, CharacterAnimatorParameters.EmoteTrigger(emoteId));
        }

        /// <summary>
        /// 트리거 이름과 같게 해 Animator 창에서 어떤 키가 어떤 상태인지 찾기 쉽게 한다.
        /// </summary>
        private static string StateName(int emoteId) => CharacterAnimatorParameters.EmoteTrigger(emoteId);

        /// <summary>다리와 루트는 Base Layer의 걷기를 따라야 해서 상체만 켠 마스크를 만든다.</summary>
        private static AvatarMask CreateUpperBodyMask()
        {
            AssetDatabase.DeleteAsset(UpperBodyMaskPath);
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            AvatarMaskBodyPart[] upperBodyParts =
            {
                AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK
            };
            foreach (AvatarMaskBodyPart bodyPart in upperBodyParts)
                mask.SetHumanoidBodyPartActive(bodyPart, true);

            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            return mask;
        }

        #endregion

        #region 클립 임포트

        /// <summary>Mixamo FBX는 기본이 Generic이라 리타깃되지 않으므로 휴머노이드로 바꾼다.</summary>
        private static AnimationClip ImportClip(EmoteClip emote)
        {
            if (AssetImporter.GetAtPath(emote.AssetPath) is not ModelImporter importer)
            {
                Debug.LogWarning($"[MannequinAnimatorBuilder] 애니메이션 파일을 찾을 수 없습니다: {emote.AssetPath}");
                return null;
            }

            EnsureHumanoid(importer);
            ConfigureClips(importer, emote);

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(emote.AssetPath).OfType<AnimationClip>()
                .FirstOrDefault(animationClip => !animationClip.name.StartsWith("__preview__"));
            if (clip == null)
                Debug.LogWarning($"[MannequinAnimatorBuilder] 클립이 없습니다: {emote.AssetPath}");
            return clip;
        }

        /// <summary>타입을 바꾸면 클립 기본값이 바뀌므로 클립 설정 전에 바로 다시 임포트한다.</summary>
        private static void EnsureHumanoid(ModelImporter importer)
        {
            if (importer.animationType == ModelImporterAnimationType.Human
                && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
                return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.SaveAndReimport();
        }

        /// <summary>실제 이동은 캐릭터 컨트롤러가 하므로 몸이 캡슐 밖으로 빠져나가지 않도록 클립을 제자리 재생으로 맞춘다.</summary>
        private static void ConfigureClips(ModelImporter importer, EmoteClip emote)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
                return;

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = emote.ClipName;
                clip.loopTime = emote.Loop;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        #endregion
    }
}
