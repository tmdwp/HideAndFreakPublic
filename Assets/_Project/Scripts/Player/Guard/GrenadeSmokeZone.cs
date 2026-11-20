using System.Collections;
using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>연막이 남아 있는 동안 안에 있는 마네킹에게 수류탄 효과를 계속 다시 거는 서버 전용 판정 영역.</summary>
    public class GrenadeSmokeZone : MonoBehaviour
    {
        private readonly MannequinFinder finder = new();
        private float radius;
        private float duration;
        private float refreshInterval;

        /// <summary>
        /// 던진 가드가 연막 도중에 죽거나 나가도 판정이 끝까지 이어지도록 아바타와 떨어진 오브젝트로 만든다.
        /// </summary>
        public static GrenadeSmokeZone Spawn(Vector3 center)
        {
            var zoneObject = new GameObject("GrenadeSmokeZone");
            zoneObject.transform.position = center;
            GrenadeSmokeZone zone = zoneObject.AddComponent<GrenadeSmokeZone>();
            zone.radius = GameTuning.GrenadeRadius;
            zone.duration = GameTuning.GrenadeSmokeDuration;
            zone.refreshInterval = GameTuning.GrenadeSmokeRefreshInterval;
            zone.StartCoroutine(zone.RefreshWhileActive());
            return zone;
        }

        /// <summary>
        /// 효과를 덮어쓰는 방식이라 간격마다 다시 걸면 연막 안에서는 끊기지 않고, 나간 뒤에는 마지막 효과 시간만큼만 남는다.
        /// </summary>
        private IEnumerator RefreshWhileActive()
        {
            var wait = new WaitForSeconds(refreshInterval);
            for (float elapsed = 0f; elapsed < duration; elapsed += refreshInterval)
            {
                // 판이 끝나 가드 도구를 못 쓰는 단계가 되면 남은 연막도 더 이상 효과를 주지 않는다.
                if (!DamageRules.CanUseGuardTools(GameServices.State))
                    break;
                ApplyToMannequinsInside();
                yield return wait;
            }
            Destroy(gameObject);
        }

        /// <summary>연막은 벽을 넘지 못하므로 벽 너머 마네킹은 반경 안이어도 제외한다.</summary>
        private void ApplyToMannequinsInside()
        {
            foreach (PlayerIdentity mannequin in finder.Find(transform.position, radius, requireLineOfSight: true))
            {
                if (mannequin.TryGetComponent(out StatusEffects effects) && effects.IsServerInitialized)
                    effects.ApplyGrenadeHit();
            }
        }
    }
}
