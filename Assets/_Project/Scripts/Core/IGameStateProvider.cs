namespace GuardVsMannequin.Core
{
    /// <summary>런타임 오브젝트가 상태 머신 구현에 의존하지 않도록 현재 상태를 읽기 전용으로 제공한다.</summary>
    public interface IGameStateProvider
    {
        GameState CurrentState { get; }

        /// <summary>룸 설정을 시작 인원으로 보정한 값이라 룸 쪽 원본과 다를 수 있다.</summary>
        GameSettings Settings { get; }

        /// <summary>단위는 초.</summary>
        float RemainingTime { get; }
    }

    /// <summary>프리팹이 씬 오브젝트를 직렬화 참조할 수 없어 씬 단위 서비스의 최소 조회 지점을 둔다.</summary>
    public static class GameServices
    {
        /// <summary>게임 씬 밖에서는 null.</summary>
        public static IGameStateProvider State { get; private set; }

        /// <summary>룸 없이 게임 씬만 열어 테스트하면 null.</summary>
        public static IRoomInfo Room { get; private set; }

        /// <summary>룸 없이 게임 씬만 열어 테스트하면 null.</summary>
        public static ISceneFlow SceneFlow { get; private set; }

        /// <summary>테스트 방 게임 씬의 호스트에서만 있고, 그 밖에는 null.</summary>
        public static ITestRoomActions TestRoom { get; private set; }

        /// <summary>물리는 서버가 계산하므로 게임 씬의 서버에서만 있고, 그 밖에는 null.</summary>
        public static IPropPhysics PropPhysics { get; private set; }

        /// <summary>
        /// 총·수류탄 같은 프리팹이 씬의 물리 서비스를 직렬화로 참조할 수 없어서 여기에 등록해 둔다.
        /// </summary>
        public static void RegisterPropPhysics(IPropPhysics service)
        {
            PropPhysics = service;
        }

        /// <summary>
        /// 씬 전환 중 새 인스턴스가 먼저 등록됐을 수 있어서, 자기 자신일 때만 해제한다.
        /// </summary>
        public static void UnregisterPropPhysics(IPropPhysics service)
        {
            if (PropPhysics == service)
                PropPhysics = null;
        }

        /// <summary>게임 씬의 클라이언트에서만 있고, 그 밖에는 null.</summary>
        public static IPropPushRequester PropPush { get; private set; }

        /// <summary>아바타가 씬의 물리 창구를 직렬화로 참조할 수 없어서 여기에 등록해 둔다.</summary>
        public static void RegisterPropPush(IPropPushRequester requester)
        {
            PropPush = requester;
        }

        /// <summary>
        /// 씬 전환 중 새 인스턴스가 먼저 등록됐을 수 있어서, 자기 자신일 때만 해제한다.
        /// </summary>
        public static void UnregisterPropPush(IPropPushRequester requester)
        {
            if (PropPush == requester)
                PropPush = null;
        }

        /// <summary>
        /// HUD 테스트 메뉴가 씬의 테스트 방 조작을 찾을 수 있도록 등록해 둔다.
        /// </summary>
        public static void RegisterTestRoom(ITestRoomActions actions)
        {
            TestRoom = actions;
        }

        /// <summary>
        /// 씬 전환 중 새 인스턴스가 먼저 등록됐을 수 있어서, 자기 자신일 때만 해제한다.
        /// </summary>
        public static void UnregisterTestRoom(ITestRoomActions actions)
        {
            if (TestRoom == actions)
                TestRoom = null;
        }

        /// <summary>
        /// 룸이 게임 씬보다 오래 살아 있어서, 게임 씬이 룸을 직렬화로 참조할 수 없어 여기에 등록해 둔다.
        /// </summary>
        public static void RegisterRoom(IRoomInfo room, ISceneFlow sceneFlow)
        {
            Room = room;
            SceneFlow = sceneFlow;
        }

        /// <summary>
        /// 씬 전환 중 새 룸이 먼저 등록됐을 수 있어서, 자기 자신일 때만 해제한다.
        /// </summary>
        public static void UnregisterRoom(IRoomInfo room)
        {
            if (Room != room)
                return;

            Room = null;
            SceneFlow = null;
        }

        /// <summary>
        /// 아바타처럼 런타임에 생기는 오브젝트가 상태 기계를 찾을 수 있도록 등록해 둔다.
        /// </summary>
        public static void RegisterState(IGameStateProvider provider)
        {
            State = provider;
        }

        /// <summary>
        /// 씬 전환 중 새 인스턴스가 먼저 등록됐을 수 있어서, 자기 자신일 때만 해제한다.
        /// </summary>
        public static void UnregisterState(IGameStateProvider provider)
        {
            if (State == provider)
                State = null;
        }

        /// <summary>
        /// 게임 씬 밖에서도 null 검사 없이 쓸 수 있도록, 제공자가 없으면 Waiting으로 본다.
        /// </summary>
        public static GameState CurrentStateOrWaiting => State?.CurrentState ?? GameState.Waiting;

        /// <summary>기본값을 곳곳에 적지 않도록 설정을 여기서 읽고, 제공자가 없으면 기본 설정 파일 값을 쓴다.</summary>
        public static GameSettings CurrentSettingsOrDefault => State?.Settings ?? DefaultGameSettings.Cached;
    }
}
