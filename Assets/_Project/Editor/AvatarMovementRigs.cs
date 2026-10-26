using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>아바타 종류. (이동 구현이 역할별 캐릭터 컴포넌트를 고를 때 쓴다)</summary>
    public enum AvatarKind
    {
        Guard,
        Mannequin
    }

    /// <summary>
    /// 아바타 프리팹에 캐릭터 이동 구현(현재 ECM2) 컴포넌트를 붙이는 방법.
    /// 프리팹 생성 도구(<see cref="TestEnvironmentSetupTool"/>)는 이동 라이브러리를 직접 참조하지 않고 이 인터페이스로 붙인다.
    /// 구현은 이동 에디터 어셈블리(GuardVsMannequin.Movement.ECM2.Editor)가 에디터 시작 때 <see cref="AvatarMovementRigs.Register"/>로 등록한다.
    /// </summary>
    public interface IAvatarMovementRig
    {
        /// <summary>
        /// 이동 충돌체와 역할별 캐릭터(가드: 1인칭, 마네킹: 3인칭)를 붙이고 기본값을 맞춘다. 붙인 캐릭터 컴포넌트를 돌려준다.
        /// </summary>
        Component AddCharacter(GameObject root, AvatarKind kind);

        /// <summary>
        /// 소유자 입력으로 캐릭터를 움직이는 컴포넌트를 붙인다. (소유자 전용 목록에 넣으므로 꺼서 돌려준다)
        /// </summary>
        Behaviour AddMovementDriver(GameObject root);

        /// <summary>
        /// 카메라 정면을 조준 광선으로 쓰는 컴포넌트(<c>IAimProvider</c>)를 붙인다.
        /// </summary>
        void AddAimProvider(GameObject root);

        /// <summary>
        /// 마네킹 전용 이동 능력(벽타기, 변신 중 회전)을 붙인다.
        /// </summary>
        void AddMannequinAbilities(GameObject root);
    }

    /// <summary>
    /// 등록된 아바타 이동 구현을 찾는다. 이동 라이브러리가 없는 프로젝트(public 브랜치)에서는 비어 있다.
    /// </summary>
    public static class AvatarMovementRigs
    {
        /// <summary>등록된 구현. 없으면 null.</summary>
        public static IAvatarMovementRig Current { get; private set; }

        /// <summary>
        /// 이동 구현을 등록한다. (이동 에디터 어셈블리가 에디터 시작 때 호출)
        /// </summary>
        public static void Register(IAvatarMovementRig rig)
        {
            Current = rig;
        }
    }
}
