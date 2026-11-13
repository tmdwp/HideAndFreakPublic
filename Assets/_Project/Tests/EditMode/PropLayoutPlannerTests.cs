using System.Collections.Generic;
using GuardVsMannequin.Map;
using NUnit.Framework;
using UnityEngine;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 판마다 섞는 변신 사물 배치가 모든 PC에서 같고, 비슷한 크기끼리만 자리를 바꾸는지 확인한다.
    /// </summary>
    public class PropLayoutPlannerTests
    {
        private const float Tolerance = 1.4f;
        private const float JitterDistance = 0.6f;
        private const float MaxYaw = 180f;

        /// <summary>작은 사물 4개와 큰 사물 3개를 섞어 둬 크기 묶음이 두 개 생기게 한다.</summary>
        private static List<PropLayoutPlanner.Spot> CreateSpots()
        {
            var spots = new List<PropLayoutPlanner.Spot>();
            for (int i = 0; i < 7; i++)
            {
                Vector3 size = i % 2 == 0 ? new Vector3(0.5f, 0.5f, 0.5f) : new Vector3(2f, 1.5f, 1f);
                spots.Add(new PropLayoutPlanner.Spot(i, new Vector3(i * 3f, 0f, 0f), size));
            }
            return spots;
        }

        /// <summary>시드가 같으면 PC마다 같은 배치가 나와야 화면이 일치하므로 결과가 완전히 같은지 확인한다.</summary>
        [Test]
        public void Plan_SameSeed_SameResult()
        {
            List<PropLayoutPlanner.Spot> spots = CreateSpots();

            PropLayoutPlanner.Placement[] first = PropLayoutPlanner.Plan(spots, 42, Tolerance, JitterDistance, MaxYaw);
            PropLayoutPlanner.Placement[] second = PropLayoutPlanner.Plan(spots, 42, Tolerance, JitterDistance, MaxYaw);

            for (int i = 0; i < spots.Count; i++)
            {
                Assert.AreEqual(first[i].spot, second[i].spot);
                Assert.AreEqual(first[i].jitter, second[i].jitter);
                Assert.AreEqual(first[i].yaw, second[i].yaw);
            }
        }

        /// <summary>두 사물이 한 자리로 가지 않도록 자리가 빠짐없이 한 번씩 쓰이는지 확인한다.</summary>
        [Test]
        public void Plan_TargetsArePermutation()
        {
            List<PropLayoutPlanner.Spot> spots = CreateSpots();

            PropLayoutPlanner.Placement[] result = PropLayoutPlanner.Plan(spots, 3, Tolerance, JitterDistance, MaxYaw);

            var used = new HashSet<int>();
            foreach (PropLayoutPlanner.Placement placement in result)
                Assert.IsTrue(used.Add(placement.spot), $"자리 {placement.spot}가 두 번 쓰임");
            Assert.AreEqual(spots.Count, used.Count);
        }

        /// <summary>크기가 크게 다른 사물끼리 바꾸면 겹치거나 떠 보이므로, 여러 시드에서 늘 같은 크기 자리로만 가는지 확인한다.</summary>
        [Test]
        public void Plan_OnlySwapsSimilarSizes()
        {
            List<PropLayoutPlanner.Spot> spots = CreateSpots();

            for (int seed = 0; seed < 20; seed++)
            {
                PropLayoutPlanner.Placement[] result = PropLayoutPlanner.Plan(spots, seed, Tolerance, JitterDistance, MaxYaw);
                for (int i = 0; i < spots.Count; i++)
                    Assert.AreEqual(spots[i].size, spots[result[i].spot].size, $"시드 {seed}, 사물 {i}");
            }
        }

        /// <summary>흩기·회전이 조정 파일에서 정한 범위를 넘지 않고 수평으로만 움직이는지 확인한다.</summary>
        [Test]
        public void Plan_JitterAndYawStayInRange()
        {
            PropLayoutPlanner.Placement[] result = PropLayoutPlanner.Plan(CreateSpots(), 9, Tolerance, JitterDistance, MaxYaw);

            foreach (PropLayoutPlanner.Placement placement in result)
            {
                Assert.LessOrEqual(placement.jitter.magnitude, JitterDistance + 1e-4f);
                Assert.AreEqual(0f, placement.jitter.y);
                Assert.LessOrEqual(Mathf.Abs(placement.yaw), MaxYaw);
            }
        }
    }
}
