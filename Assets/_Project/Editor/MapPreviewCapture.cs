using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Editor
{
    /// <summary>맵 빌더 결과를 Unity를 열지 않고 확인하도록 맵을 임시 카메라로 찍어 PNG로 저장한다.</summary>
    internal static class MapPreviewCapture
    {
        private const int Width = 1600;
        private const int Height = 1000;

        /// <summary>한 장면: 이름, 카메라 위치, 바라볼 점.</summary>
        public readonly struct Shot
        {
            public readonly string name;
            public readonly Vector3 position;
            public readonly Vector3 target;

            /// <summary>맵 빌더마다 찍을 장면이 달라서 호출한 쪽이 장면 목록을 넘길 수 있게 값으로 묶는다.</summary>
            public Shot(string name, Vector3 position, Vector3 target)
            {
                this.name = name;
                this.position = position;
                this.target = target;
            }
        }

        /// <summary>배치는 위에서, 분위기는 눈높이에서 봐야 알 수 있어 두 종류를 함께 찍는다.</summary>
        /// <param name="hideFromTop">배치도를 찍는 동안 숨길 렌더러(천장 등).</param>
        public static List<string> Capture(string folder, Bounds area, IReadOnlyList<Renderer> hideFromTop, IReadOnlyList<Shot> shots)
        {
            Directory.CreateDirectory(folder);
            var savedPaths = new List<string>();
            var cameraObject = new GameObject("PreviewCamera (임시)");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;

                savedPaths.Add(CaptureTopDown(camera, folder, area, hideFromTop));
                camera.orthographic = false;
                camera.fieldOfView = 75f;
                foreach (Shot shot in shots)
                {
                    camera.transform.SetPositionAndRotation(shot.position, Quaternion.LookRotation(shot.target - shot.position));
                    savedPaths.Add(Render(camera, Path.Combine(folder, shot.name + ".png")));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
            return savedPaths;
        }

        /// <summary>천장과 어둠 때문에 배치가 안 보이므로 천장을 숨기고 환경광을 밝힌 채 정사영으로 찍는다.</summary>
        private static string CaptureTopDown(Camera camera, string folder, Bounds area, IReadOnlyList<Renderer> hiddenRenderers)
        {
            AmbientMode savedAmbientMode = RenderSettings.ambientMode;
            Color savedAmbientLight = RenderSettings.ambientLight;
            var wasEnabled = new List<bool>();
            foreach (Renderer hiddenRenderer in hiddenRenderers)
            {
                wasEnabled.Add(hiddenRenderer.enabled);
                hiddenRenderer.enabled = false;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.75f);

            try
            {
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(area.extents.z, area.extents.x * Height / Width) + 1f;
                camera.transform.SetPositionAndRotation(new Vector3(area.center.x, area.max.y + 30f, area.center.z), Quaternion.Euler(90f, 0f, 0f));
                return Render(camera, Path.Combine(folder, "00_TopDown.png"));
            }
            finally
            {
                RenderSettings.ambientMode = savedAmbientMode;
                RenderSettings.ambientLight = savedAmbientLight;
                for (int i = 0; i < hiddenRenderers.Count; i++)
                    hiddenRenderers[i].enabled = wasEnabled[i];
            }
        }

        /// <summary>URP RenderGraph에서는 camera.Render()가 동작하지 않을 수 있어 렌더 요청을 먼저 쓴다.</summary>
        private static string Render(Camera camera, string path)
        {
            var renderTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousActive = RenderTexture.active;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                var renderRequest = new RenderPipeline.StandardRequest { destination = renderTarget };
                if (RenderPipeline.SupportsRenderRequest(camera, renderRequest))
                {
                    RenderPipeline.SubmitRenderRequest(camera, renderRequest);
                }
                else
                {
                    camera.targetTexture = renderTarget;
                    camera.Render();
                    camera.targetTexture = null;
                }

                RenderTexture.active = renderTarget;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return path;
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(image);
                renderTarget.Release();
                Object.DestroyImmediate(renderTarget);
            }
        }
    }
}
