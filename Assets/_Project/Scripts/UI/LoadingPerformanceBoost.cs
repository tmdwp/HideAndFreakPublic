using UnityEngine;

namespace GuardVsMannequin.UI
{
    /// <summary>씬 로딩을 빠르게 하도록 로딩 중에만 비동기 로딩 설정을 올리고 끝나면 되돌린다.</summary>
    public static class LoadingPerformanceBoost
    {
        private const int BoostedUploadTimeSliceMs = 8;
        private const int BoostedUploadBufferMb = 64;

        private static bool isActive;
        private static ThreadPriority savedPriority;
        private static int savedTimeSlice;
        private static int savedBufferSize;

        /// <summary>두 번 켜면 원래 값을 잃으므로 켜져 있으면 무시하고, 이미 큰 값은 줄이지 않는다.</summary>
        public static void Begin()
        {
            if (isActive)
                return;
            isActive = true;

            savedPriority = Application.backgroundLoadingPriority;
            savedTimeSlice = QualitySettings.asyncUploadTimeSlice;
            savedBufferSize = QualitySettings.asyncUploadBufferSize;

            Application.backgroundLoadingPriority = ThreadPriority.High;
            QualitySettings.asyncUploadTimeSlice = Mathf.Max(savedTimeSlice, BoostedUploadTimeSliceMs);
            QualitySettings.asyncUploadBufferSize = Mathf.Max(savedBufferSize, BoostedUploadBufferMb);
        }

        /// <summary>
        /// 게임 중에도 가속이 남으면 프레임이 떨어지므로 로딩이 끝나면 저장해 둔 값으로 되돌린다.
        /// </summary>
        public static void End()
        {
            if (!isActive)
                return;
            isActive = false;

            Application.backgroundLoadingPriority = savedPriority;
            QualitySettings.asyncUploadTimeSlice = savedTimeSlice;
            QualitySettings.asyncUploadBufferSize = savedBufferSize;
        }
    }
}
