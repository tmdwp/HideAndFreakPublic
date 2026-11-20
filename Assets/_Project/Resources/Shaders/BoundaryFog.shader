// 맵 경계 안개: 나갈 수 없는 영역을 흰 반투명 안개로 가린다. (BoundaryFog가 실행 중 붙이는 면 전용)
// 면이 수십 m로 커서 정점 단위 계산은 어긋나므로 잡음·장면 안개를 모두 픽셀에서 월드 위치로 계산한다.
Shader "GuardVsMannequin/BoundaryFog"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.25
        _NoiseScale ("Noise Scale", Float) = 0.12
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.4
        _ScrollSpeed ("Scroll Speed", Float) = 0.25
        _NearFadeDistance ("Near Fade Distance", Float) = 2
        _FarFadeStart ("Far Fade Start", Float) = 10
        _FarFadeEnd ("Far Fade End", Float) = 30
        _SoftDistance ("Soft Distance", Float) = 3
        _TopFade ("Top Fade", Range(0.01, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BoundaryFog"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                float _NoiseScale;
                half _NoiseStrength;
                float _ScrollSpeed;
                float _NearFadeDistance;
                float _FarFadeStart;
                float _FarFadeEnd;
                float _SoftDistance;
                half _TopFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            // 텍스처 없이도 벽 방향과 상관없이 이어지도록 3D 값 잡음을 쓴다.
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float bottom = lerp(lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), f.x),
                                    lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), f.x), f.y);
                float top = lerp(lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), f.x),
                                 lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + float3(1, 1, 1)), f.x), f.y);
                return lerp(bottom, top, f.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 flow = float3(0.6, 0.15, 0.4) * (_Time.y * _ScrollSpeed);
                float3 samplePoint = input.positionWS * _NoiseScale + flow;
                float noise = ValueNoise(samplePoint) * 0.65 + ValueNoise(samplePoint * 2.3 - flow * 1.7) * 0.35;
                half alpha = _Opacity * lerp(1.0 - _NoiseStrength, 1.0, noise);

                // 면이 땅·물체를 뚫고 지나가는 자리에 딱딱한 선이 생기지 않도록, 면 바로 뒤에 있는 것일수록 옅게 덮는다.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fogEyeDepth = -TransformWorldToView(input.positionWS).z;
                alpha *= saturate((sceneEyeDepth - fogEyeDepth) / max(_SoftDistance, 0.01));

                // 벽에 붙었을 때 눈앞의 면이 칼같이 잘려 보이지 않도록 가까울수록 옅게 한다.
                float cameraDistance = distance(GetCameraPositionWS(), input.positionWS);
                alpha *= smoothstep(0.0, max(_NearFadeDistance, 0.01), cameraDistance);

                // 멀리서 맵 끝이 흰 벽처럼 드러나지 않도록, 경계에 다가갈 때만 안개가 보이게 한다.
                alpha *= 1.0 - smoothstep(_FarFadeStart, max(_FarFadeEnd, _FarFadeStart + 0.01), cameraDistance);

                // 면 위쪽 끝이 하늘에 선으로 보이지 않도록 위로 갈수록 사라지게 한다.
                alpha *= 1.0 - smoothstep(1.0 - _TopFade, 1.0, input.uv.y);

                // 사냥 시간 검은 안개처럼 장면 안개가 바뀌면 멀리 있는 경계 안개도 함께 묻히도록 섞는다.
                float fogFactor = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
                half3 color = MixFog(_Color.rgb, fogFactor);
                return half4(color, saturate(alpha * _Color.a));
            }
            ENDHLSL
        }
    }
}
