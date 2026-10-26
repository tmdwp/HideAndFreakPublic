using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 아바타를 움직이는 캐릭터 이동 구현(현재 ECM2)을 게임 코드에서 다루는 창구.
    /// 게임 코드(Runtime 어셈블리)는 이동 라이브러리를 직접 참조하지 않고 이 인터페이스만 쓴다.
    /// 구현은 이동 어셈블리(GuardVsMannequin.Movement.ECM2)의 <c>PlayerCharacter</c>가 맡는다.
    /// 이동 라이브러리가 없는 프로젝트(public 브랜치)에서도 게임 코드가 컴파일되게 하기 위함이다.
    /// </summary>
    public interface ICharacterMotor
    {
        /// <summary>걷기 최대 속도(m/s). (게임 설정 속도 × 배율, <see cref="MoveSpeedController"/>가 정함)</summary>
        float MaxWalkSpeed { get; set; }

        /// <summary>비행(벽타기) 최대 속도(m/s).</summary>
        float MaxFlySpeed { get; set; }

        /// <summary>충돌 캡슐 반지름(m).</summary>
        float CapsuleRadius { get; }

        /// <summary>충돌 캡슐 높이(m).</summary>
        float CapsuleHeight { get; }

        /// <summary>
        /// 충돌 캡슐 크기를 바꾼다. (변신하면 사물 크기에 맞춘다)
        /// </summary>
        void SetCapsule(float radius, float height);

        /// <summary>시점 카메라 트랜스폼. 카메라가 없으면(원격 아바타) null.</summary>
        Transform ViewTransform { get; }

        /// <summary>
        /// 로컬 소유자 아바타로 설정한다. 카메라를 연결하고 화면이 부드럽게 보이도록 보간을 켠다.
        /// </summary>
        void SetupAsOwner(Camera playerCamera);

        /// <summary>
        /// 원격 아바타로 설정한다. 이동 시뮬레이션을 멈춰 네트워크로 받은 위치만 따르게 한다.
        /// </summary>
        void SetupAsRemote();

        /// <summary>
        /// 위치(와 방향)를 바로 옮기고 속도를 없앤다. (서버의 되돌림·순간이동)
        /// </summary>
        /// <param name="rotation">null이면 방향은 그대로 둔다.</param>
        void Teleport(Vector3 position, Quaternion? rotation);

        /// <summary>
        /// 이동 시뮬레이션을 멈추거나 다시 시작한다. 멈춘 동안은 입력·중력으로 움직이지 않고 <see cref="Teleport"/>로만 옮겨진다.
        /// (변신한 마네킹이 총에 맞아 날아가는 동안)
        /// </summary>
        void SetFrozen(bool frozen);
    }
}
