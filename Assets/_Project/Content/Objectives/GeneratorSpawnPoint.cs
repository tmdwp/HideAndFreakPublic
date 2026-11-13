using UnityEngine;

namespace GuardVsMannequin.Objectives
{
    /// <summary>판마다 전원 장치 위치가 바뀌도록 후보를 여러 개 두면 서로 먼 후보가 골라진다.</summary>
    public class GeneratorSpawnPoint : MonoBehaviour
    {
        private static readonly Vector3 GeneratorSize = new Vector3(1f, 1.6f, 0.8f);

        /// <summary>
        /// 빈 오브젝트라 씬 뷰에서 보이지 않으므로, 실제 장치가 벽에 박히는지 확인할 수 있게 장치 크기 상자와 방향, 이름표를 그린다.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * GeneratorSize.y * 0.5f, transform.rotation, Vector3.one);
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
            Gizmos.DrawCube(Vector3.zero, GeneratorSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Vector3.zero, GeneratorSize);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward);
            Gizmos.matrix = Matrix4x4.identity;

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * (GeneratorSize.y + 0.3f), "전원 장치 후보");
#endif
        }
    }
}
