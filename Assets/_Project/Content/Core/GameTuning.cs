using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace GuardVsMannequin.Core
{
    /// <summary>
    /// 밸런스 조정용 수치. 호스트가 룸에서 바꾸는 값(<see cref="GameSettings"/>)이 아닌, 개발 중에 다듬는 고정 수치를 모은다.
    /// 값은 Resources/Data/game_tuning.csv(빌드 후에는 GameData/Data/game_tuning.csv)에서 읽는다. (<see cref="DataFiles"/>)
    /// 파일에 없는 항목은 여기 적힌 코드 기본값을 쓴다.
    ///
    /// 항목 추가 방법: 아래에 속성 한 줄(이름 = csv의 id, 코드 기본값)을 더하고 game_tuning.csv에 같은 id로 한 줄을 더한다.
    /// 파일 내용이 다른 PC끼리는 판정이 어긋나므로, 파일 내용 지문(<see cref="Fingerprint"/>)을 게임 버전에 넣어 접속을 막는다.
    /// </summary>
    public static class GameTuning
    {
        private const string FilePath = "Data/game_tuning";
        private const string Extension = ".csv";

        private static Dictionary<string, float> values;
        private static string fingerprint;

        #region 이동

        /// <summary>사냥 시간 마네킹 이동 속도 배율. (호스트 설정의 마네킹 속도 × 이 값)</summary>
        public static float MannequinHuntSpeedMultiplier => Get(nameof(MannequinHuntSpeedMultiplier), 0.875f);

        /// <summary>달리기 중 이동 속도 배율.</summary>
        public static float SprintSpeedMultiplier => Get(nameof(SprintSpeedMultiplier), 1.6f);

        /// <summary>스태미나가 가득 찬 상태에서 쉬지 않고 달릴 수 있는 시간(초).</summary>
        public static float StaminaSprintSeconds => Get(nameof(StaminaSprintSeconds), 5f);

        /// <summary>스태미나가 바닥에서 가득 찰 때까지 걸리는 시간(초).</summary>
        public static float StaminaRecoverSeconds => Get(nameof(StaminaRecoverSeconds), 8f);

        /// <summary>달리기를 멈춘 뒤 스태미나 회복이 시작되기까지의 시간(초).</summary>
        public static float StaminaRecoverDelay => Get(nameof(StaminaRecoverDelay), 1f);

        /// <summary>스태미나를 다 쓴 뒤 다시 달릴 수 있게 되는 회복 비율(0~1).</summary>
        public static float StaminaRestartRatio => Get(nameof(StaminaRestartRatio), 0.25f);

        /// <summary>점프 순간 위쪽 속도(m/s). 점프 높이 ≈ 값² ÷ 19.6.</summary>
        public static float JumpImpulse => Get(nameof(JumpImpulse), 5f);

        #endregion

        #region 마네킹

        /// <summary>변신 대상을 지정할 수 있는 최대 거리(m).</summary>
        public static float TransformRange => Get(nameof(TransformRange), 8f);

        /// <summary>변신할 수 있는 사물의 최대 크기(가장 긴 변, m). 이보다 큰 사물은 조준해도 변신할 수 없다.</summary>
        public static float DisguiseMaxSize => Get(nameof(DisguiseMaxSize), 3f);

        /// <summary>변신 해제 후 다시 변신할 수 있을 때까지의 시간(초).</summary>
        public static float UntransformCooldown => Get(nameof(UntransformCooldown), 5f);

        /// <summary>전원 장치와 상호작용할 수 있는 최대 거리(m).</summary>
        public static float InteractRange => Get(nameof(InteractRange), 2.2f);

        /// <summary>여러 마네킹이 함께 파괴할 때의 속도 증가 밑값. (인원이 늘수록 이 값의 거듭제곱으로 빨라짐)</summary>
        public static float CoopDestroySpeedBase => Get(nameof(CoopDestroySpeedBase), 1.4f);

        /// <summary>사냥 시간 마네킹 공격 쿨타임(초).</summary>
        public static float MannequinAttackCooldown => Get(nameof(MannequinAttackCooldown), 1f);

        /// <summary>마네킹 공격 판정 구체의 반경(m).</summary>
        public static float MannequinAttackRadius => Get(nameof(MannequinAttackRadius), 0.35f);

        /// <summary>변신 중 주기적 소음 간격의 최솟값(초).</summary>
        public static float DisguiseNoiseIntervalMin => Get(nameof(DisguiseNoiseIntervalMin), 8f);

        /// <summary>변신 중 주기적 소음 간격의 최댓값(초).</summary>
        public static float DisguiseNoiseIntervalMax => Get(nameof(DisguiseNoiseIntervalMax), 14f);

        /// <summary>감정 표현 한 번의 재생 시간(초).</summary>
        public static float EmoteDuration => Get(nameof(EmoteDuration), 1.5f);

        #endregion

        #region 변신 사물 배치

        /// <summary>판마다 변신 사물 배치를 섞을지 여부. (1 = 섞음, 0 = 맵 원래 배치)</summary>
        public static bool PropShuffleEnabled => Get(nameof(PropShuffleEnabled), 1f) > 0.5f;

        /// <summary>자리를 서로 바꿀 수 있는 사물 크기 차이 배율. (1.4 = 바닥 넓이·높이가 1.4배 이내인 사물끼리)</summary>
        public static float PropShuffleSizeTolerance => Mathf.Max(1f, Get(nameof(PropShuffleSizeTolerance), 1.4f));

        /// <summary>바뀐 자리에서 사물을 흩는 최대 거리(m).</summary>
        public static float PropJitterDistance => Mathf.Max(0f, Get(nameof(PropJitterDistance), 0.6f));

        /// <summary>사물을 세로축으로 돌리는 최대 각도(도, ±). 180이면 아무 방향.</summary>
        public static float PropJitterRotation => Mathf.Clamp(Get(nameof(PropJitterRotation), 180f), 0f, 180f);

        #endregion

        #region 변신 사물 물리 (총에 맞으면 튕겨나감)

        /// <summary>총에 맞은 사물이 튕겨나갈지 여부. (1 = 켬, 0 = 끔)</summary>
        public static bool PropPhysicsEnabled => Get(nameof(PropPhysicsEnabled), 1f) > 0.5f;

        /// <summary>튕겨나가는 사물의 최대 크기(가장 긴 변, m). 이보다 큰 사물은 움직이지 않는다. (통로 막힘 방지)</summary>
        public static float PropPhysicsMaxSize => Mathf.Max(0f, Get(nameof(PropPhysicsMaxSize), 2f));

        /// <summary>동시에 움직이는 사물 수 상한. 넘으면 가장 오래 움직인 사물부터 그 자리에서 멈춘다.</summary>
        public static int PropPhysicsMaxActive => Mathf.Max(1, GetInt(nameof(PropPhysicsMaxActive), 40));

        /// <summary>사물 질량 = 겉모습 경계 부피 × 이 밀도(kg/m³).</summary>
        public static float PropDensity => Mathf.Max(0.01f, Get(nameof(PropDensity), 40f));

        /// <summary>사물 질량 최솟값(kg).</summary>
        public static float PropMassMin => Mathf.Max(0.01f, Get(nameof(PropMassMin), 1f));

        /// <summary>사물 질량 최댓값(kg).</summary>
        public static float PropMassMax => Mathf.Max(PropMassMin, Get(nameof(PropMassMax), 40f));

        /// <summary>총에 맞은 사물이 날아가는 세기. 속도(m/s) = 이 값 ÷ √질량(kg). (1kg 12m/s, 10kg 3.8m/s, 40kg 1.9m/s)</summary>
        public static float GunKnockback => Mathf.Max(0f, Get(nameof(GunKnockback), 12f));

        /// <summary>움직이는 사물이 멈춰 있는 사물·변신한 마네킹을 깨우는 최소 충격량(N·s).</summary>
        public static float PropWakeImpulse => Mathf.Max(0f, Get(nameof(PropWakeImpulse), 5f));

        /// <summary>이 속도(m/s) 이하로 <see cref="PropSettleTime"/> 동안 있으면 멈춘 것으로 본다.</summary>
        public static float PropSettleSpeed => Mathf.Max(0f, Get(nameof(PropSettleSpeed), 0.05f));

        /// <summary>멈춤으로 볼 때까지 느린 상태가 이어져야 하는 시간(초).</summary>
        public static float PropSettleTime => Mathf.Max(0f, Get(nameof(PropSettleTime), 0.5f));

        /// <summary>움직이는 사물 위치를 보내는 횟수(초당).</summary>
        public static float PropSyncRate => Mathf.Clamp(Get(nameof(PropSyncRate), 20f), 5f, 60f);

        /// <summary>판 시작 때 가장 낮은 사물보다 이만큼(m) 아래로 떨어지면 시작 위치로 되돌린다. (맵 밖 추락)</summary>
        public static float PropKillDepth => Mathf.Max(1f, Get(nameof(PropKillDepth), 10f));

        /// <summary>변신한 마네킹이 날아가는 "사물 모드"의 최대 시간(초). 넘으면 그 자리에서 멈춘다.</summary>
        public static float DisguiseKnockbackMaxTime => Mathf.Max(0.5f, Get(nameof(DisguiseKnockbackMaxTime), 4f));

        #endregion

        #region 사냥 시간 조명

        /// <summary>사냥 시간 맵 전체 환경광 밝기(0~1). 모든 플레이어 화면에 적용된다. (0.03 = 거의 깜깜)</summary>
        public static float HuntAmbientBrightness => Get(nameof(HuntAmbientBrightness), 0.03f);

        /// <summary>가드 손전등 밝기.</summary>
        public static float FlashlightIntensity => Get(nameof(FlashlightIntensity), 8f);

        /// <summary>사냥 시간에 꺼지지 않고 깜빡이며 남는 맵 조명 비율(0~1). 0이면 전부 꺼진다.</summary>
        public static float HuntFlickerLightRatio => Get(nameof(HuntFlickerLightRatio), 0.2f);

        /// <summary>깜빡이며 남는 조명의 밝기 배율. (원래 밝기 × 이 값)</summary>
        public static float HuntFlickerIntensityScale => Get(nameof(HuntFlickerIntensityScale), 0.4f);

        #endregion

        #region 사냥 시간 마네킹 시야

        /// <summary>마네킹 본인 화면에만 켜지는 개인 조명의 범위(m).</summary>
        public static float HuntVisionLightRange => Get(nameof(HuntVisionLightRange), 12f);

        /// <summary>개인 조명의 밝기.</summary>
        public static float HuntVisionLightIntensity => Get(nameof(HuntVisionLightIntensity), 1.5f);

        /// <summary>검은 안개가 시작되는 거리(m).</summary>
        public static float HuntVisionFogStart => Get(nameof(HuntVisionFogStart), 3f);

        /// <summary>이 거리부터 완전히 보이지 않는다(m).</summary>
        public static float HuntVisionFogEnd => Get(nameof(HuntVisionFogEnd), 13.5f);

        #endregion

        #region 가드

        /// <summary>가드 스폰 위치가 마네킹 스폰 지점에서 떨어져야 하는 최소 거리(m).</summary>
        public static float GuardSpawnMinDistance => Get(nameof(GuardSpawnMinDistance), 20f);

        /// <summary>가드끼리 스폰 위치가 떨어져야 하는 최소 거리(m).</summary>
        public static float GuardSpawnSeparation => Get(nameof(GuardSpawnSeparation), 8f);

        /// <summary>가드 스폰 후보에서 마네킹 스폰 지점이 "보인다"고 칠 최대 거리(m). 이보다 멀면 가려지지 않아도 보이지 않는 것으로 친다.</summary>
        public static float GuardSpawnSightDistance => Get(nameof(GuardSpawnSightDistance), 40f);

        /// <summary>탐색 시작 후 가드가 총·수류탄을 쓸 수 없는 시간(초). (출발 보호)</summary>
        public static float GuardToolLockTime => Get(nameof(GuardToolLockTime), 10f);

        /// <summary>수류탄 폭발 반경(m).</summary>
        public static float GrenadeRadius => Get(nameof(GrenadeRadius), 2f);

        /// <summary>수류탄에 맞은 마네킹의 외곽선이 가드에게 보이는 시간(초).</summary>
        public static float GrenadeRevealDuration => Get(nameof(GrenadeRevealDuration), 4f);

        /// <summary>수류탄에 맞은 마네킹의 감속 시간(초).</summary>
        public static float GrenadeSlowDuration => Get(nameof(GrenadeSlowDuration), 3f);

        /// <summary>수류탄 감속 배율.</summary>
        public static float GrenadeSlowMultiplier => Get(nameof(GrenadeSlowMultiplier), 0.5f);

        /// <summary>수류탄을 던지는 초기 속도(m/s).</summary>
        public static float GrenadeThrowSpeed => Get(nameof(GrenadeThrowSpeed), 14f);

        /// <summary>수류탄 최대 비행 시간(초). 넘으면 그 자리에서 터진다.</summary>
        public static float GrenadeMaxFlightTime => Get(nameof(GrenadeMaxFlightTime), 3f);

        /// <summary>손전등 범위(m).</summary>
        public static float FlashlightRange => Get(nameof(FlashlightRange), 25f);

        /// <summary>손전등을 다시 켜고 끌 수 있을 때까지 시간(초). (연타로 동기화·소리가 몰리는 것 방지)</summary>
        public static float FlashlightToggleCooldown => Get(nameof(FlashlightToggleCooldown), 0.2f);

        /// <summary>손전등 퍼짐 각도(도).</summary>
        public static float FlashlightAngle => Get(nameof(FlashlightAngle), 55f);

        #endregion

        #region 진행

        /// <summary>판 시작 준비(카운트다운) 시간(초). 이 동안은 아무도 움직일 수 없다.</summary>
        public static float MatchStartDelay => Get(nameof(MatchStartDelay), 3f);

        /// <summary>결과 화면을 보여준 뒤 룸으로 돌아가기까지의 시간(초).</summary>
        public static float ResultDisplayDuration => Get(nameof(ResultDisplayDuration), 10f);

        #endregion

        /// <summary>
        /// 파일에 적힌 값들의 지문(8자리 16진수). 게임 버전에 붙여 같은 값을 쓰는 PC끼리만 함께 플레이하게 한다.
        /// </summary>
        public static string Fingerprint
        {
            get
            {
                EnsureLoaded();
                return fingerprint;
            }
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때마다 다시 읽는다. (에디터에서 도메인 재로드를 끈 경우에도 파일 수정이 반영되도록)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            values = null;
            fingerprint = null;
        }

        /// <summary>
        /// 실수 항목을 읽는다. 파일에 없으면 코드 기본값.
        /// </summary>
        private static float Get(string id, float fallback)
        {
            EnsureLoaded();
            return values.TryGetValue(id, out float value) ? value : fallback;
        }

        /// <summary>
        /// 정수 항목을 읽는다. 파일 값은 반올림한다.
        /// </summary>
        private static int GetInt(string id, int fallback)
        {
            return Mathf.RoundToInt(Get(id, fallback));
        }

        /// <summary>
        /// 처음 한 번 파일을 읽고 지문을 만든다. 파일을 쓸 수 없으면 모두 코드 기본값을 쓴다.
        /// </summary>
        private static void EnsureLoaded()
        {
            if (values != null)
                return;

            if (!DataFiles.TryLoad<Dictionary<string, float>>(FilePath, Extension, TryParse, out values))
            {
                Debug.LogWarning($"[GameTuning] {FilePath}{Extension}을 사용할 수 없어 코드 기본값을 사용합니다.");
                values = new Dictionary<string, float>();
            }
            fingerprint = ComputeFingerprint(values);
        }

        /// <summary>
        /// "id,value,설명" 형식을 읽는다. 숫자가 아닌 값·모르는 id는 경고 후 건너뛴다. (한 줄 실수로 파일 전체를 버리지 않도록)
        /// </summary>
        private static bool TryParse(string text, out Dictionary<string, float> result)
        {
            result = new Dictionary<string, float>();
            HashSet<string> knownIds = GetKnownIds();

            foreach (string[] row in CsvParser.Parse(text))
            {
                if (row.Length < 2 || row[0] == "id")
                    continue;

                string id = row[0];
                if (!knownIds.Contains(id))
                {
                    Debug.LogWarning($"[GameTuning] 모르는 항목이라 무시합니다: {id} (이름 오타 확인)");
                    continue;
                }
                if (!float.TryParse(row[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    Debug.LogWarning($"[GameTuning] {id}의 값 '{row[1]}'이 숫자가 아니라 코드 기본값을 사용합니다.");
                    continue;
                }
                result[id] = value;
            }
            return true;
        }

        /// <summary>
        /// 이 클래스에 정의된 항목 이름(공개 정적 속성) 목록. 지문 속성은 제외한다.
        /// </summary>
        private static HashSet<string> GetKnownIds()
        {
            var ids = new HashSet<string>();
            foreach (PropertyInfo property in typeof(GameTuning).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                if (property.Name != nameof(Fingerprint))
                    ids.Add(property.Name);
            }
            return ids;
        }

        /// <summary>
        /// 항목을 이름순으로 이어 붙여 FNV-1a 해시를 만든다. (설명·순서·주석이 바뀌어도 값이 같으면 지문이 같음)
        /// </summary>
        private static string ComputeFingerprint(Dictionary<string, float> fileValues)
        {
            var ids = new List<string>(fileValues.Keys);
            ids.Sort(System.StringComparer.Ordinal);

            var builder = new StringBuilder();
            foreach (string id in ids)
                builder.Append(id).Append('=').Append(fileValues[id].ToString("R", CultureInfo.InvariantCulture)).Append(';');

            uint hash = 2166136261;
            foreach (char c in builder.ToString())
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }
}
