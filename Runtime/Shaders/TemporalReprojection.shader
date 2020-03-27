// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

Shader "Playdead/Post/TemporalReprojection"{
    Properties	{
        _MainTex ("_MainTex", 2D) = "white" {}
        _PrevTex ("_PrevTex", 2D) = "white" {}
        _VelocityBuffer ("_VelocityBuffer", 2D) = "white" {}
        _VelocityNeighborMax ("_VelocityNeighborMax", 2D) = "white" {}
        _FeedbackMin ("_FeedbackMin", float) = 0.9
        _FeedbackMax ("_FeedbackMax", float) = 0.9
        _MotionScale ("_MotionScale", float) = 0.9
        _AdaptiveClippingGamma ("_AdaptiveClippingGamma", float) = 0.9
        _AdaptiveClippingGammaMin ("_AdaptiveClippingGammaMin", float) = 0.9
        _PhasicVelocityWeight ("_PhasicVelocityWeight", float) = 0.0
        _ConstraintVelocityWeight("_ConstraintVelocityWeight", float)= 0.2
        _ClippingPhaseInFactor ("_ClippingPhaseInFactor", float) = 0.9
        _ClippingPhaseOutFactor ("_ClippingPhaseOutFactor", float) = 0.9
        _XBoxScalingFactor ("_XBoxScalingFactor", float) = 1.0
        _YBoxScalingFactor ("_YBoxScalingFactor", float) = 1.0
        _ZBoxScalingFactor ("_ZBoxScalingFactor", float) = 1.0
        _JimanezAfContrastMax ("_JimanezAfContrastMax", float) = 0.35
        _JimanezAfContrastMin ("_JimanezAfContrastMin", float) = 0.05
        _StachowiacAfContrastMin ("_StachowiacAfContrastMin", float) = 0.2
        _StachowiacAfContrastMax ("_StachowiacAfContrastMax", float) = 1.0
    }

    CGINCLUDE

    // Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
    #pragma exclude_renderers d3d11 gles
    // Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
    #pragma exclude_renderers gles
    //--- program begin

    //TVG MODIFICATION: EXCLUDED IN ORDER TO REDUCE BUILD TIME:
    //#pragma only_renderers d3d11
    //ORIGINAL:
    #pragma only_renderers ps4 xboxone d3d11 d3d9 xbox360 opengl glcore gles3 metal vulkan

    #pragma target 3.0


    //#include "MultiCompiles.cginc" // NOTE: SHOULD BE EMBEDDED HERE, NOT INCLUDED!
    #include "Flags.cginc"


    #include "UnityCG.cginc"
    #include "IncDepth.cginc"
    #include "IncNoise.cginc"
    #include "Clipping.cginc"
    #include "CatMullRom.cginc"
    #include "ChannelEncoding.cginc"

    #if SHADER_API_MOBILE
        static const float FLT_EPS = 0.0001f;
    #else
        static const float FLT_EPS = 1e-20;
    #endif

    uniform float4 _JitterUV;// frustum jitter uv deltas, where xy = current frame, zw = previous

    uniform sampler2D _MainTex;
    uniform float4 _MainTex_TexelSize;

    uniform sampler2D _PrevTex;
    uniform float4 _PrevTex_TexelSize;

    uniform sampler2D_half _VelocityBuffer;
    uniform float4 _VelocityBuffer_TexelSize;

    uniform sampler2D_half _VelocityNeighborMax;
    //uniform float4 _VelocityNeighborMax_TexelSize;

    uniform float _FeedbackMin;
    uniform float _FeedbackMax;
    uniform float _MotionScale;
    uniform float _AdaptiveBoxMin;
    uniform float _AdaptiveBoxMax;
    uniform float _PhasicVelocityWeight;
    uniform float _ConstraintVelocityWeight;
    uniform float _ClippingPhaseInFactor;
    uniform float _ClippingPhaseOutFactor;

    uniform float _JimanezAfContrastMin;
    uniform float _JimanezAfContrastMax;

    uniform float _StachowiacAfContrastMin;
    uniform float _StachowiacAfContrastMax;

    uniform float _XBoxScalingFactor;
    uniform float _YBoxScalingFactor;
    uniform float _ZBoxScalingFactor;

    struct v2f {
        float4 cs_pos : SV_POSITION;
        float2 ss_txc : TEXCOORD0;
    };

    v2f vert(appdata_img IN)	{
        v2f OUT;

        #if UNITY_VERSION < 540
            OUT.cs_pos = UnityObjectToClipPos(IN.vertex);
        #else
            OUT.cs_pos = UnityObjectToClipPos(IN.vertex);
        #endif
        #if UNITY_SINGLE_PASS_STEREO
            OUT.ss_txc = UnityStereoTransformScreenSpaceTex(IN.texcoord.xy);
        #else
            OUT.ss_txc = IN.texcoord.xy;
        #endif

        return OUT;
    }

    float2 sample_velocity_dilated(sampler2D tex, float2 uv, int support)	{
        float2 du = float2(_MainTex_TexelSize.x, 0.0);
        float2 dv = float2(0.0, _MainTex_TexelSize.y);
        float2 mv = 0.0;
        float rmv = 0.0;

        int end = support + 1;
        for (int i = -support; i != end; i++) {
            for (int j = -support; j != end; j++) {
                float2 v = tex2D(tex, uv + i * dv + j * du).xy;
                float rv = dot(v, v);
                if (rv > rmv){
                    mv = v;
                    rmv = rv;
                }
            }
        }

        return mv;
    }

    float4 sample_color_motion(sampler2D tex, float2 uv, float2 ss_vel)	{
        const float2 v = 0.5 * ss_vel;
        const int taps = 3;// on either side!

        float srand = PDsrand(uv + _SinTime.xx);
        float2 vtap = v / taps;
        float2 pos0 = uv + vtap * (0.5 * srand);
        float4 accu = 0.0;
        float wsum = 0.0;

        [unroll]
        for (int i = -taps; i <= taps; i++)	{
            float w = 1.0;// box
            //float w = taps - abs(i) + 1;// triangle
            //float w = 1.0 / (1 + abs(i));// pointy triangle
            accu += w * sample_color(tex, pos0 + i * vtap);
            wsum += w;
        }

        return accu / wsum;
    }

    float remap( float x, float x0, float x1, float o0, float o1 ){
        return o0 + (o1-o0) * clamp( (x-x0)/(x1-x0), 0.0, 1.0);
    }

	float eval_weight( float v ){
        return exp(-3.0 * v / 4.0);
    }

    float4 temporal_reprojection(float2 ss_txc, float2 ss_vel, float vs_dist, float max_neighbour_vel_mag){

        // read texels
        #if UNJITTER_CURRENT
            float4 texel_current = sample_color(_MainTex, ss_txc - _JitterUV.xy);
        #else
            float4 texel_current = sample_color(_MainTex, ss_txc);
        #endif

        float2 prev_txc = ss_txc - ss_vel;

        #if INTRPL_NONE
            float4 texel_history = sample_history(_PrevTex, prev_txc);
        #elif INTRPL_CATMULL_ROM
            float4 texel_history = sample_catmull_rom(_PrevTex, prev_txc, _PrevTex_TexelSize.zw);
        #elif INTRPL_CATMULL_ROM_CUBIC
            float4 texel_history = sample_catmull_rom_cubic(_PrevTex, prev_txc, _PrevTex_TexelSize.zw);
        #else
            #error "missing keyword INTRPL_..."
        #endif

        texel_history.rgb = late_history_color_transform(texel_history.rgb);

        float4 constrained_history = texel_history;
        float feedback_factor = 0.85;
        float3 out_color = (0.5).xxx;
        float out_alpha = 1.0;

        // calc min-max of current neighbourhood
        #if UNJITTER_NEIGHBORHOOD
            float2 uv = ss_txc - _JitterUV.xy;
        #else
            float2 uv = ss_txc;
        #endif

        #if MINMAX_3X3 || MINMAX_3X3_ROUNDED || MINMAX_3X3_WEIGHTED
            float2 du = float2(_MainTex_TexelSize.x, 0.0);
            float2 dv = float2(0.0, _MainTex_TexelSize.y);

            //float4 cmc = sample_color(_MainTex, uv);
            float4 cmc = texel_current;

            float4 ctl = sample_color(_MainTex, uv - dv - du);
            float4 ctc = sample_color(_MainTex, uv - dv);
            float4 ctr = sample_color(_MainTex, uv - dv + du);

            float4 cml = sample_color(_MainTex, uv - du);
            float4 cmr = sample_color(_MainTex, uv + du);

            float4 cbl = sample_color(_MainTex, uv + dv - du);
            float4 cbc = sample_color(_MainTex, uv + dv);
            float4 cbr = sample_color(_MainTex, uv + dv + du);

            #if !CONSTRAINT_NONE
                const int num_samples = 9;
            #endif

            #if !CONSTRAINT_NONE && GAUSSIAN_NEIGHBORHOOD
                float2 offsets[num_samples] = {
                        float2(0,0),
                        float2(-1,-1),
                        float2(-1, 0),
                        float2(-1, 1),
                        float2(0, -1),
                        float2(0, 1),
                        float2(1, -1),
                        float2(1, 0),
                        float2(1, 1)
                    };
                float4 samples[num_samples] = {cmc,ctl,ctc,ctr,cml,cmr,cbl,cbc,cbr};
            #endif

            #if !GAUSSIAN_NEIGHBORHOOD

                float4 n_min_diag = min(min(ctl, ctr), min(cbl, cbr));
                float4 n_max_diag = max(max(ctl, ctr), max(cbl, cbr));
                float4 n_min5 = min(cmc, min(min(ctc, cbc), min(cml, cmr)));
                float4 n_max5 = max(cmc, max(max(ctc, cbc), max(cml, cmr)));

                #if MINMAX_3X3_WEIGHTED
                    float4 n_avg =   ctl * 0.0625 +
                                    ctc * 0.125 +
                                    ctr * 0.0625 +
                                    cml * 0.125 +
                                    cmc * 0.25 +
                                    cmr * 0.125 +
                                    cbl * 0.0625 +
                                    cbc * 0.125 +
                                    cbr * 0.0625;

                    float4 n_min = n_min5 * 0.5 + min(n_min_diag, n_min5) * 0.5;
                    float4 n_max = n_max5 * 0.5 + max(n_max_diag, n_max5) * 0.5;
                #else
                    float4 n_min = min(n_min_diag, n_min5);
                    float4 n_max = max(n_max_diag, n_max5);

                    #if !CONSTRAINT_NONE
                        float4 n_avg = (ctl + ctc + ctr + cml + cmc + cmr + cbl + cbc + cbr) / (float)num_samples;
                    #endif

                    #if MINMAX_3X3_ROUNDED
                        float4 n_avg5 = (ctc + cml + cmc + cmr + cbc) / 5.0;
                        n_min = 0.5 * (n_min + n_min5);
                        n_max = 0.5 * (n_max + n_max5);
                        n_avg = 0.5 * (n_avg + n_avg5);
                    #endif
                #endif
            #endif
        #elif MINMAX_4TAP_VARYING// this is the method used in v2 (PDTemporalReprojection2)
    
            const float _SubpixelThreshold = 0.5;
            const float _GatherBase = 0.5;
            const float _GatherSubpixelMotion = 0.1666;
    
            float2 texel_vel = ss_vel / _MainTex_TexelSize.xy;
            float texel_vel_mag = length(texel_vel) * vs_dist;
            float k_subpixel_motion = saturate(_SubpixelThreshold / (FLT_EPS + texel_vel_mag));
            float k_min_max_support = _GatherBase + _GatherSubpixelMotion * k_subpixel_motion;
    
            float2 ss_offset01 = k_min_max_support * float2(-_MainTex_TexelSize.x, _MainTex_TexelSize.y);
            float2 ss_offset11 = k_min_max_support * float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y);
            float4 c00 = sample_color(_MainTex, uv - ss_offset11);
            float4 c10 = sample_color(_MainTex, uv - ss_offset01);
            float4 c01 = sample_color(_MainTex, uv + ss_offset01);
            float4 c11 = sample_color(_MainTex, uv + ss_offset11);


            #if !CONSTRAINT_NONE
                const int num_samples = 4;
            #endif

            #if !CONSTRAINT_NONE && !GAUSSIAN_NEIGHBORHOOD

                float4 n_avg = (c00 + c10 + c01 + c11) / (float)num_samples;

                float4 n_min = min(c00, min(c10, min(c01, c11)));
                float4 n_max = max(c00, max(c10, max(c01, c11)));
            #endif

            #if !CONSTRAINT_NONE && GAUSSIAN_NEIGHBORHOOD
                float4 samples[num_samples] = {c00,c10,c01,c11};
            #endif
        #else
            #error "missing keyword MINMAX_..."
        #endif

        #if !CONSTRAINT_NONE && GAUSSIAN_NEIGHBORHOOD
            float4 mean = 0;
            float4 stddev = 0;
                
            #if MITCHELL_WEIGHTED && !MINMAX_4TAP_VARYING && 0 
                //NOT DONE
                float sum_w = eval_weight(0.0);
            
                [unroll]
                for (int i = 0; i < num_samples; i++) {
                    float2 ofs = offsets[i];
                    float4 s = samples[i];
                    float w = eval_weight( dot(ofs,ofs) );
                    mean += s * w;
                    stddev += s * s * w;
                    sum_w += w;
                }
                
                mean / sum_w;
                stddev / sum_w;
            #else
                [unroll]
                for (int i = 0; i < num_samples; i++) {
                    float4 s = samples[i];
                    mean += s;
                    stddev += s * s;
                }
    
                const float divident = (float)num_samples;
                mean /= divident;
                stddev /= divident;
                float4 dev = sqrt(abs(stddev - mean * mean));
    
                float4 n_min = mean - dev;
                float4 n_max = mean + dev;
                float4 n_avg = mean;
            #endif
        #endif

        #if !CONSTRAINT_NONE && VELOCITY_ADAPT_CONSTRAINT
            float near_vel_mag = length(ss_vel * _VelocityBuffer_TexelSize.zw) + max_neighbour_vel_mag/4;

            {
            float near_vel_mag_rcp = rcp(_ConstraintVelocityWeight * near_vel_mag + 1);

            float vel_adapt = lerp(_AdaptiveBoxMin, _AdaptiveBoxMax, near_vel_mag_rcp);

            float3 vel_center = 0.5 * (n_max.rgb + n_min.rgb);
            float3 vel_extent = 0.5 * (n_max.rgb - n_min.rgb);

            vel_extent *=  vel_adapt;

            n_min.rgb = vel_center - vel_extent;
            n_max.rgb = vel_center + vel_extent;
            }
        #endif

        #if !CONSTRAINT_NONE && !COLORSPACE_RGB && SCALE_AABB_BOX
            {
            #if ONLY_CONSTRAINT_CHROMA_SPACE

                float2 lc_center = 0.5 * (n_max.gb + n_min.gb);
                float2 lc_extent = 0.5 * (n_max.gb - n_min.gb);

                lc_extent = mul(lc_extent, float2( _YBoxScalingFactor, _ZBoxScalingFactor));

                n_min.gb = lc_center - lc_extent;
                n_max.gb = lc_center + lc_extent;
            #else
                float3 lc_center = 0.5 * (n_max.rgb + n_min.rgb);
                float3 lc_extent = 0.5 * (n_max.rgb - n_min.rgb);

                lc_extent = mul(lc_extent, float3( _XBoxScalingFactor, _YBoxScalingFactor, _ZBoxScalingFactor));

                n_min.rgb = lc_center - lc_extent; 
                n_max.rgb = lc_center + lc_extent;
            #endif
            }
        #endif

        #if CONSTRAINT_CLAMP
            constrained_history = clamp(texel_history, n_min, n_max);
        #elif CONSTRAINT_CLIP
            {
            float3 center = 0.5 * (n_max.xyz + n_min.xyz);
            float3 extent = 0.5 * (n_max.xyz - n_min.xyz);

            #if CLIP_TOWARDS_CENTER
                constrained_history = center_clip_aabb(center, extent, texel_history);
            #else
                n_avg = clamp(n_avg, n_min, n_max);
                constrained_history = full_clip_aabb(center, extent, n_avg, texel_history); // clamp to neighbourhood of current sample
            #endif
            }
        #endif

        #if ONLY_CONSTRAINT_CHROMA_SPACE
            constrained_history.r = texel_history.r;
        #endif

        #if JIMANEZ_ANTI_FLICKER
            {
            #if COLORSPACE_RGB
                float local_contrast = Luminance(n_max.rgb) - Luminance(n_min.rgb);
            #else
                float local_contrast = n_max.r - n_min.r;
            #endif

            out_alpha = local_contrast;

            float temporal_t = abs(texel_history.a - local_contrast );

            float rt = remap( temporal_t, _JimanezAfContrastMin, _JimanezAfContrastMax, 0.0, 1.0-feedback_factor );

            feedback_factor = feedback_factor + rt;
            } 
        #else
            {
            #if !COLORSPACE_RGB
                float lum0 = Luminance(texel_current.rgb);
                float lum1 = Luminance(texel_history.rgb);
            #else
                float lum0 = texel_current.r;
                float lum1 = texel_history.r;
            #endif

            float unbiased_diff = abs(lum0 - lum1) / max(lum0, max(lum1, 0.2));
            float unbiased_weight = 1.0 - unbiased_diff; // feedback weight from unbiased luminance diff (t.lottes)
            float feedback_factor = unbiased_weight * unbiased_weight;

            }
        #endif

        #if STACHOWIAC_ANTI_FLICKER
            {
            #if COLORSPACE_RGB
                float l_constrained_history = Luminance(constrained_history.rgb);
                float l_n_min = Luminance(n_min.rgb);
                float l_n_max = Luminance(n_max.rgb);
                float l_avg = Luminance(n_avg.rgb);
            #else
                float l_constrained_history = constrained_history.r;
                float l_n_min = n_min.r;
                float l_n_max = n_max.r;
                float l_avg = n_avg.r;
            #endif

            //note: from https://github.com/h3r2tic/rtoy-samples/blob/master/assets/shaders/taa.glsl
            float clamp_dist = (min(abs(l_constrained_history - l_n_min), abs(l_constrained_history - l_n_max))) / max(max(l_constrained_history, l_avg), 1e-5);
            float neighborhood_t = lerp(_StachowiacAfContrastMin, _StachowiacAfContrastMax, smoothstep(0.0, 2.0, clamp_dist));
            feedback_factor = 1.0 - (1.0-feedback_factor) * neighborhood_t;
            }
        #endif

        #if LIMIT_FEEDBACK
            feedback_factor = lerp(_FeedbackMin, _FeedbackMax, feedback_factor);
        #else
            feedback_factor = saturate(feedback_factor);
        #endif

        #if !CONSTRAINT_NONE && PHASIC_CONSTRAINT && !JIMANEZ_ANTI_FLICKER
            #if VELOCITY_ADAPT_PHASIC_CONSTRAINT
                constrained_history.a += (_PhasicVelocityWeight*near_vel_mag);
            #endif

            float history_l = saturate(lerp(constrained_history.a * _ClippingPhaseInFactor, texel_history.a * _ClippingPhaseOutFactor, feedback_factor));
            constrained_history.rgb = lerp(texel_history.rgb, constrained_history.rgb, history_l);
            out_color = lerp(texel_current.rgb, constrained_history.rgb, 1.0-history_l);
            out_alpha = history_l;
        #else
            out_color = lerp(texel_current.rgb, constrained_history.rgb, feedback_factor);
        #endif

        return float4(out_color, out_alpha);
    }

    struct f2rt	{
        fixed4 buffer : SV_Target0;
        fixed4 screen : SV_Target1;
    };

    f2rt frag(v2f IN){
        f2rt OUT;

        #if UNJITTER_REPROJECTION
            float2 uv = IN.ss_txc - _JitterUV.xy;
        #else
            float2 uv = IN.ss_txc;
        #endif
    
        #if USE_DILATION
            //--- 3x3 norm (sucks)
            //float2 ss_vel = sample_velocity_dilated(_VelocityBuffer, uv, 1);
            //float vs_dist = depth_sample_linear(uv);
    
            //--- 5 tap nearest (decent)
            //float3 c_frag = find_closest_fragment_5tap(uv);
            //float2 ss_vel = tex2D(_VelocityBuffer, c_frag.xy).xy;
            //float vs_dist = depth_resolve_linear(c_frag.z);
    
            //--- 3x3 nearest (good)
            float3 c_frag = find_closest_fragment_3x3(uv);
            float2 ss_vel = tex2D(_VelocityBuffer, c_frag.xy).xy;
            float vs_dist = depth_resolve_linear(c_frag.z);
        #else
            float2 ss_vel = tex2D(_VelocityBuffer, uv).xy;
            float vs_dist = depth_sample_linear(uv);
        #endif

        #if USE_MAX_NEIGHBOR_VELOCITY
            float2 ss_vel_max =  tex2D(_VelocityNeighborMax, IN.ss_txc).xy;
        #else
            float2 ss_vel_max = ss_vel;
        #endif

        float vel_max_mag = length( ss_vel_max * _VelocityBuffer_TexelSize.zw);

        float4 color_temporal = temporal_reprojection(IN.ss_txc, ss_vel, vs_dist, vel_max_mag);

        #if USE_MOTION_BLUR
            const float vel_trust_full = 2.0;
            const float vel_trust_none = 15.0;
            const float vel_trust_span = vel_trust_none - vel_trust_full;
            float trust = 1.0 - clamp(vel_max_mag - vel_trust_full, 0.0, vel_trust_span) / vel_trust_span;
    
            #if UNJITTER_COLORSAMPLES
                float4 color_motion = sample_color_motion(_MainTex, IN.ss_txc - _JitterUV.xy, vel_max_mag*_MotionScale);
            #else
                float4 color_motion = sample_color_motion(_MainTex, IN.ss_txc, vel_max_mag*_MotionScale);
            #endif
    
            float4 to_screen = lerp(color_motion, color_temporal, trust));
            float4 to_buffer = color_temporal;
        #else
            float4 to_screen = color_temporal;
            float4 to_buffer = color_temporal;
        #endif

        to_screen.a = 1.0; // ALPHA CONST OF 1.0 to the screen not the history buffer
        to_screen = resolve_color(to_screen);
        to_buffer = resolve_history(to_buffer);

        #if VELOCITY_DEBUG
            const float overlay_strength = .33;
            to_screen.r += overlay_strength * (ss_vel.x / _VelocityBuffer_TexelSize.x);
            to_screen.g += overlay_strength * (ss_vel.y / _VelocityBuffer_TexelSize.y);
            to_screen.b += overlay_strength * vel_max_mag;
        #endif

        OUT.buffer = to_buffer;
        OUT.screen = saturate(to_screen);

        return OUT;
    }

    //--- program end
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Fog { Mode off }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            ENDCG
        }
    }

    Fallback off
}