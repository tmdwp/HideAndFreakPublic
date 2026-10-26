using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 가드의 총 외형을 관리한다.
    /// - 소유자: 카메라 앞의 1인칭 총을 보여준다. (손에 든 3인칭 총은 그림자만)
    /// - 다른 플레이어: 외형의 손에 3인칭 총을 들려 조준 방향으로 향하게 한다.
    /// 사격 연출은 <see cref="Muzzle"/>(보는 사람 기준 총구)에서 시작한다.
    /// 총 모델 프리팹은 "캐릭터·무기 에셋 생성" 도구가 총구가 +Z를 향하고 Muzzle 자식을 갖도록 만든다.
    ///
    /// 손잡이 지점(<see cref="GunGripSpots"/>)
    /// - Right Hand + Left Hand가 있으면 양손 총: 몸 기준 어깨 높이의 회전 중심에서 일정 거리에 총을 놓고(상하 시선을 따라 돎),
    ///   조준 IK가 두 손을 각 지점에 붙이며 팔꿈치는 몸 바깥·아래를 향하게 한다.
    ///   총 위치는 IK 직전(<see cref="TryGetHandGoals"/>)에 정해 손과 총이 같은 프레임에 맞는다.
    /// - Right Hand(또는 예전 이름 Spot)만 있으면 한 손 총: 애니메이션이 끝난 손 뼈에 그 지점이 오도록 총을 놓는다.
    ///
    /// 위치 조정 (값은 매 프레임 적용되므로 Play 중 바꾸며 맞출 수 있다)
    /// - 1인칭 총: 카메라 기준이라 외형과 무관 → 이 컴포넌트의 값. Play 중 맞춘 뒤 Copy Component → 프리팹에 Paste Component Values.
    /// - 3인칭 총·조준 IK 손 위치: 외형마다 다름 → 외형 에셋(Skin_*.asset)의 Weapon Grip. 에셋이라 Play 중 바꾼 값이 그대로 저장된다.
    /// </summary>
    public class GuardWeaponView : NetworkBehaviour, IHandIKGoalSource, IPerspectiveListener
    {
        private const string MuzzleName = "Muzzle";

        [Tooltip("카메라 피벗 아래의 1인칭 총. 소유자에게만 보인다.")]
        [SerializeField] private Transform firstPersonGun;

        [Tooltip("다른 플레이어에게 보일 3인칭 총 프리팹. (Muzzle 자식 필요, 손잡이는 Right Hand / Left Hand 자식)")]
        [SerializeField] private GameObject thirdPersonGunPrefab;

        [Tooltip("조준 방향 기준. (카메라 피벗, 원격 아바타는 동기화된 상하 시선이 적용된다)")]
        [SerializeField] private Transform aimPivot;

        [Header("1인칭 총 위치 (카메라 기준)")]
        [SerializeField] private Vector3 firstPersonPosition = new Vector3(0.18f, -0.2f, 0.3f);
        [SerializeField] private Vector3 firstPersonRotation = Vector3.zero;
        [SerializeField] private float firstPersonScale = 1f;

        [Header("3인칭 총 위치 (외형이 없을 때만 사용)")]
        [Tooltip("손 위치는 외형마다 다르므로 보통 외형 에셋(Skin_*.asset)의 Weapon Grip을 쓴다. 외형이 없을 때만 이 값을 쓴다.")]
        [SerializeField] private WeaponGrip fallbackGrip = new WeaponGrip();

        private CharacterSkinView skinView;
        private Transform firstPersonMuzzle;
        private Transform thirdPersonGun;
        private Transform thirdPersonMuzzle;
        private Vector3 rightGripPoint;
        private Vector3? leftGripPoint;
        private Transform rightHand;
        private bool hasAimIK;

        // 양손 총을 이번 프레임 IK 직전에 이미 놓았는지. (애니메이터가 꺼져 IK가 불리지 않은 프레임은 LateUpdate에서 놓는다)
        private int twoHandedPlacedFrame = -1;

        /// <summary>보는 사람 기준 총구. (소유자는 1인칭 총, 다른 플레이어는 3인칭 총)</summary>
        public Transform Muzzle => IsOwner || thirdPersonMuzzle == null ? firstPersonMuzzle : thirdPersonMuzzle;

        /// <summary>양손으로 잡는 총인지. (3인칭 총에 Left Hand 지점이 있고 외형이 조준 IK를 쓸 때)</summary>
        private bool IsTwoHanded => thirdPersonGun != null && aimPivot != null && leftGripPoint.HasValue && hasAimIK;

        /// <summary>
        /// 3인칭 총을 만들고, 외형의 조준 IK와 손 뼈를 연결한다.
        /// </summary>
        private void Awake()
        {
            if (firstPersonGun != null)
                firstPersonMuzzle = FindMuzzle(firstPersonGun);

            if (thirdPersonGunPrefab != null)
            {
                // 뼈의 크기·회전에 영향받지 않도록 아바타 루트에 두고 매 프레임 손 위치로 옮긴다.
                thirdPersonGun = Instantiate(thirdPersonGunPrefab, transform, false).transform;
                thirdPersonGun.name = "ThirdPersonGun";
                thirdPersonMuzzle = FindMuzzle(thirdPersonGun);
                rightGripPoint = GunGripSpots.FindLocalPoint(thirdPersonGun, GunGripSpots.RightHand) ?? Vector3.zero;
                leftGripPoint = GunGripSpots.FindLocalPoint(thirdPersonGun, GunGripSpots.LeftHand);
            }

            skinView = GetComponent<CharacterSkinView>();
            if (skinView != null)
            {
                skinView.SetAimSource(aimPivot);
                skinView.SetHandGoalSource(this);
                Animator animator = skinView.Animator;
                if (animator != null && animator.isHuman)
                    rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                hasAimIK = rightHand != null && skinView.ActiveSkin != null && skinView.ActiveSkin.useAimIK;
            }
        }

        /// <summary>
        /// 총 모델의 Muzzle 자식을 찾는다. 없으면 총 자체를 총구로 쓴다.
        /// </summary>
        private static Transform FindMuzzle(Transform gun)
        {
            Transform found = gun.Find(MuzzleName);
            return found != null ? found : gun;
        }

        /// <summary>
        /// 소유자에게는 1인칭 총만, 다른 플레이어에게는 3인칭 총만 보이게 한다.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (firstPersonGun != null)
                firstPersonGun.gameObject.SetActive(IsOwner);

            if (IsOwner && thirdPersonGun != null)
            {
                foreach (Renderer gunRenderer in thirdPersonGun.GetComponentsInChildren<Renderer>(true))
                    gunRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        /// <summary>
        /// 소유자 시점이 바뀌면 1인칭 총과 손에 든 3인칭 총 중 하나만 보이게 한다. (테스트 방 시점 전환)
        /// </summary>
        public void OnPerspectiveChanged(bool firstPerson)
        {
            if (!IsOwner)
                return;

            if (firstPersonGun != null)
                firstPersonGun.gameObject.SetActive(firstPerson);
            if (thirdPersonGun != null)
            {
                ShadowCastingMode mode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                foreach (Renderer gunRenderer in thirdPersonGun.GetComponentsInChildren<Renderer>(true))
                    gunRenderer.shadowCastingMode = mode;
            }
        }

        /// <summary>
        /// 조준 IK 직전에 불린다. 양손 총이면 총을 놓고 두 손잡이 지점과 팔꿈치 방향을 돌려준다.
        /// </summary>
        public bool TryGetHandGoals(out HandIKGoals goals)
        {
            goals = default;
            if (!IsTwoHanded)
                return false;

            WeaponGrip grip = CurrentGrip;
            PlaceTwoHandedGun(grip);
            twoHandedPlacedFrame = Time.frameCount;

            // 팔꿈치 힌트는 상하 시선과 관계없이 몸 방향 기준이라 위아래를 볼 때도 팔이 옆·아래로 굽는다.
            Vector3 pivot = HoldPivot(grip, out Quaternion bodyYaw);
            goals = new HandIKGoals
            {
                RightHand = thirdPersonGun.TransformPoint(rightGripPoint),
                LeftHand = thirdPersonGun.TransformPoint(leftGripPoint.Value),
                RightElbowHint = pivot + bodyYaw * grip.rightElbowHint,
                LeftElbowHint = pivot + bodyYaw * grip.leftElbowHint
            };
            return true;
        }

        /// <summary>
        /// 애니메이션(IK 포함)이 끝난 뒤 총 위치를 적용한다. 인스펙터 값을 매 프레임 반영한다.
        /// </summary>
        private void LateUpdate()
        {
            WeaponGrip grip = CurrentGrip;
            if (skinView != null)
                skinView.SetAimHandOffset(grip.ikHandPosition);

            if (firstPersonGun != null)
            {
                firstPersonGun.localPosition = firstPersonPosition;
                firstPersonGun.localRotation = Quaternion.Euler(firstPersonRotation);
                firstPersonGun.localScale = Vector3.one * firstPersonScale;
            }

            if (thirdPersonGun == null || aimPivot == null)
                return;

            if (IsTwoHanded)
            {
                // 이번 프레임 IK에서 이미 놓았으면 손과 맞춘 그 자리를 유지한다.
                if (twoHandedPlacedFrame != Time.frameCount)
                    PlaceTwoHandedGun(grip);
                return;
            }

            thirdPersonGun.localScale = Vector3.one * grip.gunScale;
            if (rightHand == null)
            {
                // 휴머노이드가 아니면 카메라 피벗 오른쪽 아래에 둔다.
                thirdPersonGun.SetPositionAndRotation(aimPivot.TransformPoint(new Vector3(0.2f, -0.3f, 0.4f)), aimPivot.rotation);
                return;
            }

            // 한 손 총: 회전·크기를 먼저 정한 뒤, 오른손 지점이 손 뼈(+보정)에 오도록 총을 옮긴다.
            Quaternion baseRotation = grip.followHandRotation ? rightHand.rotation : aimPivot.rotation;
            thirdPersonGun.rotation = baseRotation * Quaternion.Euler(grip.handRotationOffset);
            Vector3 handTarget = rightHand.position + baseRotation * grip.handPositionOffset;
            MoveGripPointTo(rightGripPoint, handTarget);
        }

        /// <summary>
        /// 양손 총을 몸 기준 회전 중심(어깨 높이, holdPivot)에 매달아 놓는다.
        /// 두 손 사이 가운데 지점이 회전 중심에서 holdOffset만큼 떨어진 채 상하 시선을 따라 돌아,
        /// 위아래를 봐도 총과 몸통 사이 거리가 일정하다. (상하 각도는 holdPitchLimit까지만 따라간다)
        /// 총구 방향은 제한 없이 조준 방향 + handRotationOffset. (한 손 총 전용 값은 쓰지 않는다)
        /// </summary>
        private void PlaceTwoHandedGun(WeaponGrip grip)
        {
            Vector3 pivot = HoldPivot(grip, out Quaternion bodyYaw);
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, aimPivot.eulerAngles.x), -grip.holdPitchLimit, grip.holdPitchLimit);
            Quaternion holdRotation = bodyYaw * Quaternion.Euler(pitch, 0f, 0f);

            thirdPersonGun.localScale = Vector3.one * grip.gunScale;
            thirdPersonGun.rotation = aimPivot.rotation * Quaternion.Euler(grip.handRotationOffset);
            Vector3 handsCenter = (rightGripPoint + leftGripPoint.Value) * 0.5f;
            MoveGripPointTo(handsCenter, pivot + holdRotation * grip.holdOffset);
        }

        /// <summary>
        /// 양손 총의 회전 중심(월드)과 좌우 시선 방향의 회전. 좌우는 조준 방향을 따르고 상하는 뺀다.
        /// </summary>
        private Vector3 HoldPivot(WeaponGrip grip, out Quaternion bodyYaw)
        {
            bodyYaw = Quaternion.Euler(0f, aimPivot.eulerAngles.y, 0f);
            return transform.position + bodyYaw * grip.holdPivot;
        }

        /// <summary>
        /// 총의 회전·크기는 그대로 두고, 총 루트 기준 지점이 목표 위치에 오도록 총을 옮긴다.
        /// </summary>
        private void MoveGripPointTo(Vector3 gripPoint, Vector3 target)
        {
            Vector3 gripOffset = thirdPersonGun.TransformPoint(gripPoint) - thirdPersonGun.position;
            thirdPersonGun.position = target - gripOffset;
        }

        /// <summary>
        /// 적용된 외형의 손 위치 설정. 외형이 없으면 이 컴포넌트의 예비 설정.
        /// (매 프레임 읽으므로 Play 중 외형 에셋 값을 바꾸면 바로 반영된다)
        /// </summary>
        private WeaponGrip CurrentGrip
        {
            get
            {
                CharacterSkin skin = skinView != null ? skinView.ActiveSkin : null;
                return skin != null && skin.weaponGrip != null ? skin.weaponGrip : fallbackGrip;
            }
        }

        /// <summary>
        /// 선택했을 때 씬 뷰에 총구(빨강)와 손잡이 지점(오른손 하늘색, 왼손 노랑)을 표시한다. (위치 조정용)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if (firstPersonMuzzle != null)
                Gizmos.DrawWireSphere(firstPersonMuzzle.position, 0.02f);
            if (thirdPersonMuzzle != null)
                Gizmos.DrawWireSphere(thirdPersonMuzzle.position, 0.03f);

            if (thirdPersonGun == null)
                return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(thirdPersonGun.TransformPoint(rightGripPoint), 0.03f);
            if (leftGripPoint.HasValue)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(thirdPersonGun.TransformPoint(leftGripPoint.Value), 0.03f);
            }
        }
    }
}
