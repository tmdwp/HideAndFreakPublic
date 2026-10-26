namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// 직접 설계하는 맵 빌더가 쓰는 에셋 경로 모음. (MapKit 구조물·소품 + 프로젝트의 에셋 팩 프리팹)
    /// 빌더에서 <c>using static GuardVsMannequin.Editor.MapAssets;</c>로 Kit.Wall처럼 쓴다.
    /// </summary>
    internal static class MapAssets
    {
        /// <summary>Blender로 만든 MapKit 모델 (Art/MapKit).</summary>
        internal static class Kit
        {
            private const string Root = "Assets/_Project/Art/MapKit/Models/";
            public const string Floor = Root + "Structure/SM_Floor_4x4.fbx";
            public const string Wall = Root + "Structure/SM_Wall_4m.fbx";
            public const string WallDoor = Root + "Structure/SM_Wall_Door_4m.fbx";
            public const string WallWindow = Root + "Structure/SM_Wall_Window_4m.fbx";
            public const string Platform = Root + "Structure/SM_Platform_2x2x1.fbx";
            public const string Stairs = Root + "Structure/SM_Stairs_2m.fbx";
            public const string Railing = Root + "Structure/SM_Railing_2m.fbx";
            public const string PillarSquare = Root + "Structure/SM_Pillar_Square.fbx";
            public const string Crate = Root + "Prop/SM_Crate_1m.fbx";
            public const string CrateSmall = Root + "Prop/SM_Crate_06m.fbx";
            public const string Barrel = Root + "Prop/SM_Barrel.fbx";
            public const string Pallet = Root + "Prop/SM_Pallet.fbx";
            public const string Box = Root + "Prop/SM_Box_Cardboard.fbx";
            public const string BoxStack = Root + "Prop/SM_Box_Cardboard_Stack.fbx";
            public const string Shelf = Root + "Prop/SM_Shelf_Metal.fbx";
            public const string Table = Root + "Prop/SM_Table.fbx";
            public const string Chair = Root + "Prop/SM_Chair.fbx";
            public const string Stool = Root + "Prop/SM_Stool.fbx";
            public const string Locker = Root + "Prop/SM_Locker.fbx";
            public const string LockerRow = Root + "Prop/SM_Locker_Row3.fbx";
            public const string Bed = Root + "Prop/SM_Bed_Hospital.fbx";
            public const string Cabinet = Root + "Prop/SM_Cabinet_Drawers.fbx";
            public const string TrashBin = Root + "Prop/SM_TrashBin.fbx";
            public const string Cone = Root + "Prop/SM_TrafficCone.fbx";
            public const string Bucket = Root + "Prop/SM_Bucket.fbx";
            public const string Pipe = Root + "Prop/SM_Pipe_Straight_2m.fbx";
            public const string Duct = Root + "Prop/SM_Duct_Straight_2m.fbx";
            public const string PendantLamp = Root + "Prop/SM_Lamp_Ceiling.fbx";
            public const string SandbagWall = Root + "Prop/SM_Sandbag_Wall.fbx";
        }

        /// <summary>
        /// 지하철역 소품 (Art/MapKit/Models/Subway, Source~/generate_subway_props.py). 앞면은 +Z.
        /// 화면·발광판·도기는 머티리얼 이름 M_Sub_Screen / M_Sub_LightPanel / M_Sub_AdPanel / M_Sub_Ceramic으로 찾아 맵 머티리얼로 바꿔 끼운다.
        /// </summary>
        internal static class Subway
        {
            private const string Root = "Assets/_Project/Art/MapKit/Models/Subway/";
            public const string Bench = Root + "SM_Sub_Bench.fbx";
            public const string RecycleBin = Root + "SM_Sub_RecycleBin.fbx";
            public const string TicketMachine = Root + "SM_Sub_TicketMachine.fbx";
            public const string FireCabinet = Root + "SM_Sub_FireCabinet.fbx";
            public const string InfoBoard = Root + "SM_Sub_InfoBoard.fbx";
            public const string AdBoard = Root + "SM_Sub_AdBoard.fbx";
            public const string CoinLocker = Root + "SM_Sub_CoinLocker.fbx";
            public const string EmergencyBox = Root + "SM_Sub_EmergencyBox.fbx";
            public const string Stanchion = Root + "SM_Sub_Stanchion.fbx";
            public const string WetFloorSign = Root + "SM_Sub_WetFloorSign.fbx";
            public const string Planter = Root + "SM_Sub_Planter.fbx";
            public const string Suitcase = Root + "SM_Sub_Suitcase.fbx";
            public const string CleaningCart = Root + "SM_Sub_CleaningCart.fbx";
            /// <summary>사무 책상. 앉는 쪽이 −Z(앞면 반대)이므로 의자 쪽을 보게 하려면 180도 돌린다.</summary>
            public const string OfficeDesk = Root + "SM_Sub_OfficeDesk.fbx";
            public const string OfficeChair = Root + "SM_Sub_OfficeChair.fbx";
            public const string Monitor = Root + "SM_Sub_Monitor.fbx";
            public const string Toilet = Root + "SM_Sub_Toilet.fbx";
            public const string WashBasin = Root + "SM_Sub_WashBasin.fbx";
            public const string ElectricPanel = Root + "SM_Sub_ElectricPanel.fbx";
            public const string AirHandler = Root + "SM_Sub_AirHandler.fbx";
        }

        /// <summary>Abandoned Asylum 팩.</summary>
        internal static class Asylum
        {
            private const string Root = "Assets/Abandoned_Asylum/Prefabs/";
            public const string SurgeryTable = Root + "SurgeryTable.prefab";
            public const string SurgeryBed = Root + "SurgeryWheelBad.prefab";
            public const string MedRack2 = Root + "MedRack_V2.prefab";
            public const string MorgueBed = Root + "MorgueWheelBed.prefab";
            public const string MetalCase = Root + "CaseMetallic.prefab";
            public const string Monitor = Root + "PC_Monitor.prefab";
            public const string OfficeTable = Root + "TableOffice.prefab";
            public const string OfficeChair = Root + "ChairOffice.prefab";
            public const string WhiteTable = Root + "TableWhite.prefab";
            public const string WhiteChair = Root + "ChairWhite.prefab";
            public const string KitchenTable = Root + "TableKitchen.prefab";
            public const string Fridge = Root + "Fridge.prefab";
            public const string Boiler = Root + "BigBoiler.prefab";
            public const string Cupboard = Root + "Cupboard.prefab";
            public const string WheelChair = Root + "WheelChair.prefab";
            public const string Sink = Root + "Sink_V1.prefab";
            public const string Toilet = Root + "Toilet.prefab";
            public const string Bench = Root + "Bench.prefab";
            public const string Sofa = Root + "Sofa.prefab";
            public const string Elevator = Root + "Elevator.prefab";
            public const string StockShelf = Root + "BigShelf_Stock.prefab";
        }

        /// <summary>Horror Hospital 팩.</summary>
        internal static class Hospital
        {
            private const string Root = "Assets/Horror Hospital Pack/Prefabs/";
            public const string Stretcher = Root + "Stretcher.prefab";
            public const string Trolley = Root + "Trolley.prefab";
            public const string Bed = Root + "Bad.prefab";
        }

        /// <summary>RPG FPS 산업 팩.</summary>
        internal static class Industrial
        {
            private const string Root = "Assets/RPG_FPS_game_assets_industrial/";
            public const string ElectricBox1 = Root + "Other_props/Electric_box/Electric_box_v1/Electric_box_v1.prefab";
            public const string ElectricBox2 = Root + "Other_props/Electric_box/Electric_box_v2/Electric_box_v2.prefab";
            public const string Conditioner = Root + "Other_props/Conditioners/Conditioner_v1/Conditioner_v1.prefab";
            public const string Barrel = Root + "Barrels/Barrel_v2/Barrel_v2_single.prefab";
            public const string Barrel3 = Root + "Barrels/Barrel_v3/Barrel_v3_single.prefab";
            public const string WoodBox = Root + "Boxes/Wooden_box_v1/Wooden_box_v1_LD1.prefab";
            public const string BagsPallet = Root + "Other_props/Palets/Bags_on_pallet_v1/Bags_on_pallet_v1_1.prefab";
        }

        /// <summary>BK Alchemist House 팩.</summary>
        internal static class Alchemy
        {
            private const string Root = "Assets/BK_AlchemistHouse/Prefabs/Items/";
            public const string LabSetup1 = Root + "Lab/LabSetup01.prefab";
            public const string LabSetup2 = Root + "Lab/LabSetup02.prefab";
            public const string LabSetup3 = Root + "Lab/LabSetup03.prefab";
            public const string Flask1 = Root + "Bottles/Flask01.prefab";
            public const string Flask2 = Root + "Bottles/Flask03.prefab";
            public const string JarBrain = Root + "Organic/JarBrain.prefab";
            public const string JarBaby = Root + "Organic/JarBaby01.prefab";
        }

        public const string VendingMachine = "Assets/Modern Vending Machine/Modern Vending Machine.prefab";
    }
}
