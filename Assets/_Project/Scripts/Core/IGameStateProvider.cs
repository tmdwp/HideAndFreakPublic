namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 현재 게임 상태를 읽기 전용으로 제공한다.
    /// 아바타 등 런타임에 생성되는 오브젝트가 상태 머신 구현에 의존하지 않도록 분리한다(ISP, DIP).
    /// </summary>
    public interface IGameStateProvider
    {
        /// <summary>현재 상태.</summary>
        GameState CurrentState { get; }

        /// <summary>이번 판에 확정된 게임 설정.</summary>
        GameSettings Settings { get; }

        /// <summary>현재 상태 타이머의 남은 시간(초).</summary>
        float RemainingTime { get; }
    }

    /// <summary>
    /// 씬 단위 서비스를 등록하고 조회한다.
    /// 프리팹에서 씬 오브젝트를 직렬화 참조할 수 없어 최소한의 조회 지점으로 사용한다.
    /// </summary>
    public static class GameServices
    {
        /// <summary>현재 씬의 게임 상태 제공자. 없으면 null.</summary>
        public static IGameStateProvider State { get; private set; }

        /// <summary>현재 룸 정보. 룸 없이 실행하면 null.</summary>
        public static IRoomInfo Room { get; private set; }

        /// <summary>룸 복귀 흐름. 룸 없이 실행하면 null.</summary>
        public static ISceneFlow SceneFlow { get; private set; }

        /// <summary>테스트 방 조작. 테스트 방 게임 씬의 호스트(서버)에서만 있고, 그 밖에는 null.</summary>
        public static ITestRoomActions TestRoom { get; private set; }

        /// <summary>변신 사물 물리. 게임 씬의 서버에서만 있고, 그 밖에는 null.</summary>
        public static IPropPhysics PropPhysics { get; private set; }

        /// <summary>
        /// 변신 사물 물리 서비스를 등록한다.
        /// </summary>
        public static void RegisterPropPhysics(IPropPhysics service)
        {
            PropPhysics = service;
        }

        /// <summary>
        /// 등록된 서비스와 같을 때만 해제한다.
        /// </summary>
        public static void UnregisterPropPhysics(IPropPhysics service)
        {
            if (PropPhysics == service)
                PropPhysics = null;
        }

        /// <summary>
        /// 테스트 방 조작을 등록한다.
        /// </summary>
        public static void RegisterTestRoom(ITestRoomActions actions)
        {
            TestRoom = actions;
        }

        /// <summary>
        /// 등록된 조작과 같을 때만 해제한다.
        /// </summary>
        public static void UnregisterTestRoom(ITestRoomActions actions)
        {
            if (TestRoom == actions)
                TestRoom = null;
        }

        /// <summary>
        /// 룸 정보와 흐름을 등록한다.
        /// </summary>
        public static void RegisterRoom(IRoomInfo room, ISceneFlow sceneFlow)
        {
            Room = room;
            SceneFlow = sceneFlow;
        }

        /// <summary>
        /// 등록된 룸과 같을 때만 해제한다.
        /// </summary>
        public static void UnregisterRoom(IRoomInfo room)
        {
            if (Room != room)
                return;

            Room = null;
            SceneFlow = null;
        }

        /// <summary>
        /// 게임 상태 제공자를 등록한다.
        /// </summary>
        public static void RegisterState(IGameStateProvider provider)
        {
            State = provider;
        }

        /// <summary>
        /// 등록된 제공자와 같을 때만 해제한다. (씬 전환 중 다른 인스턴스를 지우지 않도록)
        /// </summary>
        public static void UnregisterState(IGameStateProvider provider)
        {
            if (State == provider)
                State = null;
        }

        /// <summary>
        /// 현재 상태를 반환한다. 제공자가 없으면 Waiting으로 취급한다.
        /// </summary>
        public static GameState CurrentStateOrWaiting => State?.CurrentState ?? GameState.Waiting;

        /// <summary>
        /// 이번 판 설정을 반환한다. 제공자가 없으면(게임 씬 밖, 테스트) 기본 설정 파일 값을 쓴다.
        /// 기본값 숫자를 코드 곳곳에 따로 적지 않도록 설정은 여기서 읽는다.
        /// </summary>
        public static GameSettings CurrentSettingsOrDefault => State?.Settings ?? DefaultGameSettings.Cached;
    }
}
