using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>게임 코드가 이동 라이브러리를 직접 참조하지 않도록 캐릭터 이동 구현을 인터페이스로 둔다.</summary>
    public interface ICharacterMotor
    {
        /// <summary>MoveSpeedController가 정하는 걷기 속도(m/s).</summary>
        float MaxWalkSpeed { get; set; }

        /// <summary>벽타기가 비행 모드라 따로 정하는 속도(m/s).</summary>
        float MaxFlySpeed { get; set; }

        /// <summary>단위는 m.</summary>
        float CapsuleRadius { get; }

        /// <summary>단위는 m.</summary>
        float CapsuleHeight { get; }

        /// <summary>
        /// 변신하면 사물보다 큰 캡슐에 걸려 숨을 수 없는 곳이 생기므로 사물 크기에 맞춘다.
        /// </summary>
        void SetCapsule(float radius, float height);

        /// <summary>
        /// 캡슐로는 길쭉하거나 각진 사물 모양을 맞출 수 없어서, 이 상자들이 벽에 박히면 밀어내 보이는 모양대로 막히게 한다.
        /// </summary>
        /// <param name="shapeBoxes">빈 목록이면 캡슐만으로 움직인다.</param>
        void SetShapeBoxes(IReadOnlyList<BoxCollider> shapeBoxes);

        /// <summary>원격 아바타에는 카메라가 없어서 null일 수 있다.</summary>
        Transform ViewTransform { get; }

        /// <summary>
        /// 고정 틱 이동이 화면에서 끊겨 보이지 않도록 카메라를 연결하면서 보간을 켠다.
        /// </summary>
        void SetupAsOwner(Camera playerCamera);

        /// <summary>
        /// 원격 아바타가 스스로 중력·충돌로 움직이면 네트워크 위치와 어긋나므로 시뮬레이션을 멈춘다.
        /// </summary>
        void SetupAsRemote();

        /// <summary>
        /// 서버 되돌림이나 순간이동 뒤에 남은 속도로 계속 미끄러지지 않도록 속도도 함께 없앤다.
        /// </summary>
        /// <param name="rotation">null이면 방향은 그대로 둔다.</param>
        void Teleport(Vector3 position, Quaternion? rotation);

        /// <summary>날아가는 동안에는 물리가 위치를 정하므로 입력·중력으로 움직이지 않게 멈춘다.</summary>
        void SetFrozen(bool frozen);

        /// <summary>
        /// 도약처럼 이동 속도 제한(은신 중 0 포함)과 상관없이 정해진 시간 동안 밀어야 하는 이동.
        /// 충돌은 평소처럼 처리하며, 땅이나 공중에 있을 때만 적용한다.
        /// </summary>
        /// <param name="horizontalVelocity">수평 속도(m/s). 세로 성분은 무시한다.</param>
        /// <param name="duration">단위는 초.</param>
        /// <param name="launchSpeed">처음 위로 차오르는 속도(m/s). 0이면 바닥을 따라 곧게 민다.</param>
        /// <param name="arcGravity">도약 동안 평소 중력 대신 쓸 아래 가속도(m/s²).</param>
        void Dash(Vector3 horizontalVelocity, float duration, float launchSpeed, float arcGravity);

        /// <summary>
        /// 밟고 선 바닥까지 밀면 올라선 사물이 넘어지므로, 옆면으로 부딪힌 충돌만 (닿은 충돌체, 닿은 지점, 캐릭터 속도)로 알린다.
        /// 스스로 시뮬레이션하는 내 아바타에서만 발생한다.
        /// </summary>
        event Action<Collider, Vector3, Vector3> SideCollided;
    }
}
