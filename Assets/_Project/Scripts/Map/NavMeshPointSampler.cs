using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace GuardVsMannequin.Map
{
    /// <summary>가드 스폰 후보가 넓은 바닥에 고르게 퍼지도록 NavMesh 위의 무작위 지점을 뽑는다.</summary>
    public static class NavMeshPointSampler
    {
        /// <summary>잘게 나뉜 좁은 곳에 몰리지 않도록 면적 누적합으로 삼각형을 면적에 비례해 고른다.</summary>
        /// <param name="center">맵 중심을 따로 몰라도 되도록 주는 뽑은 지점들의 평균.</param>
        public static List<Vector3> Sample(int count, out Vector3 center)
        {
            var result = new List<Vector3>(count);
            center = Vector3.zero;

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int triangleCount = triangulation.indices.Length / 3;
            if (triangleCount == 0)
                return result;

            var cumulativeArea = new float[triangleCount];
            float totalArea = 0f;
            for (int i = 0; i < triangleCount; i++)
            {
                GetTriangle(triangulation, i, out Vector3 vertexA, out Vector3 vertexB, out Vector3 vertexC);
                totalArea += Vector3.Cross(vertexB - vertexA, vertexC - vertexA).magnitude * 0.5f;
                cumulativeArea[i] = totalArea;
            }

            for (int sampleIndex = 0; sampleIndex < count; sampleIndex++)
            {
                int triangleIndex = System.Array.BinarySearch(cumulativeArea, Random.value * totalArea);
                if (triangleIndex < 0)
                    triangleIndex = Mathf.Min(~triangleIndex, triangleCount - 1);

                GetTriangle(triangulation, triangleIndex, out Vector3 vertexA, out Vector3 vertexB, out Vector3 vertexC);
                Vector3 point = RandomPointInTriangle(vertexA, vertexB, vertexC);
                result.Add(point);
                center += point;
            }

            center /= result.Count;
            return result;
        }

        /// <summary>
        /// 인덱스 배열을 세 개씩 읽는 계산을 여러 곳에서 반복하지 않도록 한곳에 둔다.
        /// </summary>
        private static void GetTriangle(NavMeshTriangulation triangulation, int triangleIndex, out Vector3 vertexA, out Vector3 vertexB, out Vector3 vertexC)
        {
            vertexA = triangulation.vertices[triangulation.indices[triangleIndex * 3]];
            vertexB = triangulation.vertices[triangulation.indices[triangleIndex * 3 + 1]];
            vertexC = triangulation.vertices[triangulation.indices[triangleIndex * 3 + 2]];
        }

        /// <summary>
        /// 두 난수를 그대로 쓰면 평행사변형에 퍼지므로, 합이 1을 넘으면 뒤집어 삼각형 안에 고르게 들어오게 한다.
        /// </summary>
        private static Vector3 RandomPointInTriangle(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
        {
            float u = Random.value;
            float v = Random.value;
            if (u + v > 1f)
            {
                u = 1f - u;
                v = 1f - v;
            }
            return vertexA + (vertexB - vertexA) * u + (vertexC - vertexA) * v;
        }
    }
}
