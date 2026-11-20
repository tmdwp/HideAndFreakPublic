using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>맵마다 외형을 바꿀 수 있도록 모델과 애니메이터를 묶은 외형 하나다.</summary>
    [CreateAssetMenu(menuName = "GameSettingTools/Character Skin", fileName = "Skin_")]
    public class CharacterSkin : ScriptableObject
    {
        [Tooltip("표시 이름. (인스펙터 확인용)")]
        public string displayName;

        [Tooltip("외형 모델 프리팹. 발이 원점, 정면이 +Z여야 한다.")]
        public GameObject modelPrefab;

        [Tooltip("모델에 적용할 애니메이터 컨트롤러. 비어 있으면 모델에 있던 컨트롤러를 그대로 쓴다.")]
        public RuntimeAnimatorController animatorController;

        [Tooltip("모델의 정면이 +Z가 아닐 때 보정할 회전(도).")]
        public float yawOffset;

        [Tooltip("오른손이 조준 방향을 향하도록 IK를 쓸지 여부. (총을 든 가드)")]
        public bool useAimIK;

        [Tooltip("애니메이션 이벤트(클립 안에서 함수를 부르는 표시)를 실행할지 여부.\n" +
                 "에셋 팩 애니메이션에는 팩 전용 스크립트를 부르는 이벤트가 들어 있는 경우가 많아, 받을 곳이 없으면 오류가 난다. " +
                 "이벤트를 받는 컴포넌트를 직접 만들었을 때만 켠다.")]
        public bool fireAnimationEvents;

        [Tooltip("제자리 애니메이션으로 고정할지 여부. (휴머노이드가 아닌 모델에만 적용)\n" +
                 "일반(Generic) 모델의 이동 클립은 뼈 자체가 앞으로 나아갔다가 반복될 때 제자리로 돌아온다. " +
                 "실제 이동은 캐릭터가 하므로, 뿌리 뼈의 수평 이동을 매 프레임 지워 제자리에서 걷게 한다.")]
        public bool keepRootBoneInPlace = true;

        [Tooltip("공격 동작(Attack 트리거)을 시작한 뒤 실제로 맞는 순간까지의 시간(초). 이 시점에 피해 판정을 한다.\n" +
                 "0이면 누르자마자 판정한다. Play 중 이 에셋의 값을 바꾸면 바로 반영된다.")]
        [Min(0f)]
        public float attackHitDelay;

        [Tooltip("도약 공격 동작(Lunge 트리거)을 시작한 뒤 덮쳐 맞히는 순간까지의 시간(초).\n" +
                 "0이면 도약 동작이 없는 외형으로 보고 attackHitDelay를 쓴다.")]
        [Min(0f)]
        public float lungeHitDelay;

        [Tooltip("총을 드는 외형(가드)의 손 위치 설정. 모델마다 손 크기·뼈 방향이 달라 외형별로 둔다.\n" +
                 "Play 중 이 에셋의 값을 바꾸면 바로 반영되고, Play를 꺼도 값이 유지된다.")]
        public WeaponGrip weaponGrip = new WeaponGrip();

        [Tooltip("가드 외형의 손전등 모양. 위치는 모델 안 \"Flashlight\" 이름의 빈 오브젝트를 따르고(없으면 기본 위치),\n" +
                 "비추는 방향은 항상 시선을 따른다. 밝기·범위·각도는 공정하도록 게임이 정한다.")]
        public FlashlightLook flashlight = new FlashlightLook();
    }

    /// <summary>외형마다 달라도 공정성에 영향이 없는 손전등의 위치·색.</summary>
    [System.Serializable]
    public class FlashlightLook
    {
        /// <summary>모델 안에서 손전등 위치로 쓸 빈 오브젝트 이름.</summary>
        public const string MountPointName = "Flashlight";

        [Tooltip("손전등 빛 색. 어두운 색으로 덜 보이게 하는 것을 막으려고 밝기는 게임이 맞추고 색감만 쓴다.")]
        public Color color = new Color(1f, 0.95f, 0.85f, 1f);

        /// <summary>
        /// 어두운 색을 골라 빛이 약해지거나 남에게 덜 보이는 이점이 없도록, 가장 밝은 채널이 1이 되게 밝기를 맞춘 색.
        /// </summary>
        public Color GetNormalizedColor()
        {
            float brightest = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (brightest <= 0.001f)
                return Color.white;
            return new Color(color.r / brightest, color.g / brightest, color.b / brightest, 1f);
        }
    }

    /// <summary>3인칭 총을 손에 들리는 위치와 조준 IK 손 위치.</summary>
    [System.Serializable]
    public class WeaponGrip
    {
        [Tooltip("(한 손 총만) 켜면 손 뼈의 회전을 따르고, 끄면 조준 방향을 향한다.")]
        public bool followHandRotation;

        [Tooltip("(한 손 총만) 손 위치에서의 총 위치 보정. 조준 방향(또는 손 뼈) 좌표.")]
        public Vector3 handPositionOffset = new Vector3(0f, 0.02f, 0.06f);

        [Tooltip("총 회전 보정(도). 양손 총은 조준 방향 기준으로 적용된다.")]
        public Vector3 handRotationOffset = Vector3.zero;

        [Tooltip("3인칭 총 크기 배율.")]
        public float gunScale = 1f;

        [Tooltip("(한 손 총만) 조준 IK: 카메라 피벗 기준 오른손 위치.")]
        public Vector3 ikHandPosition = new Vector3(0.18f, -0.15f, 0.45f);

        [Header("양손 총 (총에 Right Hand + Left Hand 지점이 있을 때)")]
        [Tooltip("총이 위아래로 돌 때의 회전 중심. 캐릭터 발 기준 좌표(어깨 높이 근처).")]
        public Vector3 holdPivot = new Vector3(0.05f, 1.4f, 0.05f);

        [Tooltip("회전 중심에서 두 손 사이 가운데 지점까지의 거리·방향. 조준 방향 기준 좌표. 이 거리를 유지한 채 위아래로 돈다.")]
        public Vector3 holdOffset = new Vector3(0.1f, -0.1f, 0.4f);

        [Tooltip("총이 따라 도는 상하 각도 제한(도). 이보다 더 보면 총구 방향만 따라가고 위치는 멈춰 팔이 지나치게 뻗지 않는다.")]
        [Range(0f, 90f)]
        public float holdPitchLimit = 60f;

        [Tooltip("오른팔꿈치가 향할 위치. 회전 중심 기준, 몸 방향 좌표. (바깥·아래로 둘수록 팔이 옆으로 굽는다)")]
        public Vector3 rightElbowHint = new Vector3(0.35f, -0.35f, -0.1f);

        [Tooltip("왼팔꿈치가 향할 위치. 회전 중심 기준, 몸 방향 좌표.")]
        public Vector3 leftElbowHint = new Vector3(-0.3f, -0.4f, 0.15f);

        [Header("손과 총 연결 (양손 총)")]
        [Tooltip("켜면 두 손의 방향도 총에 고정한다. 총을 처음 잡고 잠시 뒤의 손 방향을 총 기준으로 기억해 계속 유지한다. (끄면 손 방향은 애니메이션 그대로)")]
        public bool lockHandRotation = true;

        [Tooltip("기억한 오른손 방향에 더할 보정(도). 총 기준.")]
        public Vector3 rightHandRotationOffset = Vector3.zero;

        [Tooltip("기억한 왼손 방향에 더할 보정(도). 총 기준.")]
        public Vector3 leftHandRotationOffset = Vector3.zero;
    }
}
