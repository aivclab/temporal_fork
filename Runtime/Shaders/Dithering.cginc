#ifndef __DITHERING__
#define __DITHERING__

vec2 hash2( uint n ) {
    // integer hash copied from Hugo Elias
	n = (n << 13U) ^ n;
    n = n * (n * n * 15731U + 789221U) + 1376312589U;
    uvec2 k = n * uvec2(n,n*16807U);
    return vec2( k & 0x7fffffffU)/float(0x7fffffff);
}

vec2 off = hash2(uint(iFrame)) - 0.5;

#endif