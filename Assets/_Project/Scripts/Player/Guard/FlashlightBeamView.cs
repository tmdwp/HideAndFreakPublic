using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player.Guard
{
    /// <summary>
    /// 스포트라이트는 비춘 바닥만 밝힐 뿐 빛을 든 사람은 어둠 속에 그대로라, 다른 사람 화면에서 켜진 손전등이 어디 있는지 보이도록
    /// 렌즈 빛과 희미한 빛줄기를 그린다. 1인칭 본인 화면에는 시야를 가리므로 쓰지 않는다.
    /// </summary>
    public class FlashlightBeamView : MonoBehaviour
    {
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

        // 빛줄기 원뿔의 둘레·길이 방향 나눔 수. 길이 방향을 나눠야 끝으로 갈수록 부드럽게 흐려진다.
        private const int ConeSegments = 24;
        private const int ConeRings = 8;

        // 원뿔 표면을 비스듬히 볼수록(가장자리) 투명하게 하는 정도. 클수록 가장자리가 더 빨리 사라진다.
        private const float EdgeSoftness = 2f;

        // 손전등 뒤쪽에서 볼 때도 렌즈가 조금은 보이도록 남기는 밝기.
        private const float LensBackGlow = 0.08f;

        private static Material fallbackMaterial;

        // 매 프레임 카메라 목록 배열을 새로 만들지 않도록 재사용한다.
        private static readonly Camera[] cameraBuffer = new Camera[8];

        private MeshFilter beamFilter;
        private MeshRenderer beamRenderer;
        private MeshRenderer lensRenderer;
        private Transform lensTransform;
        private Mesh beamMesh;
        private Mesh lensMesh;

        private Vector3[] beamVertices;
        private Vector3[] beamNormals;
        private float[] beamLengthFade;
        private Color[] beamColors;
        private Color[] lensColors;
        private Light sourceLight;
        private Color lightColor;
        private float beamAlpha;
        private bool isVisible;

        /// <summary>
        /// 손전등 수치가 바뀌어도 맞도록 빛의 길이·각도를 받아 모양을 만든다.
        /// </summary>
        /// <param name="material">미리 구운 머티리얼. 없으면 실행 중에 같은 설정으로 만든다.</param>
        public static FlashlightBeamView Create(Light spotLight, Material material, float beamLength, float beamAlpha, float lensSize)
        {
            Material beamMaterial = material != null ? material : GetFallbackMaterial();
            if (beamMaterial == null)
                return null;

            var view = spotLight.gameObject.AddComponent<FlashlightBeamView>();
            view.sourceLight = spotLight;
            view.lightColor = spotLight.color;
            view.beamAlpha = beamAlpha;
            view.BuildBeam(spotLight.transform, beamMaterial, beamLength, spotLight.spotAngle * 0.5f);
            view.BuildLens(spotLight.transform, beamMaterial, lensSize);
            view.SetVisible(false);
            return view;
        }

        /// <summary>
        /// 손전등이 켜졌을 때만 보이도록 바깥에서 켜고 끈다.
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (beamRenderer != null)
                beamRenderer.enabled = visible && beamAlpha > 0f;
            if (lensRenderer != null)
                lensRenderer.enabled = visible;
        }

        /// <summary>
        /// 원뿔은 보는 방향에 따라 가장자리가 딱딱하게 보이므로 보는 카메라 기준으로 투명도를 다시 칠하고, 렌즈는 카메라를 향하게 한다.
        /// </summary>
        private void LateUpdate()
        {
            if (!isVisible)
                return;
            // 외형이 손전등 색을 바꿀 수 있어 매번 빛 색을 따른다.
            if (sourceLight != null)
                lightColor = sourceLight.color;
            Camera viewer = FindViewer();
            if (viewer == null)
                return;

            Vector3 viewerPosition = viewer.transform.position;
            if (beamRenderer != null && beamRenderer.enabled)
                UpdateBeamColors(viewerPosition);
            UpdateLens(viewerPosition);
        }

        /// <summary>
        /// 표면을 정면으로 볼수록 진하고 비스듬히 볼수록 투명하게 해, 단면이 아니라 빛이 퍼진 덩어리처럼 보이게 한다.
        /// </summary>
        private void UpdateBeamColors(Vector3 viewerWorldPosition)
        {
            Vector3 viewerLocal = beamFilter.transform.InverseTransformPoint(viewerWorldPosition);
            for (int i = 0; i < beamVertices.Length; i++)
            {
                Vector3 toViewer = (viewerLocal - beamVertices[i]).normalized;
                float facing = Mathf.Abs(Vector3.Dot(beamNormals[i], toViewer));
                float alpha = beamAlpha * beamLengthFade[i] * Mathf.Pow(facing, EdgeSoftness);
                beamColors[i] = new Color(lightColor.r, lightColor.g, lightColor.b, alpha);
            }
            beamMesh.colors = beamColors;
        }

        /// <summary>
        /// 손전등을 정면에서 볼 때만 눈부시게 보이고 뒤에서는 거의 안 보이도록 렌즈 밝기를 보는 방향에 맞춘다.
        /// </summary>
        private void UpdateLens(Vector3 viewerWorldPosition)
        {
            Vector3 toViewer = viewerWorldPosition - lensTransform.position;
            if (toViewer.sqrMagnitude < 0.0001f)
                return;
            lensTransform.rotation = Quaternion.LookRotation(-toViewer);

            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, toViewer.normalized));
            float brightness = Mathf.Lerp(LensBackGlow, 1f, facing * facing);
            lensColors[0] = new Color(lightColor.r, lightColor.g, lightColor.b, brightness);
            lensMesh.colors = lensColors;
        }

        /// <summary>
        /// 아바타 카메라에 MainCamera 태그가 없을 수 있어, 없으면 지금 켜진 카메라 중 첫 번째를 쓴다.
        /// </summary>
        private static Camera FindViewer()
        {
            Camera main = Camera.main;
            if (main != null)
                return main;
            int count = Camera.GetAllCameras(cameraBuffer);
            return count > 0 ? cameraBuffer[0] : null;
        }

        /// <summary>에디터 미리 굽기도 같은 설정의 머티리얼을 만들도록 공개한다. 빛처럼 보이도록 더하기 혼합을 쓴다.</summary>
        public static Material CreateDefaultMaterial()
        {
            Shader shader = Shader.Find(ParticleShaderName);
            if (shader == null)
                return null;

            var material = new Material(shader) { name = "M_FlashlightBeam" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetColor("_BaseColor", Color.white);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        /// <summary>
        /// 가드마다 머티리얼을 새로 만들지 않도록 실행 중 대체 머티리얼은 하나만 만든다.
        /// </summary>
        private static Material GetFallbackMaterial()
        {
            if (fallbackMaterial == null)
                fallbackMaterial = CreateDefaultMaterial();
            if (fallbackMaterial == null)
                Debug.LogWarning($"[FlashlightBeamView] {ParticleShaderName} 셰이더가 없어 손전등 빛줄기를 그리지 않습니다.");
            return fallbackMaterial;
        }

        /// <summary>
        /// 손전등 앞(+Z)으로 퍼지는 원뿔을 여러 고리로 나눠 만든다. 길이 방향 흐려짐은 미리 계산해 두고 매 프레임 보는 방향만 곱한다.
        /// </summary>
        private void BuildBeam(Transform parent, Material material, float length, float halfAngle)
        {
            float tanHalf = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
            int vertexCount = (ConeRings + 1) * ConeSegments;
            beamVertices = new Vector3[vertexCount];
            beamNormals = new Vector3[vertexCount];
            beamLengthFade = new float[vertexCount];
            beamColors = new Color[vertexCount];
            var triangles = new int[ConeRings * ConeSegments * 6];

            // 원뿔 표면의 바깥 방향: 둘레 방향으로 퍼지면서 뒤(-Z)로 기운다.
            float normalBack = -tanHalf / Mathf.Sqrt(1f + tanHalf * tanHalf);
            float normalOut = 1f / Mathf.Sqrt(1f + tanHalf * tanHalf);
            for (int ring = 0; ring <= ConeRings; ring++)
            {
                float t = (float)ring / ConeRings;
                float z = length * t;
                // 꼭짓점으로 모으면 가운데가 너무 밝아져 시작부를 작은 원으로 둔다.
                float radius = Mathf.Max(0.03f, z * tanHalf);
                // 렌즈 바로 앞은 렌즈 빛과 겹쳐 하얗게 뭉치므로 조금 띄워 시작하고, 끝으로 갈수록 부드럽게 사라지게 한다.
                float fade = Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - t) * (1f - t);
                for (int segment = 0; segment < ConeSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / ConeSegments;
                    var around = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    int index = ring * ConeSegments + segment;
                    beamVertices[index] = new Vector3(around.x * radius, around.y * radius, z);
                    beamNormals[index] = new Vector3(around.x * normalOut, around.y * normalOut, normalBack);
                    beamLengthFade[index] = fade;
                }
            }

            int triangle = 0;
            for (int ring = 0; ring < ConeRings; ring++)
            {
                for (int segment = 0; segment < ConeSegments; segment++)
                {
                    int current = ring * ConeSegments + segment;
                    int next = ring * ConeSegments + (segment + 1) % ConeSegments;
                    triangles[triangle++] = current;
                    triangles[triangle++] = current + ConeSegments;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next;
                    triangles[triangle++] = current + ConeSegments;
                    triangles[triangle++] = next + ConeSegments;
                }
            }

            beamMesh = new Mesh { name = "FlashlightBeamCone", vertices = beamVertices, colors = beamColors, triangles = triangles };
            beamMesh.MarkDynamic();
            beamMesh.RecalculateBounds();
            beamRenderer = CreatePart(parent, "FlashlightBeam", beamMesh, material, out beamFilter);
        }

        /// <summary>
        /// 멀리서도 빛나는 점으로 보이도록 가운데가 밝고 가장자리가 투명한 작은 원판을 만든다.
        /// </summary>
        private void BuildLens(Transform parent, Material material, float size)
        {
            const int segments = 16;
            var vertices = new Vector3[segments + 1];
            lensColors = new Color[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            lensColors[0] = new Color(lightColor.r, lightColor.g, lightColor.b, 1f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size;
                lensColors[i + 1] = new Color(lightColor.r, lightColor.g, lightColor.b, 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            lensMesh = new Mesh { name = "FlashlightLensGlow", vertices = vertices, colors = lensColors, triangles = triangles };
            lensMesh.MarkDynamic();
            lensMesh.RecalculateBounds();
            lensRenderer = CreatePart(parent, "FlashlightLensGlow", lensMesh, material, out MeshFilter lensFilter);
            lensTransform = lensFilter.transform;
        }

        /// <summary>
        /// 빛줄기·렌즈가 그림자를 만들거나 받지 않도록 끄고 손전등 아래 자식으로 붙인다.
        /// </summary>
        private static MeshRenderer CreatePart(Transform parent, string objectName, Mesh mesh, Material material, out MeshFilter filter)
        {
            var part = new GameObject(objectName);
            part.transform.SetParent(parent, false);
            filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return meshRenderer;
        }

        /// <summary>
        /// 실행 중에 만든 메시는 씬과 함께 지워지지 않으므로 직접 해제한다.
        /// </summary>
        private void OnDestroy()
        {
            if (beamMesh != null)
                Destroy(beamMesh);
            if (lensMesh != null)
                Destroy(lensMesh);
        }
    }
}
