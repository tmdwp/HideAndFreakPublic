using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>이동 캡슐은 보이는 모양보다 크거나 작아서, 더 정확한 판정 모양이 있을 때 총이 캡슐을 건너뛰게 한다.</summary>
    public interface IHitShapeOverride
    {
        /// <summary>참이면 이 충돌체 대신 다른 판정 모양으로 맞힌다.</summary>
        bool ReplacesCollider(Collider collider);
    }
}
