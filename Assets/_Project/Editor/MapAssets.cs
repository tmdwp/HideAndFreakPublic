namespace GuardVsMannequin.Editor
{
    /// <summary>맵 빌더가 경로를 한곳에서 바꿀 수 있도록 맵 물체 프리팹 경로를 모은다.</summary>
    internal static class MapAssets
    {
        /// <summary>Blender로 만든 MapKit 모델의 프리팹.</summary>
        internal static class Kit
        {
            private const string Root = "Assets/_Project/Prefabs/Obj/Kit/";
            public const string Floor = Root + "Structure/Floor.prefab";
            public const string Wall = Root + "Structure/Wall.prefab";
            public const string WallDoor = Root + "Structure/Wall_Door_4m.prefab";
            public const string WallWindow = Root + "Structure/Wall_Window_4m.prefab";
            public const string Platform = Root + "Structure/Platform_2x2x1.prefab";
            public const string Stairs = Root + "Structure/Stairs_2m.prefab";
            public const string Railing = Root + "Structure/Railing_2m.prefab";
            public const string PillarSquare = Root + "Structure/Pillar_Square.prefab";
            public const string Crate = Root + "Prop/Crate.prefab";
            /// <summary>상자 프리팹을 0.6배로 줄인 작은 상자.</summary>
            public const string CrateSmall = Crate + "?scale=0.6";
            public const string Barrel = Root + "Prop/Barrel.prefab";
            public const string Pallet = Root + "Prop/Pallet.prefab";
            public const string Box = Root + "Prop/Box_Cardboard.prefab";
            public const string BoxStack = Root + "Prop/Box_Cardboard_Stack.prefab";
            public const string Shelf = Root + "Prop/Shelf_Metal.prefab";
            public const string Table = Root + "Prop/Table.prefab";
            public const string Chair = Root + "Prop/Chair.prefab";
            public const string Stool = Root + "Prop/Stool.prefab";
            public const string Locker = Root + "Prop/Locker.prefab";
            public const string LockerRow = Root + "Prop/Locker_Row3.prefab";
            public const string Bed = Root + "Prop/Bed_Hospital.prefab";
            public const string Cabinet = Root + "Prop/Cabinet_Drawers.prefab";
            public const string TrashBin = Root + "Prop/TrashBin.prefab";
            public const string Cone = Root + "Prop/TrafficCone.prefab";
            public const string Bucket = Root + "Prop/Bucket.prefab";
            public const string Pipe = Root + "Prop/Pipe_Straight_2m.prefab";
            public const string Duct = Root + "Prop/Duct_Straight_2m.prefab";
            public const string PendantLamp = Root + "Prop/Lamp_Ceiling.prefab";
            public const string SandbagWall = Root + "Prop/Sandbag_Wall.prefab";
        }

        /// <summary>지하철역 소품(앞면 +Z).</summary>
        internal static class Subway
        {
            private const string Root = "Assets/_Project/Prefabs/Obj/Subway/";
            public const string Bench = Root + "Bench.prefab";
            public const string RecycleBin = Root + "RecycleBin.prefab";
            public const string TicketMachine = Root + "TicketMachine.prefab";
            public const string FireCabinet = Root + "FireCabinet.prefab";
            public const string InfoBoard = Root + "InfoBoard.prefab";
            public const string AdBoard = Root + "AdBoard.prefab";
            public const string CoinLocker = Root + "CoinLocker.prefab";
            public const string EmergencyBox = Root + "EmergencyBox.prefab";
            public const string Stanchion = Root + "Stanchion.prefab";
            public const string WetFloorSign = Root + "WetFloorSign.prefab";
            public const string Planter = Root + "Planter.prefab";
            public const string Suitcase = Root + "Suitcase.prefab";
            public const string CleaningCart = Root + "CleaningCart.prefab";
            /// <summary>앉는 쪽이 −Z라 의자 쪽을 보게 하려면 180도 돌리는 사무 책상.</summary>
            public const string OfficeDesk = Root + "OfficeDesk.prefab";
            public const string OfficeChair = Root + "OfficeChair.prefab";
            public const string Monitor = Root + "Monitor.prefab";
            public const string Toilet = Root + "Toilet.prefab";
            public const string WashBasin = Root + "WashBasin.prefab";
            public const string ElectricPanel = Root + "ElectricPanel.prefab";
            public const string AirHandler = Root + "AirHandler.prefab";
        }

        /// <summary>숲 맵 자연물(피벗은 밑동 중앙).</summary>
        internal static class Nature
        {
            private const string Root = "Assets/_Project/Prefabs/Obj/Nature/";
            public const string BushRound = Root + "Bush_Round_A.prefab";
            public const string BushWide = Root + "Bush_Wide_A.prefab";
            public const string BushTall = Root + "Bush_Tall_A.prefab";
            public const string BushBerry = Root + "Bush_Berry_A.prefab";
        }

        /// <summary>도시 길거리 소품(앞면 +Z, 피벗은 바닥 중앙).</summary>
        internal static class City
        {
            private const string Root = "Assets/_Project/Prefabs/Obj/City/";
            public const string StreetLamp = Root + "StreetLamp.prefab";
            public const string TrafficLight = Root + "TrafficLight.prefab";
            public const string FireHydrant = Root + "FireHydrant.prefab";
            public const string Mailbox = Root + "Mailbox.prefab";
            public const string StreetBin = Root + "StreetBin.prefab";
            public const string ParkBench = Root + "ParkBench.prefab";
            public const string BusStop = Root + "BusStop.prefab";
            public const string NewsBox = Root + "NewsBox.prefab";
            public const string Bollard = Root + "Bollard.prefab";
            public const string PlanterBox = Root + "PlanterBox.prefab";
            public const string Dumpster = Root + "Dumpster.prefab";
            public const string TrashBags = Root + "TrashBags.prefab";
            public const string ACUnit = Root + "ACUnit.prefab";
            public const string UtilityBox = Root + "UtilityBox.prefab";
            /// <summary>길이 방향이 X축인 자전거.</summary>
            public const string Bicycle = Root + "Bicycle.prefab";
            /// <summary>길이 방향이 X축(앞 +X)인 배달 오토바이.</summary>
            public const string Scooter = Root + "Scooter.prefab";
            /// <summary>3m를 넘어 변신 대상이 아닌 승용차(앞 +X).</summary>
            public const string Sedan = Root + "Sedan.prefab";
            public const string CafeTable = Root + "CafeTable.prefab";
            /// <summary>등받이가 −Z라 앉는 사람이 +Z를 보는 카페 의자.</summary>
            public const string CafeChair = Root + "CafeChair.prefab";
            public const string Parasol = Root + "Parasol.prefab";
            public const string SignBoard = Root + "SignBoard.prefab";
            public const string PhoneBooth = Root + "PhoneBooth.prefab";
            public const string Barricade = Root + "Barricade.prefab";
            public const string WaterBarrier = Root + "WaterBarrier.prefab";
            public const string FridgeCase = Root + "FridgeCase.prefab";
            public const string ShopShelf = Root + "ShopShelf.prefab";
            public const string Counter = Root + "Counter.prefab";
            public const string CrateStack = Root + "CrateStack.prefab";
            public const string ParkingBooth = Root + "ParkingBooth.prefab";
            public const string RoadSign = Root + "RoadSign.prefab";
            /// <summary>벽으로 쓰므로 변신 대상으로 등록하지 않는 공사장 가림막.</summary>
            public const string ConstructionFence = Root + "ConstructionFence.prefab";
            public const string WheelStop = Root + "WheelStop.prefab";
        }

        /// <summary>실제 크기로 만들어 맵 빌더가 10배로 키워 놓는 거대한 방 소품.</summary>
        internal static class GiantRoom
        {
            private const string Root = "Assets/_Project/Prefabs/Obj/GiantRoom/";
            /// <summary>머리판이 −Z 쪽인 침대.</summary>
            public const string Bed = Root + "Bed.prefab";
            public const string Nightstand = Root + "Nightstand.prefab";
            /// <summary>앉는 쪽이 +Z, 서랍장이 −X 쪽인 책상.</summary>
            public const string Desk = Root + "Desk.prefab";
            /// <summary>등받이가 −Z라 앉는 사람이 +Z를 보는 책상 의자.</summary>
            public const string DeskChair = Root + "DeskChair.prefab";
            /// <summary>앞(+Z)이 열린 6단 책장.</summary>
            public const string Bookshelf = Root + "Bookshelf.prefab";
            public const string Book = Root + "Book.prefab";
            public const string Wardrobe = Root + "Wardrobe.prefab";
            /// <summary>등받이가 −Z 쪽인 소파.</summary>
            public const string Sofa = Root + "Sofa.prefab";
            public const string CoffeeTable = Root + "CoffeeTable.prefab";
            public const string Rug = Root + "Rug.prefab";
            public const string FloorLamp = Root + "FloorLamp.prefab";
            public const string DeskLamp = Root + "DeskLamp.prefab";
            public const string CeilingLamp = Root + "CeilingLamp.prefab";
            public const string ToyBox = Root + "ToyBox.prefab";
            public const string TrashCan = Root + "TrashCan.prefab";
            /// <summary>손잡이가 +Z 쪽인 방문.</summary>
            public const string Door = Root + "Door.prefab";
            /// <summary>침대에서 앞(+Z)으로 흘러내려 오르는 비탈이 되는 이불 자락.</summary>
            public const string BlanketDrape = Root + "BlanketDrape.prefab";

            public const string Pencil = Root + "Small/Pencil.prefab";
            public const string Eraser = Root + "Small/Eraser.prefab";
            public const string Ruler = Root + "Small/Ruler.prefab";
            public const string Sharpener = Root + "Small/Sharpener.prefab";
            public const string Crayon = Root + "Small/Crayon.prefab";
            public const string GlueStick = Root + "Small/GlueStick.prefab";
            public const string Scissors = Root + "Small/Scissors.prefab";
            public const string Coin = Root + "Small/Coin.prefab";
            public const string Dice = Root + "Small/Dice.prefab";
            public const string ToyBlock = Root + "Small/ToyBlock.prefab";
            public const string Mug = Root + "Small/Mug.prefab";
            public const string RubberDuck = Root + "Small/RubberDuck.prefab";
            public const string Battery = Root + "Small/Battery.prefab";
            public const string StickyNotes = Root + "Small/StickyNotes.prefab";
            public const string Smartphone = Root + "Small/Smartphone.prefab";
            public const string ToyCar = Root + "Small/ToyCar.prefab";
            public const string PaperClip = Root + "Small/PaperClip.prefab";
            public const string BottleCap = Root + "Small/BottleCap.prefab";
            public const string LegoBrick = Root + "Small/LegoBrick.prefab";
            public const string TapeRoll = Root + "Small/TapeRoll.prefab";
            public const string Apple = Root + "Small/Apple.prefab";
            public const string AlarmClock = Root + "Small/AlarmClock.prefab";
            public const string TissueBox = Root + "Small/TissueBox.prefab";
        }

        /// <summary>환승역 맵이 쓰는 자판기 팩 프리팹.</summary>
        public const string VendingMachine = "Assets/_Project/Prefabs/Obj/Subway/VendingMachine.prefab";
    }
}
