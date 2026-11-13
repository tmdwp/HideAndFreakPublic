using System.Collections;
using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>폭발 이펙트 프리팹이 없을 때 쓰는 임시 폭발 연출.</summary>
    public class GrenadeBlastAnimator : MonoBehaviour
    {
        private const float PeakIntensity = 12f;

        /// <summary>연출이 스스로 끝나고 지워지도록 코루틴을 이 오브젝트에서 돌린다.</summary>
        public void Run(Light flash, Transform sphere, float radius, float duration)
        {
            StartCoroutine(Animate(flash, sphere, radius, Mathf.Max(0.01f, duration)));
        }

        /// <summary>
        /// 폭발은 처음에 빨리 퍼지고 끝으로 갈수록 느려 보여야 자연스러워서 크기에 제곱근 곡선을 쓴다.
        /// </summary>
        private IEnumerator Animate(Light flash, Transform sphere, float radius, float duration)
        {
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float progress = time / duration;
                sphere.localScale = Vector3.one * (radius * 2f * Mathf.Sqrt(progress));
                flash.intensity = Mathf.Lerp(PeakIntensity, 0f, progress);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
