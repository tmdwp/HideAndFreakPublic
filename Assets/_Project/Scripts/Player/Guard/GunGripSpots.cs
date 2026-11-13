using UnityEngine;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>총 프리팹에서 손이 잡는 지점을 찾는 규칙.</summary>
    public static class GunGripSpots
    {
        public const string RightHand = "Right Hand";
        public const string LeftHand = "Left Hand";
        public const string LegacyRightHand = "Spot";

        /// <summary>예전 프리팹을 고치지 않아도 되도록 새 이름이 없으면 옛 이름을 오른손으로 쓴다.</summary>
        public static Transform Find(Transform gun, string spotName)
        {
            if (gun == null)
                return null;

            Transform legacySpot = null;
            foreach (Transform child in gun.GetComponentsInChildren<Transform>(true))
            {
                if (child == gun)
                    continue;
                if (IsSameName(child.name, spotName))
                    return child;
                if (spotName == RightHand && legacySpot == null && IsSameName(child.name, LegacyRightHand))
                    legacySpot = child;
            }
            return legacySpot;
        }

        /// <summary>총이 손에 붙은 뒤에도 쓰도록 총 루트 기준 좌표로 준다.</summary>
        public static Vector3? FindLocalPoint(Transform gun, string spotName)
        {
            Transform spot = Find(gun, spotName);
            return spot != null ? gun.InverseTransformPoint(spot.position) : null;
        }

        /// <summary>모델러마다 표기가 달라 공백과 대소문자를 무시한다.</summary>
        private static bool IsSameName(string first, string second)
        {
            return string.Equals(first.Replace(" ", ""), second.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
