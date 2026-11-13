using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>이동 구현이 역할별 캐릭터 컴포넌트를 고를 때 쓰는 아바타 종류.</summary>
    public enum AvatarKind
    {
        Guard,
        Mannequin
    }

    /// <summary>프리팹 생성 도구가 이동 라이브러리를 직접 참조하지 않도록 이동 컴포넌트를 붙이는 방법을 인터페이스로 둔다.</summary>
    public interface IAvatarMovementRig
    {
        /// <summary>가드와 마네킹은 필요한 컴포넌트가 달라 역할을 받아 고른다.</summary>
        Component AddCharacter(GameObject root, AvatarKind kind);

        /// <summary>
        /// 남의 아바타가 내 입력으로 움직이면 안 되므로 꺼서 돌려주고, 호출한 쪽이 소유자 전용 목록에 넣어 소유자에게만 켜게 한다.
        /// </summary>
        Behaviour AddMovementDriver(GameObject root);

        /// <summary>
        /// 조준 방식이 카메라 구현에 달려 있어 이동 구현이 함께 붙이게 한다.
        /// </summary>
        void AddAimProvider(GameObject root);

        /// <summary>
        /// 벽타기·변신 중 회전은 캐릭터 이동 라이브러리에 의존하므로 이동 구현이 붙이게 한다.
        /// </summary>
        void AddMannequinAbilities(GameObject root);
    }

    /// <summary>이동 라이브러리가 없는 프로젝트에서도 동작하도록 등록된 구현이 없으면 비어 있게 둔다.</summary>
    public static class AvatarMovementRigs
    {
        /// <summary>이동 라이브러리가 없는 프로젝트에서는 등록되지 않아 null이다.</summary>
        public static IAvatarMovementRig Current { get; private set; }

        /// <summary>
        /// 이 어셈블리가 이동 어셈블리를 참조하면 라이브러리 없이 컴파일되지 않으므로, 반대로 이동 에디터 어셈블리가 시작할 때 자신을 등록한다.
        /// </summary>
        public static void Register(IAvatarMovementRig rig)
        {
            Current = rig;
        }
    }
}
