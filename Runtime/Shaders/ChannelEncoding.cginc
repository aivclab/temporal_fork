#ifndef __CHANNEL_ENCODING__
#define __CHANNEL_ENCODING__

    float3 encode_rgb(float3 a) {
        #if ENCODE_EXP
            return sqrt(a);
        #elif ENCODE_SUPER_EXP
            return log(1+sqrt(a));
        #else
            return a;
        #endif
    }
    
    float3 decode_rgb(float3 a) {
        #if ENCODE_EXP
            return a * a;
        #elif ENCODE_SUPER_EXP
            a = exp(a) - 1; 
            return a * a; // (e^a - 1)^2
        #else // ENCODE_LINEAR
            return a;
        #endif
    }

    float3 rgb_to_ycocg(float3 rgb){
        float co = rgb.r - rgb.b;
        float t = rgb.b + co / 2.0;
        float cg = rgb.g - t;
        float y = t + cg / 2.0;
        return float3(y, co, cg);
    }


    float3 ycocg_to_rgb(float3 ycocg){
        float t = ycocg.r - ycocg.b / 2.0;
        float g = ycocg.b + t;
        float b = t - ycocg.g / 2.0;
        float r = ycocg.g + b;
        return float3(r, g, b);
    }

    float3 rgb_to_ycbcr(float3 col) {
        //from https://github.com/h3r2tic/rtoy-samples/blob/master/assets/shaders/inc/color.inc
        // Rec. 709
        float3x3 m = float3x3(  0.2126, 0.7152, 0.0722,
                                -0.1146,-0.3854, 0.5,
                                0.5,-0.4542,-0.0458);
        return mul(col, m);
    }

    float3 ycbcr_to_rgb(float3 col) {
        float3x3 m = float3x3(  1.0, 0.0, 1.5748,
                                1.0, -0.1873, -.4681,
                                1.0, 1.8556, 0.0);
        return mul(col, m);
    }

    float4 sample_color(sampler2D tex, float2 uv)	{
        float4 c = tex2D(tex, uv);
        c.rgb = encode_rgb(c.rgb);
        
        #if COLORSPACE_YCOCG
            c = float4(rgb_to_ycocg(c.rgb), c.a);
        #elif COLORSPACE_YCBCR
            c = float4(rgb_to_ycbcr(c.rgb), c.a);
        #endif
        
        return c;
    }


    float4 resolve_color(float4 c)	{
        #if COLORSPACE_YCOCG
            c = saturate(float4(ycocg_to_rgb(c.rgb), c.a));
        #elif COLORSPACE_YCBCR
            c = saturate(float4(ycbcr_to_rgb(c.rgb), c.a));
        #endif
        
        c.rgb = decode_rgb(c.rgb); 
        return c;
    }

    float4 sample_history(sampler2D tex, float2 uv)	{
      #if !COLORSPACE_RGB && COLORSPACE_RESOLVE_HISTORY_BUFFER && !LATE_HISTORY_COLOR_TRANSFORM
        return sample_color(tex, uv);
      #else
        return tex2D(tex, uv);
      #endif
    }

    float3 late_history_color_transform(float3 c_rgb)	{
        #if COLORSPACE_YCOCG && LATE_HISTORY_COLOR_TRANSFORM && COLORSPACE_RESOLVE_HISTORY_BUFFER
            c_rgb = rgb_to_ycocg(c_rgb);
        #elif COLORSPACE_YCBCR && LATE_HISTORY_COLOR_TRANSFORM && COLORSPACE_RESOLVE_HISTORY_BUFFER
            c_rgb = rgb_to_ycbcr(c_rgb);
        #endif
        
        return c_rgb;
    }

    float4 resolve_history(float4 c)	{
      #if !COLORSPACE_RGB && COLORSPACE_RESOLVE_HISTORY_BUFFER
        c = resolve_color(c);        
      #endif
      
      return c;
    }

#endif//__CHANNEL_ENCODING__