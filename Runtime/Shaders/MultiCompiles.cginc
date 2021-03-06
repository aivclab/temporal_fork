#pragma multi_compile CAMERA_PERSPECTIVE CAMERA_ORTHOGRAPHIC
#pragma multi_compile MINMAX_3X3 MINMAX_3X3_ROUNDED MINMAX_3X3_WEIGHTED MINMAX_4TAP_VARYING
#pragma multi_compile INTRPL_NONE INTRPL_CATMULL_ROM INTRPL_CATMULL_ROM_CUBIC
#pragma multi_compile CONSTRAINT_NONE CONSTRAINT_CLAMP CONSTRAINT_CLIP
#pragma multi_compile COLORSPACE_RGB COLORSPACE_YCOCG COLORSPACE_YCBCR
#pragma multi_compile ENCODE_LINEAR ENCODE_EXP ENCODE_SUPER_EXP
#pragma multi_compile __ UNJITTER_COLORSAMPLES
#pragma multi_compile __ UNJITTER_NEIGHBORHOOD
#pragma multi_compile __ UNJITTER_REPROJECTION

#pragma multi_compile __ CLIP_TOWARDS_CENTER
#pragma multi_compile __ COLORSPACE_RESOLVE_HISTORY_BUFFER

#pragma multi_compile __ SCALE_AABB_BOX

#pragma multi_compile __ USE_DILATION
#pragma multi_compile __ USE_MAX_NEIGHBOR_VELOCITY
#pragma multi_compile __ GAUSSIAN_NEIGHBORHOOD
#pragma multi_compile __ VELOCITY_ADAPT_CONSTRAINT
#pragma multi_compile __ VELOCITY_DEBUG
#pragma multi_compile __ USE_MOTION_BLUR