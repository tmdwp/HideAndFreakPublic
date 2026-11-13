using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Map
{
    /// <summary>모든 PC가 같은 결과를 내도록 장면을 건드리지 않고 값만으로 변신 사물의 새 배치를 계산한다.</summary>
    public static class PropLayoutPlanner
    {
        /// <summary>
        /// 사물 하나의 원래 상태.
        /// </summary>
        public readonly struct Spot
        {
            /// <summary>모든 PC에서 같은 순서로 정렬하려고 쓴다.</summary>
            public readonly int index;
            /// <summary>사물마다 기준점이 달라서, 자리를 바꿀 때는 겉모습 상자의 바닥 가운데 점을 기준으로 쓴다.</summary>
            public readonly Vector3 bottomCenter;
            /// <summary>겉모습 상자 크기.</summary>
            public readonly Vector3 size;

            /// <summary>계산이 장면을 몰라도 되도록 필요한 값만 담는다.</summary>
            public Spot(int index, Vector3 bottomCenter, Vector3 size)
            {
                this.index = index;
                this.bottomCenter = bottomCenter;
                this.size = size;
            }

            /// <summary>회전에 따라 x·z가 바뀌므로 긴 변으로 비교한다.</summary>
            public float Footprint => Mathf.Max(size.x, size.z, 0.01f);

            /// <summary>0으로 나누지 않도록 최소값을 둔다.</summary>
            public float Height => Mathf.Max(size.y, 0.01f);
        }

        /// <summary>사물 하나의 새 배치.</summary>
        public readonly struct Placement
        {
            /// <summary>옮겨 갈 자리(입력 목록의 위치).</summary>
            public readonly int spot;
            /// <summary>그 자리에서 흩을 수평 거리(m).</summary>
            public readonly Vector3 jitter;
            /// <summary>세로축으로 돌릴 각도(도).</summary>
            public readonly float yaw;

            /// <summary>계산 결과를 장면 쪽에 넘기려고 값을 묶는다.</summary>
            public Placement(int spot, Vector3 jitter, float yaw)
            {
                this.spot = spot;
                this.jitter = jitter;
                this.yaw = yaw;
            }
        }

        /// <summary>모든 PC에서 같은 결과를 내도록 장면 검사 없이 시드로만 배치를 정한다.</summary>
        /// <param name="spots">모든 PC에서 같은 순서여야 하는 자리 목록.</param>
        /// <param name="sizeTolerance">자리를 바꿀 수 있는 크기 차이 배율(1 이상).</param>
        /// <param name="jitterDistance">흩는 최대 거리(m).</param>
        /// <param name="maxYaw">돌리는 최대 각도(도, ±).</param>
        public static Placement[] Plan(IReadOnlyList<Spot> spots, int seed, float sizeTolerance, float jitterDistance, float maxYaw)
        {
            // UnityEngine.Random은 다른 코드와 상태를 공유해 PC마다 결과가 달라질 수 있어서 따로 만든다.
            var random = new System.Random(seed);
            var targets = new int[spots.Count];
            foreach (List<int> group in GroupBySize(spots, sizeTolerance))
            {
                // 그룹 안에서 자리를 섞는다.
                var shuffledGroup = new List<int>(group);
                for (int i = shuffledGroup.Count - 1; i > 0; i--)
                {
                    int swapIndex = random.Next(i + 1);
                    (shuffledGroup[i], shuffledGroup[swapIndex]) = (shuffledGroup[swapIndex], shuffledGroup[i]);
                }
                for (int i = 0; i < group.Count; i++)
                    targets[group[i]] = shuffledGroup[i];
            }

            // 흩기 값은 검사 결과와 관계없이 사물마다 같은 순서로 뽑아, 검사 결과가 달라도 다른 사물의 값이 밀리지 않게 한다.
            var result = new Placement[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                // 가운데에 몰리지 않도록 제곱근을 써서 원 안에 고르게 퍼뜨린다.
                double distance = Math.Sqrt(random.NextDouble()) * jitterDistance;
                var jitter = new Vector3((float)(Math.Cos(angle) * distance), 0f, (float)(Math.Sin(angle) * distance));
                float yaw = (float)((random.NextDouble() * 2.0 - 1.0) * maxYaw);
                result[i] = new Placement(targets[i], jitter, yaw);
            }
            return result;
        }

        /// <summary>
        /// 크기가 크게 다른 사물끼리 자리를 바꾸면 겹치거나 떠 보이므로, 크기순으로 늘어놓고 그룹 첫 사물과 tolerance배 이내인 것끼리 묶는다.
        /// </summary>
        private static List<List<int>> GroupBySize(IReadOnlyList<Spot> spots, float tolerance)
        {
            var order = new List<int>();
            for (int i = 0; i < spots.Count; i++)
                order.Add(i);
            // 크기가 같으면 번호순으로 둬 모든 PC에서 순서가 같게 한다.
            order.Sort((first, second) =>
            {
                int bySize = (spots[first].Footprint * spots[first].Height).CompareTo(spots[second].Footprint * spots[second].Height);
                return bySize != 0 ? bySize : spots[first].index.CompareTo(spots[second].index);
            });

            var groups = new List<List<int>>();
            List<int> currentGroup = null;
            foreach (int spotIndex in order)
            {
                if (currentGroup == null || !IsSimilar(spots[currentGroup[0]], spots[spotIndex], tolerance))
                {
                    currentGroup = new List<int>();
                    groups.Add(currentGroup);
                }
                currentGroup.Add(spotIndex);
            }
            return groups;
        }

        /// <summary>
        /// 바닥 넓이만 비슷해도 높이가 크게 다르면 어색하므로 두 값 모두 tolerance배 이내여야 비슷하다고 본다.
        /// </summary>
        private static bool IsSimilar(Spot first, Spot second, float tolerance)
        {
            return Ratio(first.Footprint, second.Footprint) <= tolerance && Ratio(first.Height, second.Height) <= tolerance;
        }

        /// <summary>
        /// 어느 쪽이 큰지와 관계없이 비교하려고 항상 1 이상이 되는 큰 값 ÷ 작은 값을 쓴다.
        /// </summary>
        private static float Ratio(float first, float second)
        {
            return Mathf.Max(first, second) / Mathf.Min(first, second);
        }
    }
}
