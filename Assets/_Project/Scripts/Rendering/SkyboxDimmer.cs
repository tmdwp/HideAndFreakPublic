using UnityEngine;

namespace GuardVsMannequin.Rendering
{
    /// <summary>안개는 스카이박스에 적용되지 않아 시야를 줄여도 하늘만 밝게 남으므로, 하늘 노출을 따로 낮췄다가 되돌린다.</summary>
    public sealed class SkyboxDimmer
    {
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        private Material originalSkybox;
        private Material dimmedSkybox;
        private float originalExposure;

        /// <summary>맵 원래 하늘 에셋을 고치지 않도록 처음 어둡게 할 때 복사본을 만들어 바꿔 낀다.</summary>
        /// <param name="brightness">0이면 검은 하늘, 1이면 원래 밝기.</param>
        public void Dim(float brightness)
        {
            if (dimmedSkybox == null && !TryBegin())
                return;

            dimmedSkybox.SetFloat(ExposureId, originalExposure * Mathf.Clamp01(brightness));
        }

        /// <summary>하늘이 없거나 노출 값이 없는 셰이더는 밝기를 바꿀 방법이 없어서 그대로 둔다.</summary>
        private bool TryBegin()
        {
            Material current = RenderSettings.skybox;
            if (current == null || !current.HasProperty(ExposureId))
                return false;

            originalSkybox = current;
            originalExposure = current.GetFloat(ExposureId);
            dimmedSkybox = new Material(current) { name = $"{current.name} (Dimmed)" };
            RenderSettings.skybox = dimmedSkybox;
            return true;
        }

        /// <summary>연출이 끝나면 맵 원래 하늘로 돌려놓는다.</summary>
        public void Restore()
        {
            if (dimmedSkybox == null)
                return;

            RenderSettings.skybox = originalSkybox;
            Forget();
        }

        /// <summary>새 씬은 자기 하늘을 가지고 열리므로, 씬이 바뀌면 되돌리지 않고 복사본만 버린다.</summary>
        public void Forget()
        {
            if (dimmedSkybox != null)
                Object.Destroy(dimmedSkybox);
            dimmedSkybox = null;
            originalSkybox = null;
        }
    }
}
