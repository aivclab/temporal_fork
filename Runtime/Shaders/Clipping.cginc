#ifndef __CLIPPING__
#define __CLIPPING__

inline float4 full_clip_aabb(float3 center, float3 extent, float4 current_sample, float4 history_sample)	{
    #if USE_YCOCG && YCOCG_ONLY_CONSTRAINT_CHROMA_SPACE
        float2 a_d = abs(history_sample.yz - center.yz);
        if (a_d.x <= extent.y && a_d.y <= extent.z) {
            #if CLIPPING_PHASIC
                history_sample.a = 0;
            #endif
        }else{
            float2 dir = current_sample.yz - history_sample.yz;
            float2 near = center.yz - sign(dir) * extent.yz;
            float2 tAll = (near - history_sample.yz) / dir;
            float t = 1e20;
            [unroll]
            for (int i = 0; i < 2; i++) {
                if (tAll[i] >= 0.0 && tAll[i] < t) {
                    t = tAll[i];
                }
            }

            if (t >= 1e20) {
                #if CLIPPING_PHASIC
                    history_sample.a = 0;
                #endif
            }else{
                history_sample.gb = history_sample.gb + dir * t;
                #if CLIPPING_PHASIC
                    #if SOFT_CLIP_INDICATOR
                        history_sample.a = smoothstep(0,length(dir),t);
                    #else
                        history_sample.a = 1;
                    #endif
                #endif
            }
        }
    #else
        float3 a_d = abs(history_sample.xyz - center);
        if (a_d.x <= extent.x && a_d.y <= extent.y && a_d.z <= extent.z) {
            #if CLIPPING_PHASIC
                history_sample.a = 0;
            #endif
        }else{
            float3 dir = current_sample.xyz - history_sample.xyz;
            float3 near = center - sign(dir) * extent;
            float3 t_rgb = (near - history_sample.xyz) / dir;
            float t = 1e20;
            [unroll]
            for (int i = 0; i < 3; i++) {
                if (t_rgb[i] >= 0.0 && t_rgb[i] < t) {
                    t = t_rgb[i];
                }
            }

            if (t >= 1e20) {
                #if CLIPPING_PHASIC
                    history_sample.a = 0;
                #endif
            }else{
                history_sample.rgb = history_sample.rgb + dir * t;
                #if SOFT_CLIP_INDICATOR
                    history_sample.a = smoothstep(0,length(dir),t);
                #else
                    history_sample.a = 1;
                #endif
            }
        }
    #endif

    return history_sample;
}

inline float4 center_clip_aabb(float3 center, float3 extent, float4 history_sample)	{
    #if USE_YCOCG && YCOCG_ONLY_CONSTRAINT_CHROMA_SPACE
        float2 diff = history_sample.yz - center.yz;
        float2 a_unit = abs(diff / extent.yz);
        float max_unit = max(a_unit.x, a_unit.y);
        float2 div = diff / max_unit;
        #if CLIPPING_PHASIC
            if (max_unit > 1.0){
                history_sample.gb = center.gb + div;
                #if SOFT_CLIP_INDICATOR
                    history_sample.a = smoothstep(0,length(div),length(diff));
                #else
                    history_sample.a = 1;
                #endif
            }else{
                history_sample.a = 0.0;
            }
        #else
            if (max_unit > 1.0){
                history_sample.gb = center.gb + div;
            }
        #endif
    #else
        float3 diff = history_sample.xyz - center;
        float3 a_unit = abs(diff / extent);
        float max_unit = max(a_unit.x, max(a_unit.y, a_unit.z));
        float3 div = diff / max_unit;
        #if CLIPPING_PHASIC
            if (max_unit > 1.0){
                history_sample.rgb = center + div;
                    #if SOFT_CLIP_INDICATOR
                        history_sample.a = smoothstep(0,length(div),length(diff));
                    #else
                        history_sample.a = 1;
                    #endif
            }else{
                history_sample.a = 0.0;
            }
        #else
            if (max_unit > 1.0){
                history_sample.rgb = center + div;
            }
        #endif

    #endif

    return history_sample;
}

#endif//__CLIPPING__