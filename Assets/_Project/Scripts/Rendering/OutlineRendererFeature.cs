using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GuardVsMannequin.Rendering
{
    /// <summary>오클루전과 상관없이 외곽선을 그리도록 마스크를 그린 뒤 가장자리를 합성하는 URP Renderer Feature.</summary>
    public class OutlineRendererFeature : ScriptableRendererFeature
    {
        /// <summary>인스펙터 설정.</summary>
        [Serializable]
        public class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
            public Color aimColor = new(1f, 0.85f, 0.2f, 1f);
            public Color revealColor = new(1f, 0.2f, 0.15f, 1f);

            [Tooltip("외곽선 두께(픽셀).")]
            [Range(1f, 6f)] public float thickness = 2.5f;

            [Tooltip("Hidden/GuardVsMannequin/OutlineMask. 빌드에 포함되도록 직접 연결한다.")]
            public Shader maskShader;

            [Tooltip("Hidden/GuardVsMannequin/OutlineComposite. 빌드에 포함되도록 직접 연결한다.")]
            public Shader compositeShader;
        }

        public Settings settings = new();

        private OutlinePass pass;
        private Material aimMaskMaterial;
        private Material revealMaskMaterial;
        private Material compositeMaterial;

        /// <summary>설정을 바꾸면 Unity가 다시 부르므로 이전 머티리얼을 먼저 해제한다.</summary>
        public override void Create()
        {
            ReleaseMaterials();
            if (settings.maskShader == null || settings.compositeShader == null)
                return;

            aimMaskMaterial = CoreUtils.CreateEngineMaterial(settings.maskShader);
            aimMaskMaterial.SetColor(OutlinePass.MaskColorId, new Color(1f, 0f, 0f, 1f));
            revealMaskMaterial = CoreUtils.CreateEngineMaterial(settings.maskShader);
            revealMaskMaterial.SetColor(OutlinePass.MaskColorId, new Color(0f, 1f, 0f, 1f));
            compositeMaterial = CoreUtils.CreateEngineMaterial(settings.compositeShader);

            pass = new OutlinePass(OutlineRegistry.Instance, aimMaskMaterial, revealMaskMaterial, compositeMaterial)
            {
                renderPassEvent = settings.passEvent
            };
        }

        /// <summary>반사·미리보기 카메라에 그리는 낭비를 막도록 게임·씬 카메라에만 넣는다.</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;

            compositeMaterial.SetColor(OutlinePass.AimColorId, settings.aimColor);
            compositeMaterial.SetColor(OutlinePass.RevealColorId, settings.revealColor);
            compositeMaterial.SetFloat(OutlinePass.ThicknessId, settings.thickness);
            renderer.EnqueuePass(pass);
        }

        /// <summary>
        /// 엔진 머티리얼은 자동으로 해제되지 않으므로 기능이 사라질 때 지운다.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            ReleaseMaterials();
        }

        /// <summary>Create와 Dispose가 같은 정리를 쓰도록 한곳에 둔다.</summary>
        private void ReleaseMaterials()
        {
            CoreUtils.Destroy(aimMaskMaterial);
            CoreUtils.Destroy(revealMaskMaterial);
            CoreUtils.Destroy(compositeMaterial);
            aimMaskMaterial = revealMaskMaterial = compositeMaterial = null;
            pass = null;
        }

        /// <summary>
        /// 마스크를 그리고 외곽선을 합성하는 패스.
        /// </summary>
        private sealed class OutlinePass : ScriptableRenderPass
        {
            public static readonly int MaskColorId = Shader.PropertyToID("_MaskColor");
            public static readonly int AimColorId = Shader.PropertyToID("_AimColor");
            public static readonly int RevealColorId = Shader.PropertyToID("_RevealColor");
            public static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
            private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");

            private readonly IOutlineSource source;
            private readonly Material aimMask;
            private readonly Material revealMask;
            private readonly Material composite;

            /// <summary>RenderGraph의 정적 렌더 함수가 패스 객체를 붙잡지 않도록 필요한 값만 넘긴다.</summary>
            private class MaskPassData
            {
                public IOutlineSource source;
                public Material aimMask;
                public Material revealMask;
            }

            /// <summary>RenderGraph의 정적 렌더 함수가 패스 객체를 붙잡지 않도록 필요한 값만 넘긴다.</summary>
            private class CompositePassData
            {
                public TextureHandle mask;
                public Material composite;
                public Vector4 texelSize;
            }

            /// <summary>패스가 외곽선 목록 구현을 몰라도 되도록 공급원 인터페이스로 받는다.</summary>
            public OutlinePass(IOutlineSource source, Material aimMask, Material revealMask, Material composite)
            {
                this.source = source;
                this.aimMask = aimMask;
                this.revealMask = revealMask;
                this.composite = composite;
                profilingSampler = new ProfilingSampler("Outline");
            }

            /// <summary>외곽선이 없는 프레임에 비용이 들지 않도록 그릴 것이 있을 때만 패스를 기록한다.</summary>
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!HasAnyEntry())
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle cameraColor = resourceData.activeColorTexture;
                if (!cameraColor.IsValid())
                    return;

                TextureDesc desc = renderGraph.GetTextureDesc(cameraColor);
                desc.name = "_OutlineMask";
                desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.filterMode = FilterMode.Point;
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                TextureHandle mask = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Outline Mask", out var maskData, profilingSampler))
                {
                    maskData.source = source;
                    maskData.aimMask = aimMask;
                    maskData.revealMask = revealMask;
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    // 마스크를 읽는 합성 패스와 RenderGraph가 연결을 못 보면 이 패스를 지워 버리므로 막는다.
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) => DrawMask(data, context.cmd));
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Outline Composite", out var compositeData, profilingSampler))
                {
                    compositeData.mask = mask;
                    compositeData.composite = composite;
                    compositeData.texelSize = new Vector4(1f / Mathf.Max(1, desc.width), 1f / Mathf.Max(1, desc.height), 0f, 0f);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        data.composite.SetVector(MaskTexelSizeId, data.texelSize);
                        Blitter.BlitTexture(context.cmd, data.mask, new Vector4(1f, 1f, 0f, 0f), data.composite, 0);
                    });
                }
            }

            /// <summary>
            /// 목록 전체를 셀 필요 없이 하나라도 있는지만 보면 되어서 첫 항목에서 바로 돌아온다.
            /// </summary>
            private bool HasAnyEntry()
            {
                foreach (OutlineEntry _ in source.Entries)
                    return true;
                return false;
            }

            /// <summary>합성 셰이더가 종류를 구분하도록 종류별 색으로 그리고, 꺼진 렌더러는 건너뛴다.</summary>
            private static void DrawMask(MaskPassData data, RasterCommandBuffer cmd)
            {
                foreach (OutlineEntry entry in data.source.Entries)
                {
                    Material material = entry.Kind == OutlineKind.Reveal ? data.revealMask : data.aimMask;
                    foreach (Renderer targetRenderer in entry.Renderers)
                    {
                        if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                            continue;

                        int subMeshCount = GetSubMeshCount(targetRenderer);
                        for (int i = 0; i < subMeshCount; i++)
                            cmd.DrawRenderer(targetRenderer, material, i, 0);
                    }
                }
            }

            /// <summary>여러 재질로 된 사물 전체에 외곽선이 생기도록 서브메시 수를 센다.</summary>
            private static int GetSubMeshCount(Renderer targetRenderer)
            {
                Mesh mesh = targetRenderer switch
                {
                    SkinnedMeshRenderer skinned => skinned.sharedMesh,
                    MeshRenderer => targetRenderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null,
                    _ => null
                };
                return mesh != null ? Mathf.Max(1, mesh.subMeshCount) : 1;
            }
        }
    }
}
