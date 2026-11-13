using GuardVsMannequin.Core;
using UnityEngine;

namespace GuardVsMannequin.Network
{
    /// <summary>다시 들어오면 되살릴 끊기기 직전 아바타 상태.</summary>
    public struct RejoinAvatarState
    {
        public string displayName;
        public int currentHealth;
        public int maxHealth;
        public Vector3 position;
        public Quaternion rotation;

        /// <summary>변신한 사물 번호.</summary>
        public int propIndex;

        /// <summary>사냥 체력을 다시 적용하지 않도록 기억하는 사냥 시간에 끊겼는지 여부.</summary>
        public bool wasHunting;
    }

    /// <summary>
    /// 게임 중 끊긴 사람의 재접속 대기 자리.
    /// </summary>
    public sealed class RejoinSlot
    {
        /// <summary>이 자리의 주인 키.</summary>
        public string key;

        public PlayerRole role;

        /// <summary>살아 있지 않았으면 돌아와 관전만 하도록 기억하는 끊길 때의 생존 여부.</summary>
        public bool isAlive;

        public RejoinAvatarState avatar;

        /// <summary>돌아올 수 있는 유예 시간을 재는 기준(초).</summary>
        public float disconnectTime;

        /// <summary>맵을 불러오는 동안 자리가 사라지지 않도록 적어 두는 다시 접속한 연결 번호.</summary>
        public int claimedByClientId = -1;

        /// <summary>맵 불러오기 기한을 재는 기준(초).</summary>
        public float claimTime;
    }
}
