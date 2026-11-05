Shader "Custom/RadialTurboBlur"
{
    Properties
    {
        _BlurStrength ("Blur Strength", Range(0.0, 1.0)) = 0.0
        _BlurWidth ("Blur Radial Width", Range(0.0, 0.5)) = 0.22
        _CenterFocusRadius ("Center Clear Radius", Range(0.05, 0.5)) = 0.16
        _TurboTint ("Turbo Edge Tint", Color) = (0.15, 0.75, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            sampler2D _CameraOpaqueTexture;
            float _BlurStrength;
            float _BlurWidth;
            float _CenterFocusRadius;
            fixed4 _TurboTint;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_BlurStrength <= 0.001)
                    return fixed4(0, 0, 0, 0);

                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                float2 center = float2(0.5, 0.5);
                float2 dir = center - screenUV;
                float dist = length(dir);

                // Mascara que preserva nitidez sobre o carro no centro e embaca a pista ao redor
                float edgeMask = smoothstep(_CenterFocusRadius, 0.65, dist);
                float finalBlurFactor = _BlurStrength * _BlurWidth * edgeMask;

                const int SAMPLES = 12;
                fixed4 accumColor = fixed4(0, 0, 0, 0);

                // Amostragem radial com leve separacao cromatica nas bordas
                for (int s = 0; s < SAMPLES; s++)
                {
                    float scale = (float)s / (float)(SAMPLES - 1);
                    float2 offset = dir * (scale * finalBlurFactor);
                    fixed r = tex2D(_CameraOpaqueTexture, saturate(screenUV + offset * 1.06)).r;
                    fixed g = tex2D(_CameraOpaqueTexture, saturate(screenUV + offset)).g;
                    fixed b = tex2D(_CameraOpaqueTexture, saturate(screenUV + offset * 0.94)).b;
                    accumColor += fixed4(r, g, b, 1.0);
                }
                accumColor /= (float)SAMPLES;

                // Leve brilho energetico nas bordas quando o turbo esta no apice
                accumColor.rgb = lerp(accumColor.rgb, _TurboTint.rgb, edgeMask * _BlurStrength * 0.18);
                float alpha = saturate(_BlurStrength * lerp(0.55, 0.96, edgeMask));
                return fixed4(accumColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
