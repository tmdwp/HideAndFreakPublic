using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>이동 입력과 카메라 제한이 벽타기 구현에 의존하지 않도록 현재 이동 평면의 법선을 제공한다.</summary>
    public interface IMovementPlaneProvider
    {
        /// <summary>입력 방향을 평면에 투영하려면 방향이 일정해야 하므로 항상 바깥쪽을 가리킨다.</summary>
        Vector3 MovementPlaneNormal { get; }
    }
}
