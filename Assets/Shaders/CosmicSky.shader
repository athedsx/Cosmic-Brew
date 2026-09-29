// Skybox do Cosmic Brew: gradiente pastel (roxo -> azul marinho -> rosa) com estrelas piscando
Shader "Cosmic/SpaceSky"
{
    Properties
    {
        _TopColor ("Topo", Color) = (0.10, 0.07, 0.25, 1)
        _MidColor ("Meio", Color) = (0.20, 0.12, 0.35, 1)
        _BottomColor ("Base", Color) = (0.55, 0.30, 0.50, 1)
        _StarDensity ("Densidade de Estrelas", Range(20, 400)) = 180
        _StarAmount ("Quantidade de Estrelas", Range(0, 1)) = 0.035
        _StarBrightness ("Brilho das Estrelas", Range(0, 3)) = 1.2
        _TwinkleSpeed ("Velocidade do Brilho", Range(0, 5)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _MidColor;
                half4 _BottomColor;
                float _StarDensity;
                float _StarAmount;
                float _StarBrightness;
                float _TwinkleSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);

                // Gradiente vertical suave
                float h = d.y;
                half3 col = h > 0
                    ? lerp(_MidColor.rgb, _TopColor.rgb, smoothstep(0.0, 0.8, h))
                    : lerp(_MidColor.rgb, _BottomColor.rgb, smoothstep(0.0, 0.7, -h));

                // Estrelas procedurais em células 3D
                float3 p = d * _StarDensity;
                float3 cell = floor(p);
                float3 f = frac(p) - 0.5;
                float rnd = hash31(cell);
                if (rnd < _StarAmount)
                {
                    float3 offs = float3(hash31(cell + 1.7), hash31(cell + 5.3), hash31(cell + 9.1)) - 0.5;
                    float dist = length(f - offs * 0.6);
                    float star = smoothstep(0.12, 0.0, dist);
                    float twinkle = 0.6 + 0.4 * sin(_Time.y * _TwinkleSpeed * (1.0 + rnd * 30.0) + rnd * 100.0);
                    half3 tint = lerp(half3(1.0, 0.9, 0.95), half3(0.8, 0.9, 1.0), hash31(cell + 3.3));
                    col += tint * star * twinkle * _StarBrightness;
                }

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
