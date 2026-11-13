using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GuardVsMannequin.Editor
{
    /// <summary>HUD가 색을 칠할 수 있도록 아이콘 스프라이트를 흰색 모양으로 그려 저장한다.</summary>
    public static class HudSpriteFactory
    {
        public const string AmmoGaugePath = "Assets/_Project/Art/UI/Hud_AmmoGauge.png";
        public const string FlashlightPath = "Assets/_Project/Art/UI/Hud_Flashlight.png";
        public const string RevealedPath = "Assets/_Project/Art/UI/Hud_Revealed.png";

        private const string ArtFolder = "Assets/_Project/Art";
        private const string UiFolderName = "UI";
        // 가장자리를 부드럽게 하려고 한 픽셀을 나눠 보는 수.
        private const int SuperSample = 4;

        /// <summary>시선을 옮기지 않고 탄을 볼 수 있도록 조준점 둘레의 가는 원형 고리로 만든다.</summary>
        public static Sprite GetAmmoGauge()
        {
            const float outerRadius = 46f;
            const float innerRadius = outerRadius - 8f;
            return GetOrCreate(AmmoGaugePath, 96, (x, y) =>
            {
                float distance = Mathf.Sqrt(x * x + y * y);
                return distance <= outerRadius && distance > innerRadius;
            });
        }

        /// <summary>작은 크기에서도 손전등으로 알아보도록 손잡이·머리·렌즈로 실루엣을 만든다.</summary>
        public static Sprite GetFlashlight()
        {
            return GetOrCreate(FlashlightPath, 64, (x, y) =>
            {
                bool isBody = x >= -26f && x <= 2f && Mathf.Abs(y) <= 6f;
                bool isButton = x >= -14f && x <= -6f && y > 6f && y <= 9f;
                float headProgress = Mathf.InverseLerp(2f, 20f, x);
                bool isHead = x > 2f && x <= 20f && Mathf.Abs(y) <= Mathf.Lerp(7f, 14f, headProgress);
                bool isLens = x > 20f && x <= 25f && Mathf.Abs(y) <= 14f;
                return isBody || isButton || isHead || isLens;
            });
        }

        /// <summary>"보이고 있다"는 뜻이 바로 전달되도록 눈 모양을 만든다.</summary>
        public static Sprite GetRevealed()
        {
            return GetOrCreate(RevealedPath, 64, (x, y) =>
            {
                bool isInsideOuter = IsInsideAlmond(x, y, 29f, 18f);
                bool isInsideInner = IsInsideAlmond(x, y, 23f, 12f);
                bool isPupil = x * x + y * y <= 8.5f * 8.5f;
                return (isInsideOuter && !isInsideInner) || isPupil;
            });
        }

        /// <summary>
        /// 눈 모양은 양 끝이 뾰족해야 하므로 가운데에서 끝으로 갈수록 높이가 제곱으로 줄어드는 곡선을 쓴다.
        /// </summary>
        private static bool IsInsideAlmond(float x, float y, float halfWidth, float halfHeight)
        {
            if (Mathf.Abs(x) > halfWidth)
                return false;
            float normalizedX = x / halfWidth;
            return Mathf.Abs(y) <= halfHeight * (1f - normalizedX * normalizedX);
        }

        /// <summary>사용자가 직접 그린 그림으로 바꿨을 수 있어 파일이 있으면 덮어쓰지 않는다.</summary>
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

        /// <summary>기본 텍스처 설정이면 Image에 넣을 수 없고 밉맵이 작은 아이콘을 흐리게 하므로 UI 스프라이트 설정으로 맞춘다.</summary>
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

        /// <summary>가장자리가 계단처럼 보이지 않도록 픽셀을 여러 점으로 나눠 덮인 비율을 투명도로 쓴다.</summary>
        private static Texture2D Draw(int size, Func<float, float, bool> isInside)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float halfSize = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int coveredSamples = 0;
                    for (int sampleY = 0; sampleY < SuperSample; sampleY++)
                    {
                        for (int sampleX = 0; sampleX < SuperSample; sampleX++)
                        {
                            float pointX = x + (sampleX + 0.5f) / SuperSample - halfSize;
                            float pointY = y + (sampleY + 0.5f) / SuperSample - halfSize;
                            if (isInside(pointX, pointY))
                                coveredSamples++;
                        }
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, coveredSamples / (float)(SuperSample * SuperSample));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
