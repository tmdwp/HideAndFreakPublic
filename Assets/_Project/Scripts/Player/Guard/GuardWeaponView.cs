using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>소유자와 다른 사람에게 맞는 총이 보이도록 가드의 1인칭·3인칭 총 외형을 관리한다.</summary>
    public class GuardWeaponView : NetworkBehaviour, IHandIKGoalSource, IPerspectiveListener
    {
        private const string MuzzleName = "Muzzle";

        [Tooltip("카메라 피벗 아래의 1인칭 총. 소유자에게만 보인다. (손에 든 총을 쓰면 숨긴다)")]
        [SerializeField] private Transform firstPersonGun;

        [Tooltip("켜면 소유자 1인칭에서도 카메라 앞 총 대신 손에 든 3인칭 총을 보여준다. (1인칭 몸 표시 FirstPersonBodyView와 함께 쓴다)")]
        [SerializeField] private bool useHandGunInFirstPerson = true;

        [Tooltip("다른 플레이어에게 보일 3인칭 총 프리팹. (Muzzle 자식 필요, 손잡이는 Right Hand / Left Hand 자식)")]
        [SerializeField] private GameObject thirdPersonGunPrefab;

        [Tooltip("조준 방향 기준. (카메라 피벗, 원격 아바타는 동기화된 상하 시선이 적용된다)")]
        [SerializeField] private Transform aimPivot;

        [Header("1인칭 총 위치 (카메라 기준)")]
        [SerializeField] private Vector3 firstPersonPosition = new Vector3(0.18f, -0.2f, 0.3f);
        [SerializeField] private Vector3 firstPersonRotation = Vector3.zero;
        [SerializeField] private float firstPersonScale = 1f;

        [Tooltip("몸 흔들림(CharacterSway)을 1인칭 총에 더하는 배율. 카메라가 흔들리는 만큼은 자동으로 따라가고, 그 위에 화면 안에서 보이는 흔들림을 이만큼 더한다. 0이면 화면 안에서 고정.")]
        [SerializeField] private float firstPersonSwayScale = 1f;

        [Header("3인칭 총 위치 (외형이 없을 때만 사용)")]
        [Tooltip("손 위치는 외형마다 다르므로 보통 외형 에셋(Skin_*.asset)의 Weapon Grip을 쓴다. 외형이 없을 때만 이 값을 쓴다.")]
        [SerializeField] private WeaponGrip fallbackGrip = new WeaponGrip();

        private CharacterSkinView skinView;
        private CharacterSway sway;
        private Transform firstPersonMuzzle;
        private Transform thirdPersonGun;
        private Transform thirdPersonMuzzle;
        private Vector3 rightGripPoint;
        private Vector3? leftGripPoint;
        private Transform rightHand;
        private bool hasAimIK;

        // IK 뒤 실제 손과의 차이만큼 총을 옮기려고 기억하는 오른손 목표.
        private Vector3 requestedRightHand;

        // 총을 돌려도 쥔 모양을 유지하려고 기억한 두 손 방향.
        private const int HandRotationCaptureDelay = 10;
        private int twoHandedFrames;
        private Quaternion? rightHandToGun;
        private Quaternion? leftHandToGun;

        // IK가 불리지 않은 프레임에 LateUpdate에서 놓으려고 기억하는 이번 프레임 배치 여부.
        private int twoHandedPlacedFrame = -1;

        // 소유자 화면에 카메라 앞 1인칭 총을 보여 주는 중인지.
        private bool isFirstPersonGunShown;

        /// <summary>사격 연출이 보는 사람 화면의 총에서 나와야 하므로, 지금 보이는 총의 총구를 고른다.</summary>
        public Transform Muzzle => isFirstPersonGunShown || thirdPersonMuzzle == null ? firstPersonMuzzle : thirdPersonMuzzle;

        /// <summary>왼손 지점이 있어도 외형이 조준 IK를 쓰지 않으면 왼손을 붙일 수 없으므로 둘 다 갖춰야 양손으로 본다.</summary>
        private bool IsTwoHanded => thirdPersonGun != null && aimPivot != null && leftGripPoint.HasValue && hasAimIK;

        /// <summary>
        /// 손잡이 지점과 손 뼈는 바뀌지 않으므로 처음에 한 번 찾아 두고, 외형의 조준 IK가 이 총을 목표로 쓰도록 연결한다.
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

            sway = GetComponent<CharacterSway>();
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
        /// Muzzle 자식이 없는 총 모델도 연출이 나오도록 총 자체를 총구로 대신 쓴다.
        /// </summary>
        private static Transform FindMuzzle(Transform gun)
        {
            Transform foundMuzzle = gun.Find(MuzzleName);
            return foundMuzzle != null ? foundMuzzle : gun;
        }

        /// <summary>소유 여부는 스폰 뒤에야 알 수 있어 여기서 정하고, 다른 사람에게는 카메라 앞 총을 끈다.</summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
                ApplyOwnerGunVisibility(true);
            else if (firstPersonGun != null)
                firstPersonGun.gameObject.SetActive(false);
        }

        /// <summary>
        /// 테스트 방 시점 전환 때 3인칭에서 카메라 앞 총이 떠 있지 않도록 시점에 맞는 총으로 바꾼다.
        /// </summary>
        public void OnPerspectiveChanged(bool firstPerson)
        {
            if (IsOwner)
                ApplyOwnerGunVisibility(firstPerson);
        }

        /// <summary>두 총이 겹쳐 보이지 않게 하나만 보이되, 바닥 그림자는 남도록 손에 든 총은 그림자만 그린다.</summary>
        private void ApplyOwnerGunVisibility(bool firstPerson)
        {
            isFirstPersonGunShown = firstPerson && (!useHandGunInFirstPerson || thirdPersonGun == null);
            if (firstPersonGun != null)
                firstPersonGun.gameObject.SetActive(isFirstPersonGunShown);
            if (thirdPersonGun == null)
                return;

            ShadowCastingMode mode = isFirstPersonGunShown ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer gunRenderer in thirdPersonGun.GetComponentsInChildren<Renderer>(true))
                gunRenderer.shadowCastingMode = mode;
        }

        /// <summary>
        /// 총을 IK 뒤에 놓으면 손이 한 프레임 늦게 따라와 떨려 보이므로, IK 직전에 총을 먼저 놓고 그 손잡이를 목표로 준다.
        /// </summary>
        public bool TryGetHandGoals(HandIKPose animatedPose, out HandIKGoals goals)
        {
            goals = default;
            if (!IsTwoHanded)
                return false;

            WeaponGrip grip = CurrentGrip;
            PlaceTwoHandedGun(grip);
            twoHandedPlacedFrame = Time.frameCount;
            CaptureHandRotations(grip, animatedPose);

            // 팔꿈치 힌트는 상하 시선과 관계없이 몸 방향 기준이라 위아래를 볼 때도 팔이 옆·아래로 굽는다.
            Vector3 pivot = HoldPivot(grip, out Quaternion bodyYaw);
            requestedRightHand = thirdPersonGun.TransformPoint(rightGripPoint);
            goals = new HandIKGoals
            {
                RightHand = requestedRightHand,
                LeftHand = thirdPersonGun.TransformPoint(leftGripPoint.Value),
                RightHandRotation = HandRotationOnGun(grip, rightHandToGun, grip.rightHandRotationOffset),
                LeftHandRotation = HandRotationOnGun(grip, leftHandToGun, grip.leftHandRotationOffset),
                RightElbowHint = pivot + bodyYaw * grip.rightElbowHint,
                LeftElbowHint = pivot + bodyYaw * grip.leftElbowHint
            };
            return true;
        }

        /// <summary>총을 위아래로 돌려도 쥔 모양을 유지하도록 기억한 손 방향을 지금 총 방향에 얹는다.</summary>
        private Quaternion? HandRotationOnGun(WeaponGrip grip, Quaternion? handToGun, Vector3 offset)
        {
            if (!grip.lockHandRotation || !handToGun.HasValue)
                return null;
            return thirdPersonGun.rotation * handToGun.Value * Quaternion.Euler(offset);
        }

        /// <summary>
        /// 손 뼈 Transform 방향은 IK 목표와 축이 달라 그대로 넣으면 손이 꺾이므로, IK 목표 방향을 총 기준으로 한 번 기억한다.
        /// </summary>
        private void CaptureHandRotations(WeaponGrip grip, HandIKPose animatedPose)
        {
            if (!grip.lockHandRotation || rightHandToGun.HasValue)
                return;
            // 스폰 직후에는 기본 자세(T자)일 수 있어, 애니메이션이 자리 잡은 뒤의 손 방향을 기억한다.
            if (++twoHandedFrames < HandRotationCaptureDelay)
                return;

            Quaternion toGun = Quaternion.Inverse(thirdPersonGun.rotation);
            rightHandToGun = toGun * animatedPose.RightHandRotation;
            leftHandToGun = toGun * animatedPose.LeftHandRotation;
        }

        /// <summary>팔이 모자라 총과 손이 떨어져 보이지 않도록 그 차이만큼 총을 손 쪽으로 옮긴다.</summary>
        private void AttachGunToHands()
        {
            thirdPersonGun.position += rightHand.position - requestedRightHand;
        }

        /// <summary>IK가 끝난 손 위치에 맞추도록 LateUpdate에서 총을 놓는다.</summary>
        private void LateUpdate()
        {
            WeaponGrip grip = CurrentGrip;
            if (skinView != null)
                skinView.SetAimHandOffset(grip.ikHandPosition);

            if (firstPersonGun != null)
            {
                // 화면 안에서도 총이 출렁이도록 카메라 배율보다 firstPersonSwayScale만큼 더 움직인다.
                Vector3 swayLocal = firstPersonGun.parent != null ? firstPersonGun.parent.InverseTransformVector(BodySway) : BodySway;
                float cameraScale = sway != null ? sway.CameraSwayScale : 0f;
                firstPersonGun.localPosition = firstPersonPosition + swayLocal * (cameraScale + firstPersonSwayScale);
                firstPersonGun.localRotation = Quaternion.Euler(firstPersonRotation);
                firstPersonGun.localScale = Vector3.one * firstPersonScale;
            }

            if (thirdPersonGun == null || aimPivot == null)
                return;

            if (IsTwoHanded)
            {
                // IK에서 놓았으면 IK가 끝난 손에 총을 붙인다.
                if (twoHandedPlacedFrame == Time.frameCount)
                    AttachGunToHands();
                else
                    PlaceTwoHandedGun(grip);
                return;
            }

            thirdPersonGun.localScale = Vector3.one * grip.gunScale;
            if (rightHand == null)
            {
                // 휴머노이드가 아니면 손 뼈를 알 수 없으므로 카메라 피벗 오른쪽 아래에 둔다.
                thirdPersonGun.SetPositionAndRotation(aimPivot.TransformPoint(new Vector3(0.2f, -0.3f, 0.4f)), aimPivot.rotation);
                return;
            }

            // 한 손 총: 회전·크기를 먼저 정한 뒤, 오른손 지점이 손 뼈(+보정)에 오도록 총을 옮긴다.
            Quaternion baseRotation = grip.followHandRotation ? rightHand.rotation : aimPivot.rotation;
            thirdPersonGun.rotation = baseRotation * Quaternion.Euler(grip.handRotationOffset);
            Vector3 handTarget = rightHand.position + baseRotation * grip.handPositionOffset;
            MoveGripPointTo(rightGripPoint, handTarget);
        }

        /// <summary>위아래를 볼 때 총이 몸에 박히지 않도록 손 뼈 대신 어깨 높이 회전 중심에 매단다.</summary>
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

        /// <summary>팔꿈치 힌트와 총 위치가 같은 기준을 쓰도록 회전 중심과 좌우 회전을 한곳에서 계산한다.</summary>
        private Vector3 HoldPivot(WeaponGrip grip, out Quaternion bodyYaw)
        {
            bodyYaw = Quaternion.Euler(0f, aimPivot.eulerAngles.y, 0f);
            return transform.position + bodyYaw * grip.holdPivot + BodySway + UpperBodySway;
        }

        /// <summary>몸 흔들림(월드 기준).</summary>
        private Vector3 BodySway => sway != null ? sway.WorldOffset : Vector3.zero;

        /// <summary>BodySway와 더해 쓰는 골반을 뺀 상체 흔들림.</summary>
        private Vector3 UpperBodySway => sway != null ? sway.UpperBodyWorldOffset : Vector3.zero;

        /// <summary>
        /// 손잡이 지점은 총 루트가 아닌 곳에 있으므로, 회전·크기를 정한 뒤 그 지점이 목표에 오도록 총 루트를 옮긴다.
        /// </summary>
        private void MoveGripPointTo(Vector3 gripPoint, Vector3 target)
        {
            Vector3 gripOffset = thirdPersonGun.TransformPoint(gripPoint) - thirdPersonGun.position;
            thirdPersonGun.position = target - gripOffset;
        }

        /// <summary>손 위치는 외형마다 달라 외형 에셋 값을 쓰고, 외형이 없을 때만 예비 설정을 쓴다.</summary>
        private WeaponGrip CurrentGrip
        {
            get
            {
                CharacterSkin skin = skinView != null ? skinView.ActiveSkin : null;
                return skin != null && skin.weaponGrip != null ? skin.weaponGrip : fallbackGrip;
            }
        }

        /// <summary>위치를 조정할 때 손잡이·총구가 보이도록 선택하면 씬 뷰에 표시한다.</summary>
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
