using System;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 게임플레이 코드가 사용하는 입력 추상화.
    /// Input System이나 키 설정에 직접 의존하지 않도록 분리한다(DIP).
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>WASD 이동 입력. (x: 좌우, y: 앞뒤)</summary>
        Vector2 Move { get; }

        /// <summary>이번 프레임의 마우스 이동량.</summary>
        Vector2 Look { get; }

        /// <summary>좌우 회전 입력(Q: -1, E: +1). 변신 중인 마네킹이 사물 방향을 돌릴 때 사용한다.</summary>
        float Rotate { get; }

        /// <summary>좌클릭 유지 여부. (가드: 공격, 마네킹: 상호작용)</summary>
        bool IsPrimaryHeld { get; }

        /// <summary>달리기 키(Shift) 유지 여부.</summary>
        bool IsSprintHeld { get; }

        /// <summary>점프 키(Space) 유지 여부. 누르는 순간 점프하고, 누르고 있어도 착지 후 다시 뛰지 않는다.</summary>
        bool IsJumpHeld { get; }

        /// <summary>좌클릭을 눌렀을 때.</summary>
        event Action PrimaryPressed;

        /// <summary>좌클릭을 뗐을 때.</summary>
        event Action PrimaryReleased;

        /// <summary>우클릭을 눌렀을 때. (가드: 탐지, 마네킹: 변신/해제)</summary>
        event Action SecondaryPressed;

        /// <summary>감정 표현 키(1~6)를 눌렀을 때. 인자는 1부터 시작하는 번호.</summary>
        event Action<int> EmotePressed;

        /// <summary>손전등 키(E)를 눌렀을 때.</summary>
        event Action FlashlightPressed;

        /// <summary>장전 키(R)를 눌렀을 때.</summary>
        event Action ReloadPressed;

        /// <summary>관전 대상 전환 키를 눌렀을 때. (-1: 이전, +1: 다음)</summary>
        event Action<int> SpectateCycled;
    }
}
