using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>역할별 스폰 후보 위치이며, 가드는 판마다 마네킹에서 먼 후보가 골라진다.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private PlayerRole role = PlayerRole.Mannequin;

        public PlayerRole Role => role;

        /// <summary>
        /// 빈 오브젝트라 씬 뷰에서 보이지 않으므로, 맵을 만들 때 역할과 바라보는 방향을 확인할 수 있게 그린다.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = role == PlayerRole.Guard ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
        }
    }
}
