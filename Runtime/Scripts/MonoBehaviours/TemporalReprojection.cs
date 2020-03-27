// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

#if UNITY_5_5_OR_NEWER
#define SUPPORT_STEREO
#endif

using PDTAAFork.Scripts.Classes;
using UnityEngine;


namespace PDTAAFork.Scripts.MonoBehaviours
{
    /// <inheritdoc />
    /// <summary>
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Playdead/TemporalReprojection")]
    public class TemporalReprojection : MonoBehaviour
    {
        // Should generally be used before screen space effects.

        /// <summary>
        ///
        /// </summary>
        public enum Neighborhood
        {
            MinMax3X3,
            MinMax3X3Rounded,
            MinMax3X3Weighted,
            MinMax4TapVarying
        };

        /// <summary>
        ///
        /// </summary>
        public enum HistoryInterpolation
        {
            None,
            CatMullRom,
            CatMullRomCubic
        }

        /// <summary>
        ///
        /// </summary>
        public enum Constraint
        {
            None_,
            Clamp,
            Clip
        }

        public enum ConstraintColorSpace {
            RGB,
            YCOCG,
            YCBCR
        }
        
        public enum ColorEncoding {
            LINEAR,
            EXP,
            SUPER_EXP
        }

        static readonly int MotionScale = Shader.PropertyToID("_MotionScale");
        static readonly int FeedbackMax = Shader.PropertyToID("_FeedbackMax");
        static readonly int FeedbackMin = Shader.PropertyToID("_FeedbackMin");
        static readonly int PrevTex = Shader.PropertyToID("_PrevTex");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int VelocityNeighborMax = Shader.PropertyToID("_VelocityNeighborMax");
        static readonly int VelocityBuffer = Shader.PropertyToID("_VelocityBuffer");
        static readonly int JitterUv = Shader.PropertyToID("_JitterUV");
        static readonly int AdaptiveBoxMax = Shader.PropertyToID("_AdaptiveBoxMax");
        static readonly int AdaptiveBoxMin = Shader.PropertyToID("_AdaptiveBoxMin");
        static readonly int ConstraintVelocityWeight = Shader.PropertyToID("_ConstraintVelocityWeight");
        static readonly int PhasicVelocityWeight = Shader.PropertyToID("_PhasicVelocityWeight");
        static readonly int ClippingPhaseInFactor = Shader.PropertyToID("_ClippingPhaseInFactor");
        static readonly int ClippingPhaseOutFactor = Shader.PropertyToID("_ClippingPhaseOutFactor");
        static readonly int XBoxScalingFactor = Shader.PropertyToID
        ("_XBoxScalingFactor");
        static readonly int YBoxScalingFactor = Shader.PropertyToID("_YBoxScalingFactor");
        static readonly int ZBoxScalingFactor = Shader.PropertyToID("_ZBoxScalingFactor");
        static readonly int JimanezAfContrastMin = Shader.PropertyToID("_JimanezAfContrastMin");
        static readonly int JimanezAfContrastMax = Shader.PropertyToID("_JimanezAfContrastMax");
        static readonly int StachowiacAfContrastMin = Shader.PropertyToID("_StachowiacAfContrastMin");
        static readonly int StachowiacAfContrastMax = Shader.PropertyToID("_StachowiacAfContrastMax");

        static RenderBuffer[] _mrt = new RenderBuffer[2];
        Shader _reprojectionShader;
        [SerializeField] Material reprojectionMaterial;
        RenderTexture[,] _reprojectionBuffer;
        int[] _reprojectionIndex = {-1, -1};
        Camera _camera;

        [Header("Subcomponents")]
        [SerializeField] FrustumJitter frustumJitter;
        [SerializeField] VelocityBuffer velocityBuffer;

        [Header("Unjittering")]
        [SerializeField] bool unjitterColorSamples = true;
        [SerializeField] bool unjitterNeighborhood = false;
        [SerializeField] bool unjitterReprojection = false;

        [Header("Neighborhood")]
        [SerializeField] Neighborhood neighborhood = Neighborhood.MinMax3X3Weighted;
        [SerializeField] bool gaussianNeighborhood = true;
        [SerializeField] bool useDilation = true;

        [Header("Constraint Scaling")]
        [SerializeField] Constraint _constraint = Constraint.Clamp;
        [SerializeField] bool adaptiveConstraint = true;
        [SerializeField] [Range(0.1f, 9.9f)] float adaptiveBoxMax = 1.28f;
        [SerializeField] [Range(0.1f, 1.0f)] float adaptiveBoxMin = 0.746f;
        [SerializeField] [Range(0f, 2f)] float constraintVelocityWeight = 0.328f;

        [Header("Anti Flicker - Phasic")]
        [SerializeField] bool phasicAf = false;
        [SerializeField] [Range(0f, 5f)] float phasicVelocityTerm = 0.712f;
        [SerializeField] [Range(0f, 5f)] float clippingPhaseInFactor = 1.0f;
        [SerializeField] [Range(0f, 2f)] float clippingPhaseOutFactor = 1.0f;

        [Header("Anti Flicker - Contrast")]
        [SerializeField] bool jimanezAf = true;
        [SerializeField] [Range(0.01f, 1.0f)] float jimanezAfContrastMin = 0.05f;
        [SerializeField] [Range(0.01f, 1.0f)] float jimanezAfContrastMax = 0.35f;

        [SerializeField] bool stachowiacAf = true;
        [SerializeField] [Range(0.01f, 1.0f)] float stachowiacAfContrastMin = 0.2f;
        [SerializeField] [Range(0.01f, 1.0f)] float stachowiacAfContrastMax = 1.0f;

        [Header("Color Space Box")]
        [SerializeField] ConstraintColorSpace _constraint_in = ConstraintColorSpace.RGB;
        [SerializeField] ColorEncoding _color_encoding = ColorEncoding.LINEAR;
        [SerializeField] bool scaleAabbBox = false;
        /// <summary>
        /// x component of aabb box extent (Red for RGB) (Lum for YCoCg and YCbCr)
        /// </summary>
        [SerializeField] [Range(0.0001f, 2.0f)] float xBoxScale = 0.24f;
        /// <summary>
        /// y component of aabb box extent (Green for RGB)
        /// </summary>
        [SerializeField] [Range(0.0001f, 2.0f)] float yBoxScale = 1.161f;
        /// <summary>
        /// z component of aabb box extent (Blue for RGB)
        /// </summary>
        [SerializeField] [Range(0.0001f, 2.0f)] float zBoxScale = 1.305f;

        [SerializeField] bool clipTowardsCenter = false;

        [Header("History")]
        [SerializeField] HistoryInterpolation historyInterpolation = HistoryInterpolation.CatMullRom;
        [SerializeField] bool resolveHistory = false;
        [SerializeField] RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGBFloat;
        [SerializeField] FilterMode filtering = FilterMode.Bilinear;

        [Header("Feedback Limiting")]
        [SerializeField] bool feedbackLimiting = false;
        [SerializeField] [Range(0.0f, 1.0f)] float feedbackMin = 0.847f;
        [SerializeField] [Range(0.0f, 1.0f)] float feedbackMax = 0.95f;

        [Header("Motion Blur")]
        [SerializeField] bool useMotionBlur = false;
        [SerializeField] [Range(0.0f, 2.0f)] float motionBlurStrength = 1.0f;
        [SerializeField] bool motionBlurIgnoreFf = false;

        [Header("Debug")]
        [SerializeField] bool velocityDebug = false;

        void OnPreCull()
        {
            this.frustumJitter?.OnPreCull(ref this._camera);
        }

        void OnPreRender()
        {
            this.velocityBuffer?.OnPreRender(ref this._camera);
        }

        void OnPostRender()
        {
            this.velocityBuffer?.OnPostRender(ref this._camera, ref this.frustumJitter);
        }

        void Reset()
        {
            if (!this._camera)
            {
                this._camera = this.GetComponent<Camera>();
            }

            if (!this._reprojectionShader)
            {
                this._reprojectionShader = Shader.Find("Playdead/Post/TemporalReprojection");
            }

            if (this.velocityBuffer == null)
            {
                this.frustumJitter = new FrustumJitter();
            }

            if (this.velocityBuffer == null)
            {
                this.velocityBuffer = new VelocityBuffer();
            }
        }

        void Setup()
        {
            this.Reset();
            this.Clear();

            this.velocityBuffer?.Start();
        }

        void Clear()
        {
            TaaUtilities.EnsureArray(ref this._reprojectionIndex, 2);
            this._reprojectionIndex[0] = -1;
            this._reprojectionIndex[1] = -1;

            this.frustumJitter?.Clear(ref this._camera);
            this.velocityBuffer?.Clear();
        }

        void Awake()
        {
            this.Setup();
        }

        void Resolve(RenderTexture source, RenderTexture destination)
        {
            TaaUtilities.EnsureArray(ref this._reprojectionBuffer, 2, 2);
            TaaUtilities.EnsureArray(ref this._reprojectionIndex, 2, initial_value: -1);

            TaaUtilities.EnsureMaterial(ref this.reprojectionMaterial, this._reprojectionShader);
            if (this.reprojectionMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

#if SUPPORT_STEREO
            var eyeIndex = this._camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right ? 1 : 0;
#else
        int eyeIndex = 0;
#endif
            var bufferW = source.width;
            var bufferH = source.height;

            if (TaaUtilities.EnsureRenderTarget(ref this._reprojectionBuffer[eyeIndex, 0],
                bufferW,
                bufferH,
                this.renderTextureFormat,
                this.filtering,
                anti_aliasing: source.antiAliasing))
            {
                this.Clear();
            }

            if (TaaUtilities.EnsureRenderTarget(ref this._reprojectionBuffer[eyeIndex, 1],
                bufferW,
                bufferH,
                this.renderTextureFormat,
                this.filtering,
                anti_aliasing: source.antiAliasing))
            {
                this.Clear();
            }

#if SUPPORT_STEREO
            var stereoEnabled = this._camera.stereoEnabled;
#else
        bool stereoEnabled = false;
#endif
#if UNITY_EDITOR
            var allowMotionBlur = !stereoEnabled && Application.isPlaying;
#else
        bool allowMotionBlur = !stereoEnabled;
#endif

            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "CAMERA_PERSPECTIVE",
                !this._camera.orthographic);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "CAMERA_ORTHOGRAPHIC",
                this._camera.orthographic);

            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "MINMAX_3X3",
                this.neighborhood == Neighborhood.MinMax3X3);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "MINMAX_3X3_ROUNDED",
                this.neighborhood == Neighborhood.MinMax3X3Rounded);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "MINMAX_3X3_WEIGHTED",
                this.neighborhood == Neighborhood.MinMax3X3Weighted);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "MINMAX_4TAP_VARYING",
                this.neighborhood == Neighborhood.MinMax4TapVarying);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "INTRPL_NONE",
                this.historyInterpolation == HistoryInterpolation.None);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "INTRPL_CATMULL_ROM",
                this.historyInterpolation
                == HistoryInterpolation.CatMullRom);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "INTRPL_CATMULL_ROM_CUBIC",
                this.historyInterpolation
                == HistoryInterpolation.CatMullRomCubic);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "UNJITTER_COLORSAMPLES",
                this.unjitterColorSamples);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "UNJITTER_NEIGHBORHOOD",
                this.unjitterNeighborhood);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "UNJITTER_REPROJECTION",
                this.unjitterReprojection);

            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "CONSTRAINT_NONE",
                this._constraint == Constraint.None_);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "CONSTRAINT_CLAMP",
                this._constraint == Constraint.Clamp);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "CONSTRAINT_CLIP",
                this._constraint == Constraint.Clip);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "PHASIC_CONSTRAINT",
                phasicAf);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "LIMIT_FEEDBACK",
                feedbackLimiting);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "JIMANEZ_ANTI_FLICKER",
                jimanezAf);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "STACHOWIAC_ANTI_FLICKER",
                stachowiacAf);

            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "CLIP_TOWARDS_CENTER", this.clipTowardsCenter);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "VARIANCE_CLIPPING", this.gaussianNeighborhood);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "ADAPTIVE_CLIPPING", this.adaptiveConstraint);

            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "COLORSPACE_YCBCR", this._constraint_in == ConstraintColorSpace.YCBCR);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "COLORSPACE_YCOCG", this._constraint_in == ConstraintColorSpace.YCOCG);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "COLORSPACE_RGB", this._constraint_in == ConstraintColorSpace.RGB);
            
            
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "ENCODE_LINEAR", this._color_encoding == ColorEncoding.LINEAR);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "ENCODE_EXP", this._color_encoding == ColorEncoding.EXP);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "ENCODE_SUPER_EXP", this._color_encoding == ColorEncoding.SUPER_EXP);

            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "RESOLVE_HISTORY", this.resolveHistory);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "SCALE_AABB_BOX",
                this.scaleAabbBox);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "USE_DILATION", this.useDilation);
            TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                "USE_MOTION_BLUR",
                this.useMotionBlur && allowMotionBlur);
            if (this.velocityBuffer != null)
            {
                TaaUtilities.EnsureKeyword(this.reprojectionMaterial,
                    "USE_MAX_NEIGHBOR_VELOCITY",
                    this.velocityBuffer.ActiveVelocityNeighborMax != null);

                TaaUtilities.EnsureKeyword(this.reprojectionMaterial, "VELOCITY_DEBUG", this.velocityDebug);
            }

            if (this._reprojectionIndex[eyeIndex] == -1)
            {
                // bootstrap
                this._reprojectionIndex[eyeIndex] = 0;
                this._reprojectionBuffer[eyeIndex, this._reprojectionIndex[eyeIndex]].DiscardContents();
                Graphics.Blit(source, this._reprojectionBuffer[eyeIndex, this._reprojectionIndex[eyeIndex]]);
            }

            var indexRead = this._reprojectionIndex[eyeIndex];
            var indexWrite = (this._reprojectionIndex[eyeIndex] + 1) % 2;

            var jitterUv = this.frustumJitter._ActiveSample;
            jitterUv.x /= source.width;
            jitterUv.y /= source.height;
            jitterUv.z /= source.width;
            jitterUv.w /= source.height;

            this.reprojectionMaterial.SetVector(JitterUv, jitterUv);
            this.reprojectionMaterial.SetTexture(VelocityBuffer, this.velocityBuffer.ActiveVelocityBuffer);
            this.reprojectionMaterial.SetTexture(VelocityNeighborMax,
                this.velocityBuffer.ActiveVelocityNeighborMax);
            this.reprojectionMaterial.SetTexture(MainTex, source);
            this.reprojectionMaterial.SetTexture(PrevTex, this._reprojectionBuffer[eyeIndex, indexRead]);
            this.reprojectionMaterial.SetFloat(FeedbackMin, this.feedbackMin);
            this.reprojectionMaterial.SetFloat(FeedbackMax, this.feedbackMax);
            this.reprojectionMaterial.SetFloat(AdaptiveBoxMax, this.adaptiveBoxMax);
            this.reprojectionMaterial.SetFloat(AdaptiveBoxMin, this.adaptiveBoxMin);
            this.reprojectionMaterial.SetFloat(XBoxScalingFactor, this
            .xBoxScale);
            this.reprojectionMaterial.SetFloat(YBoxScalingFactor, this.yBoxScale);
            this.reprojectionMaterial.SetFloat(ZBoxScalingFactor, this.zBoxScale);
            this.reprojectionMaterial.SetFloat(ConstraintVelocityWeight, this.constraintVelocityWeight);
            this.reprojectionMaterial.SetFloat(PhasicVelocityWeight, this.phasicVelocityTerm);
            this.reprojectionMaterial.SetFloat(MotionScale,
                this.motionBlurStrength
                * (this.motionBlurIgnoreFf
                    ? Mathf.Min(1.0f, 1.0f / this.velocityBuffer.TimeScale)
                    : 1.0f));
            this.reprojectionMaterial.SetFloat(ClippingPhaseInFactor, this.clippingPhaseInFactor);
            this.reprojectionMaterial.SetFloat(ClippingPhaseOutFactor, this.clippingPhaseOutFactor);
            this.reprojectionMaterial.SetFloat(JimanezAfContrastMin, this.jimanezAfContrastMin);
            this.reprojectionMaterial.SetFloat(JimanezAfContrastMax, this.jimanezAfContrastMax);
            this.reprojectionMaterial.SetFloat(StachowiacAfContrastMin, this.stachowiacAfContrastMin);
            this.reprojectionMaterial.SetFloat(StachowiacAfContrastMax, this.stachowiacAfContrastMax);

            // reproject frame n-1 into output + history buffer
            _mrt[0] = this._reprojectionBuffer[eyeIndex, indexWrite].colorBuffer;
            _mrt[1] = destination.colorBuffer;

            Graphics.SetRenderTarget(_mrt, source.depthBuffer);
            this.reprojectionMaterial.SetPass(0);
            this._reprojectionBuffer[eyeIndex, indexWrite].DiscardContents();

            TaaUtilities.DrawFullscreenQuad();

            this._reprojectionIndex[eyeIndex] = indexWrite;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (destination != null && source.antiAliasing == destination.antiAliasing)
            {
                // resolve without additional blit when not end of chain
                this.Resolve(source, destination);
            }
            else
            {
                var internal_destination = RenderTexture.GetTemporary(source.width,
                    source.height,
                    0,
                    this.renderTextureFormat,
                    RenderTextureReadWrite.Default,
                    source.antiAliasing);
                this.Resolve(source, internal_destination);
                Graphics.Blit(internal_destination, destination);
                RenderTexture.ReleaseTemporary(internal_destination);
            }
        }

        void OnApplicationQuit()
        {
            this.velocityBuffer.OnApplicationQuit();

            if (this._reprojectionBuffer != null)
            {
                TaaUtilities.ReleaseRenderTarget(ref this._reprojectionBuffer[0, 0]);
                TaaUtilities.ReleaseRenderTarget(ref this._reprojectionBuffer[0, 1]);
                TaaUtilities.ReleaseRenderTarget(ref this._reprojectionBuffer[1, 0]);
                TaaUtilities.ReleaseRenderTarget(ref this._reprojectionBuffer[1, 1]);
            }
        }
    }
}