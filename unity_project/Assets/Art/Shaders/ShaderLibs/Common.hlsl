#ifndef TAECG_COMMON
#define TAECG_COMMON

//smoothstep性能优化版
real smoothstepsimple(real a,real b,real x)
{
    return saturate((x - a) / (b - a));
}

//RGB转换成HSV颜色
half3 RGB2HSV(half3 c)
{
    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
    half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
    half d = q.x - min(q.w, q.y);
    half e = 1.0e-10;
    return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

//HSV转换成RGB颜色
half3 HSV2RGB(half3 c)
{
    half4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

half3 GradientColor(half uv,half3 c0,half3 c1,half3 c2, float p)
{
    half f = pow(uv, p) * 2;
    half3 a = lerp(c0, c1, frac(f));
    half3 b = lerp(c1, c2, frac(f));
    half3 c = lerp(a, b, f > 1);
    return c;
}

//4x4Dither
half Dither4x4(uint2 uv)
{
    uv %= 4;
    float A4x4[16] = {
        0, 8, 2, 10,
        12, 4, 14, 6,
        3, 11, 1, 9,
        15, 7, 13, 5
    };
    return A4x4[uv.x * 4 + uv.y] / 17;
}

//8x8Dither
//注：当同时修改多个材质球的Dither时会产生大量的GPU耗时，原因不明
half Dither8x8(uint2 uv)
{
    uv %= 8;
    float A4x4[64] = {
        0, 32, 8, 40, 2, 34, 10, 42,
        48, 16, 56, 24, 50, 18, 58, 26,
        12, 44, 4, 36, 14, 46, 6, 38,
        60, 28, 52, 20, 62, 30, 54, 22,
        3, 35, 11, 43, 1, 33, 9, 41,
        51, 19, 59, 27, 49, 17, 57, 25,
        15, 47, 7, 39, 13, 45, 5, 37,
        63, 31, 55, 23, 61, 29, 53, 21
    };
    return A4x4[uv.x * 8 + uv.y] / 65;
}

#endif
