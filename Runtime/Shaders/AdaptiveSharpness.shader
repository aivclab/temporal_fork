Shader "PDTAAFork/AdaptiveSharpness"{
    Properties {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _AdaptiveSharpnessPixelWidth ("Pixel Width", Float) = 1
        _AdaptiveSharpnessPixelHeight ("Pixel Height", Float) = 1
        _AdaptiveSharpnessStrength ("Strength", Range(0, 5.0)) = 0.60
        _AdaptiveSharpnessMagnitudeClamp ("Magnitude Clamp", Range(0, 1.0)) = 0.009
    }

    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off
            Fog { Mode off }

            CGPROGRAM

                #pragma vertex vert_img
                #pragma fragment frag
                #pragma fragmentoption ARB_precision_hint_fastest
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                sampler2D_half _CameraMotionVectorsTexture;
                float4 _CameraMotionVectorsTexture_TexelSize;
                // sampler2D_half _VelocityBuffer
                half _AdaptiveSharpnessPixelWidth;
                half _AdaptiveSharpnessPixelHeight;
                half _AdaptiveSharpnessStrength;
                half _AdaptiveSharpnessMagnitudeClamp;

                fixed4 frag(v2f_img i) : COLOR {
                    half2 xy_vel = tex2D(_CameraMotionVectorsTexture, i.uv).rg;
                    half2 uv_coord = i.uv;
                    half4 out_color = tex2D(_MainTex, uv_coord);

                    half4 sub_pixel_sampled  = tex2D(_MainTex, uv_coord + half2(0.5 *  _AdaptiveSharpnessPixelWidth,       -_AdaptiveSharpnessPixelHeight));
                          sub_pixel_sampled += tex2D(_MainTex, uv_coord + half2(      -_AdaptiveSharpnessPixelWidth, 0.5 * -_AdaptiveSharpnessPixelHeight));
                          sub_pixel_sampled += tex2D(_MainTex, uv_coord + half2(       _AdaptiveSharpnessPixelWidth, 0.5 *  _AdaptiveSharpnessPixelHeight));
                          sub_pixel_sampled += tex2D(_MainTex, uv_coord + half2(0.5 * -_AdaptiveSharpnessPixelWidth,        _AdaptiveSharpnessPixelHeight));
                    sub_pixel_sampled /= 4;

                    float magnitude = length(xy_vel* _CameraMotionVectorsTexture_TexelSize.zw);

                    float adaptive_weight = saturate(_AdaptiveSharpnessStrength * magnitude);

                    half4 luminance_adaptive = half4(0.2126, 0.7152, 0.0722, 0)*adaptive_weight;
                    half4 diff = out_color - sub_pixel_sampled;
                    half4 lum_diff_dot = dot(diff, luminance_adaptive);
                    out_color += clamp(lum_diff_dot, -_AdaptiveSharpnessMagnitudeClamp, _AdaptiveSharpnessMagnitudeClamp);

                    return out_color;
                }

            ENDCG
        }

    }

    FallBack off
}
