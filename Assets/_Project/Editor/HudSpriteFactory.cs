using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>
    /// HUD 아이콘 스프라이트(조준점 게이지 고리, 손전등, 발각 눈)를 흰색 모양으로 그려 저장한다. 색은 HUD가 칠한다.
    /// 파일이 이미 있으면 그대로 두고 가져오기 설정만 스프라이트로 맞춘다. 직접 그린 그림으로 바꿔도 된다.
    /// </summary>
    public static class HudSpriteFactory
    {
        public const string AmmoGaugePath = "Assets/_Project/Art/UI/Hud_AmmoGauge.png";
        public const string FlashlightPath = "Assets/_Project/Art/UI/Hud_Flashlight.png";
        public const string RevealedPath = "Assets/_Project/Art/UI/Hud_Revealed.png";

        private const string ArtFolder = "Assets/_Project/Art";
        private const string UiFolderName = "UI";
        // 가장자리를 부드럽게 하려고 한 픽셀을 나눠 보는 수.
        private const int SuperSample = 4;

        /// <summary>
        /// 탄 게이지용 원형 고리(96px, 두께 8px).
        /// </summary>
        public static Sprite GetAmmoGauge()
        {
            const float outer = 46f;
            const float inner = outer - 8f;
            return GetOrCreate(AmmoGaugePath, 96, (x, y) =>
            {
                float distance = Mathf.Sqrt(x * x + y * y);
                return distance <= outer && distance > inner;
            });
        }

        /// <summary>
        /// 오른쪽을 비추는 손전등(64px): 손잡이, 스위치, 넓어지는 머리, 렌즈.
        /// </summary>
        public static Sprite GetFlashlight()
        {
            return GetOrCreate(FlashlightPath, 64, (x, y) =>
            {
                bool body = x >= -26f && x <= 2f && Mathf.Abs(y) <= 6f;
                bool button = x >= -14f && x <= -6f && y > 6f && y <= 9f;
                float headT = Mathf.InverseLerp(2f, 20f, x);
                bool head = x > 2f && x <= 20f && Mathf.Abs(y) <= Mathf.Lerp(7f, 14f, headT);
                bool lens = x > 20f && x <= 25f && Mathf.Abs(y) <= 14f;
                return body || button || head || lens;
            });
        }

        /// <summary>
        /// 발각 표시용 눈(64px): 아몬드 모양 테두리와 눈동자.
        /// </summary>
        public static Sprite GetRevealed()
        {
            return GetOrCreate(RevealedPath, 64, (x, y) =>
            {
                bool outer = IsInsideAlmond(x, y, 29f, 18f);
                bool inner = IsInsideAlmond(x, y, 23f, 12f);
                bool pupil = x * x + y * y <= 8.5f * 8.5f;
                return (outer && !inner) || pupil;
            });
        }

        /// <summary>
        /// 두 원호를 맞댄 아몬드(눈) 모양 안인지. 가로 반폭 halfWidth, 가운데 반높이 halfHeight.
        /// </summary>
        private static bool IsInsideAlmond(float x, float y, float halfWidth, float halfHeight)
        {
            if (Mathf.Abs(x) > halfWidth)
                return false;
            float t = x / halfWidth;
            return Mathf.Abs(y) <= halfHeight * (1f - t * t);
        }

        /// <summary>
        /// 스프라이트를 가져온다. 없으면 정사각형 크기로 모양을 그려 저장한다.
        /// </summary>
        /// <param name="isInside">중심 기준 좌표(픽셀)가 모양 안인지.</param>
        private static Sprite GetOrCreate(string path, int size, Func<float, float, bool> isInside)
        {
            if (!File.Exists(path))
            {
                if (!AssetDatabase.IsValidFolder($"{ArtFolder}/{UiFolderName}"))
                    AssetDatabase.CreateFolder(ArtFolder, UiFolderName);
                File.WriteAllBytes(path, Draw(size, isInside).EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            EnsureSpriteImport(path);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// 가져오기 설정을 UI 스프라이트(밉맵 없음, 투명 유지)로 맞춘다.
        /// </summary>
        private static void EnsureSpriteImport(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 모양 안을 흰색으로 칠한다. 한 픽셀을 여러 점으로 나눠 덮인 비율을 투명도로 쓴다.
        /// </summary>
        private static Texture2D Draw(int size, Func<float, float, bool> isInside)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < SuperSample; sy++)
                    {
                        for (int sx = 0; sx < SuperSample; sx++)
                        {
                            float px = x + (sx + 0.5f) / SuperSample - half;
                            float py = y + (sy + 0.5f) / SuperSample - half;
                            if (isInside(px, py))
                                covered++;
                        }
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, covered / (float)(SuperSample * SuperSample));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
