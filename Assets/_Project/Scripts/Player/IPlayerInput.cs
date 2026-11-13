using System;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>게임 코드가 Input System에 의존하지 않도록 입력을 인터페이스로 둔다.</summary>
    public interface IPlayerInput
    {
        /// <summary>x: 좌우, y: 앞뒤.</summary>
        Vector2 Move { get; }

        /// <summary>프레임마다 쌓이지 않고 이번 프레임 이동량만 담는다.</summary>
        Vector2 Look { get; }

        /// <summary>변신 중에는 마우스가 카메라를 돌리므로 사물 방향은 따로 Q(-1)/E(+1)로 돌린다.</summary>
        float Rotate { get; }

        /// <summary>마네킹 상호작용처럼 누르고 있어야 진행되는 동작이 있어서 유지 여부도 제공한다.</summary>
        bool IsPrimaryHeld { get; }

        bool IsSprintHeld { get; }

        /// <summary>누르고 있어도 착지 후 계속 뛰지 않도록, 이동 쪽이 누르는 순간을 직접 판별할 수 있게 유지 여부로 준다.</summary>
        bool IsJumpHeld { get; }

        event Action PrimaryPressed;

        event Action PrimaryReleased;

        /// <summary>가드는 탐지, 마네킹은 변신/해제로 역할마다 뜻이 다르다.</summary>
        event Action SecondaryPressed;

        /// <summary>설정 화면과 헷갈리지 않도록 키 번호와 같은 1부터 시작하는 번호.</summary>
        event Action<int> EmotePressed;

        event Action FlashlightPressed;

        event Action ReloadPressed;

        /// <summary>-1: 이전, +1: 다음.</summary>
        event Action<int> SpectateCycled;
    }
}
