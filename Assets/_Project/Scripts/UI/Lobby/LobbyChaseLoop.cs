using GuardVsMannequin.Player;
using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>로비 배경에서 가드와 구울이 거리를 좁혔다 벌렸다 하며 경로를 계속 돈다.</summary>
    public class LobbyChaseLoop : MonoBehaviour
    {
        [Tooltip("두 캐릭터가 따라 도는 경로.")]
        [SerializeField] private LobbyChasePath path;

        [Tooltip("도망치는 쪽(가드).")]
        [SerializeField] private Transform runner;

        [Tooltip("쫓는 쪽(구울).")]
        [SerializeField] private Transform chaser;

        [Tooltip("쫓는 쪽 외형. 할퀴기 트리거와 애니메이션 속도에 쓴다.")]
        [SerializeField] private CharacterSkinView chaserSkin;

        [Tooltip("이동 속도(m/s). 가드 애니메이션은 1.6 걷기 ~ 4.5 달리기 사이에서 섞인다.")]
        [SerializeField] private float runSpeed = 3.8f;

        [Tooltip("평소 두 캐릭터 사이 거리(m).")]
        [SerializeField] private float baseGap = 3.2f;

        [Tooltip("거리가 좁혀졌다 벌어지는 폭(m). 평소 거리 ± 이 값.")]
        [SerializeField] private float gapSwing = 1.4f;

        [Tooltip("거리가 좁혀졌다 벌어지는 한 주기(초).")]
        [SerializeField] private float gapPeriod = 7f;

        [Tooltip("이 거리(m)보다 가까워지면 할퀴기를 한다.")]
        [SerializeField] private float attackDistance = 2.1f;

        [Tooltip("할퀴기 사이 최소 간격(초).")]
        [SerializeField] private float attackCooldown = 2.5f;

        [Tooltip("쫓는 쪽 애니메이션 재생 속도 배율. 발이 미끄러져 보이면 조절한다.")]
        [SerializeField] private float chaserAnimationSpeed = 1.2f;

        private float travelledDistance;
        private float elapsedTime;
        private float nextAttackTime;

        /// <summary>
        /// 외형은 위치 변화로 이동 속도를 재므로, 첫 프레임에 원래 자리에서 경로로 순간 이동해 속도가 튀지 않도록 Awake에서 미리 놓는다.
        /// </summary>
        private void Awake()
        {
            Place();
        }

        /// <summary>
        /// 외형의 Animator는 각자의 Awake에서 붙으므로 그 뒤인 Start에서 재생 속도를 맞춘다.
        /// </summary>
        private void Start()
        {
            if (chaserSkin != null && chaserSkin.Animator != null)
                chaserSkin.Animator.speed = chaserAnimationSpeed;
        }

        /// <summary>
        /// 거리 간격과 할퀴기가 같은 시간축을 쓰도록 이동 거리와 경과 시간을 함께 늘린다.
        /// </summary>
        private void Update()
        {
            travelledDistance += runSpeed * Time.deltaTime;
            elapsedTime += Time.deltaTime;
            Place();
            TryAttack();
        }

        /// <summary>연출이 단조롭지 않도록 거리를 사인파로 오르내리게 한다.</summary>
        private float CurrentGap()
        {
            float wave = gapPeriod > 0f ? Mathf.Sin(elapsedTime * Mathf.PI * 2f / gapPeriod) : 0f;
            return Mathf.Max(0.5f, baseGap + gapSwing * wave);
        }

        /// <summary>
        /// 두 캐릭터가 같은 경로를 쓰도록 구울은 가드 위치에서 거리만큼 뒤를 물어본다.
        /// </summary>
        private void Place()
        {
            if (path == null)
                return;
            SetPose(runner, path.Evaluate(travelledDistance));
            SetPose(chaser, path.Evaluate(travelledDistance - CurrentGap()));
        }

        /// <summary>
        /// 인스펙터에서 한쪽 캐릭터를 비워 둬도 오류 없이 동작하도록 확인한다.
        /// </summary>
        private static void SetPose(Transform target, Pose pose)
        {
            if (target != null)
                target.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>할퀴기를 연달아 하면 어색하므로 성공했을 때만 대기 시간을 건다.</summary>
        private void TryAttack()
        {
            if (chaserSkin == null || elapsedTime < nextAttackTime || CurrentGap() > attackDistance)
                return;
            if (chaserSkin.TryPlayTrigger(CharacterAnimatorParameters.Attack))
                nextAttackTime = elapsedTime + attackCooldown;
        }
    }
}
