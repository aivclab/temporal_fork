//#define USE_PP

// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

#if UNITY_5_5_OR_NEWER
#define SUPPORT_STEREO
#endif

#if USE_PP
using System;
using PDTAAFork.Scripts.Classes;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PDTAAFork.Scripts.PostProcessingStack {
  /// <summary>
  ///
  /// </summary>
  [Serializable]
  [PostProcess(typeof(TemporalReprojectionRenderer),
      PostProcessEvent.AfterStack,
      "PDTAAFork/TemporalReprojection")]
  //[RequireComponent(typeof(PpStackFrustumJitter))]
  public sealed class TemporalReprojectionEffect : PostProcessEffectSettings {
    /// <summary>
    ///
    /// </summary>
    public FloatParameter flip_x = new FloatParameter {value = 0};
  }

  /// <inheritdoc />
  /// <summary>
  /// </summary>
  public class TemporalReprojectionRenderer : PostProcessEffectRenderer<TemporalReprojectionEffect> {
    // Should generally be used before screen space effects.

    /// <summary>
    ///
    /// </summary>
    public enum Neighborhood {
      Min_max3_x3_,
      Min_max3_x3_rounded_,
      Min_max3_x3_weighted_,
      Min_max4_tap_varying_
    };

    /// <summary>
    ///
    /// </summary>
    public enum HistoryInterpolation {
      Interpolation_none_,
      Interpolation_cat_mull_rom_,
      Interpolation_cat_mull_rom_cubic_
    }

    /// <summary>
    ///
    /// </summary>
    public enum Clipping {
      Clipping_none_,
      Clipping_default_,
      Clipping_phasic_,
      Clipping_phasic_directional_,
      Clipping_phasic_variance_
    }

    static readonly int _motion_scale = Shader.PropertyToID("_MotionScale");
    static readonly int _feedback_max = Shader.PropertyToID("_FeedbackMax");
    static readonly int _feedback_min = Shader.PropertyToID("_FeedbackMin");
    static readonly int _prev_tex = Shader.PropertyToID("_PrevTex");
    static readonly int _main_tex = Shader.PropertyToID("_MainTex");
    static readonly int _velocity_neighbor_max = Shader.PropertyToID("_VelocityNeighborMax");
    static readonly int _velocity_buffer = Shader.PropertyToID("_VelocityBuffer");
    static readonly int _jitter_uv = Shader.PropertyToID("_JitterUV");
    static readonly int _adaptive_clipping_gamma = Shader.PropertyToID("_AdaptiveClippingGamma");
    static readonly int _adaptive_clipping_gamma_min = Shader.PropertyToID("_AdaptiveClippingGammaMin");
    static readonly int _velocity_weight = Shader.PropertyToID("_VelocityWeight");
    static readonly int _clipping_phase_in_factor = Shader.PropertyToID("_ClippingPhaseInFactor");
    static readonly int _y_co_cg_chroma_shrinkage_factor = Shader.PropertyToID("_YCoCgChromaShrinkageFactor");

    static RenderBuffer[] _mrt = new RenderBuffer[2];
    Shader _reprojection_shader;
    Material _reprojection_material;
    RenderTexture[,] _reprojection_buffer;
    int[] _reprojection_index = {-1, -1};
    Camera _camera;

    [SerializeField] FrustumJitter _frustumJitter;
    [SerializeField] VelocityBuffer _velocityBuffer;

    [SerializeField] Neighborhood neighborhood = Neighborhood.Min_max3_x3_weighted_;

    [SerializeField]
    HistoryInterpolation _history_interpolation = HistoryInterpolation.Interpolation_cat_mull_rom_;

    [SerializeField] Clipping _clipping = Clipping.Clipping_phasic_;
    [SerializeField] bool unjitterColorSamples = true;
    [SerializeField] bool unjitterNeighborhood = false;
    [SerializeField] bool unjitterReprojection = false;
    [SerializeField] bool useYCoCg = false;
    [SerializeField] bool useDilation = true;

    [SerializeField] [Range(0.0f, 1.0f)] float feedbackMin = 0.92f;
    [SerializeField] [Range(0.0f, 1.0f)] float feedbackMax = 0.95f;

    [SerializeField] bool varianceClipping = true;
    [SerializeField] bool adaptiveClipping = true;
    [SerializeField] bool velocity_debug = false;
    [SerializeField] [Range(0.01f, 99.9f)] float varianceClippingGamma = 1.0f;
    [SerializeField] [Range(0.01f, 9.9f)] float varianceClippingGammaMin = 0.666f;
    [SerializeField] [Range(0.01f, 1.0f)] float chromaShrinkageFactor = 0.125f;

    [SerializeField]
    [Range(0.01f, 100.0f)]
    float velocityWeight = 10;

    [SerializeField]
    [Range(0.01f, 100.0f)]
    float clippingPhaseInFactor = 1;

    [SerializeField] bool useMotionBlur = false;
    [SerializeField] [Range(0.0f, 2.0f)] float motionBlurStrength = 1.0f;
    [SerializeField] bool motionBlurIgnoreFF = false;
    [SerializeField] RenderTextureFormat render_texture_format = RenderTextureFormat.ARGB32;
    [SerializeField] FilterMode filtering = FilterMode.Bilinear;
    [SerializeField] Boolean shrinkChrome = true;
    [SerializeField] Boolean luminanceOrientBox = false;
    [SerializeField] Boolean clipTowardsCenter = false;

    void OnPreCull() { this._frustumJitter?.OnPreCull(ref this._camera); }

    void OnPreRender() { this._velocityBuffer?.OnPreRender(ref this._camera); }

    void OnPostRender() { this._velocityBuffer?.OnPostRender(ref this._camera, ref this._frustumJitter); }

    void Reset() {
      if (!this._camera) {
        this._camera = Camera.main;
      }

      if (!this._reprojection_shader) {
        this._reprojection_shader = Shader.Find("Playdead/Post/TemporalReprojection");
      }

      if (this._velocityBuffer == null) {
        this._frustumJitter = new FrustumJitter();
      }

      if (this._velocityBuffer == null) {
        this._velocityBuffer = new VelocityBuffer();
      }
    }

    void Setup() {
      this.Reset();
      this.Clear();

      this._velocityBuffer?.Start();
    }

    void Clear() {
      TaaUtilities.EnsureArray(ref this._reprojection_index, 2);
      this._reprojection_index[0] = -1;
      this._reprojection_index[1] = -1;

      this._frustumJitter?.Clear(ref this._camera);
      this._velocityBuffer?.Clear();
    }

    void Awake() { this.Setup(); }

    void Resolve(RenderTexture source, RenderTexture destination) {
      TaaUtilities.EnsureArray(ref this._reprojection_buffer, 2, 2);
      TaaUtilities.EnsureArray(ref this._reprojection_index, 2, initial_value : -1);

      TaaUtilities.EnsureMaterial(ref this._reprojection_material, this._reprojection_shader);
      if (this._reprojection_material == null) {
        Graphics.Blit(source, destination);
        return;
      }

      #if SUPPORT_STEREO
      var eye_index = this._camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right ? 1 : 0;
      #else
        int eyeIndex = 0;
      #endif
      var buffer_w = source.width;
      var buffer_h = source.height;

      if (TaaUtilities.EnsureRenderTarget(ref this._reprojection_buffer[eye_index, 0],
                                          buffer_w,
                                          buffer_h,
                                          this.render_texture_format,
                                          this.filtering,
                                          anti_aliasing : source.antiAliasing)) {
        this.Clear();
      }

      if (TaaUtilities.EnsureRenderTarget(ref this._reprojection_buffer[eye_index, 1],
                                          buffer_w,
                                          buffer_h,
                                          this.render_texture_format,
                                          this.filtering,
                                          anti_aliasing : source.antiAliasing)) {
        this.Clear();
      }

      #if SUPPORT_STEREO
        var stereo_enabled = this._camera.stereoEnabled;
      #else
        bool stereo_enabled = false;
      #endif
      #if UNITY_EDITOR
        var allow_motion_blur = !stereo_enabled && Application.isPlaying;
      #else
        bool allow_motion_blur = !stereo_enabled;
      #endif

      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CAMERA_PERSPECTIVE",
                                 !this._camera.orthographic);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CAMERA_ORTHOGRAPHIC",
                                 this._camera.orthographic);

      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "MINMAX_3X3",
                                 this.neighborhood == Neighborhood.Min_max3_x3_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "MINMAX_3X3_ROUNDED",
                                 this.neighborhood == Neighborhood.Min_max3_x3_rounded_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "MINMAX_3X3_WEIGHTED",
                                 this.neighborhood == Neighborhood.Min_max3_x3_weighted_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "MINMAX_4TAP_VARYING",
                                 this.neighborhood == Neighborhood.Min_max4_tap_varying_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "INTRPL_NONE",
                                 this._history_interpolation == HistoryInterpolation.Interpolation_none_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "INTRPL_CATMULL_ROM",
                                 this._history_interpolation
                                 == HistoryInterpolation.Interpolation_cat_mull_rom_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "INTRPL_CATMULL_ROM_CUBIC",
                                 this._history_interpolation
                                 == HistoryInterpolation.Interpolation_cat_mull_rom_cubic_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "UNJITTER_COLORSAMPLES",
                                 this.unjitterColorSamples);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "UNJITTER_NEIGHBORHOOD",
                                 this.unjitterNeighborhood);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "UNJITTER_REPROJECTION",
                                 this.unjitterReprojection);

      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CLIPPING_NONE",
                                 this._clipping == Clipping.Clipping_none_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CLIPPING_DEFAULT",
                                 this._clipping == Clipping.Clipping_default_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CLIPPING_PHASIC",
                                 this._clipping == Clipping.Clipping_phasic_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CLIPPING_PHASIC_DIRECTIONAL",
                                 this._clipping == Clipping.Clipping_phasic_directional_);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "CLIPPING_PHASIC_VARIANCE",
                                 this._clipping == Clipping.Clipping_phasic_variance_);

      TaaUtilities.EnsureKeyword(this._reprojection_material, "CLIP_TOWARDS_CENTER", this.clipTowardsCenter);
      TaaUtilities.EnsureKeyword(this._reprojection_material, "VARIANCE_CLIPPING", this.varianceClipping);
      TaaUtilities.EnsureKeyword(this._reprojection_material, "ADAPTIVE_CLIPPING", this.adaptiveClipping);

      TaaUtilities.EnsureKeyword(this._reprojection_material, "USE_YCOCG", this.useYCoCg);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "YCOCG_SHRINK_CHROMA_MIN_MAX",
                                 this.shrinkChrome);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "YCOCG_ORIENT_IN_LUMINANCE_AXIS",
                                 this.luminanceOrientBox);

      TaaUtilities.EnsureKeyword(this._reprojection_material, "USE_DILATION", this.useDilation);
      TaaUtilities.EnsureKeyword(this._reprojection_material,
                                 "USE_MOTION_BLUR",
                                 this.useMotionBlur && allow_motion_blur);
      if (this._velocityBuffer != null) {
        TaaUtilities.EnsureKeyword(this._reprojection_material,
                                   "USE_MAX_NEIGHBOR_VELOCITY",
                                   this._velocityBuffer.ActiveVelocityNeighborMax != null);

        TaaUtilities.EnsureKeyword(this._reprojection_material, "VELOCITY_DEBUG", this.velocity_debug);
      }

      if (this._reprojection_index[eye_index] == -1) { // bootstrap
        this._reprojection_index[eye_index] = 0;
        this._reprojection_buffer[eye_index, this._reprojection_index[eye_index]].DiscardContents();
        Graphics.Blit(source, this._reprojection_buffer[eye_index, this._reprojection_index[eye_index]]);
      }

      var index_read = this._reprojection_index[eye_index];
      var index_write = (this._reprojection_index[eye_index] + 1) % 2;

      var jitter_uv = this._frustumJitter._ActiveSample;
      jitter_uv.x /= source.width;
      jitter_uv.y /= source.height;
      jitter_uv.z /= source.width;
      jitter_uv.w /= source.height;

      this._reprojection_material.SetVector(_jitter_uv, jitter_uv);
      this._reprojection_material.SetTexture(_velocity_buffer, this._velocityBuffer.ActiveVelocityBuffer);
      this._reprojection_material.SetTexture(_velocity_neighbor_max,
                                             this._velocityBuffer.ActiveVelocityNeighborMax);
      this._reprojection_material.SetTexture(_main_tex, source);
      this._reprojection_material.SetTexture(_prev_tex, this._reprojection_buffer[eye_index, index_read]);
      this._reprojection_material.SetFloat(_feedback_min, this.feedbackMin);
      this._reprojection_material.SetFloat(_feedback_max, this.feedbackMax);
      this._reprojection_material.SetFloat(_adaptive_clipping_gamma, this.varianceClippingGamma);
      this._reprojection_material.SetFloat(_adaptive_clipping_gamma_min, this.varianceClippingGammaMin);
      this._reprojection_material.SetFloat(_y_co_cg_chroma_shrinkage_factor, this.chromaShrinkageFactor);
      this._reprojection_material.SetFloat(_velocity_weight, this.velocityWeight);
      this._reprojection_material.SetFloat(_motion_scale,
                                           this.motionBlurStrength
                                           * (this.motionBlurIgnoreFF
                                                  ? Mathf.Min(1.0f, 1.0f / this._velocityBuffer.TimeScale)
                                                  : 1.0f));
      this._reprojection_material.SetFloat(_clipping_phase_in_factor, this.clippingPhaseInFactor);

      // reproject frame n-1 into output + history buffer
      _mrt[0] = this._reprojection_buffer[eye_index, index_write].colorBuffer;
      _mrt[1] = destination.colorBuffer;

      Graphics.SetRenderTarget(_mrt, source.depthBuffer);
      this._reprojection_material.SetPass(0);
      this._reprojection_buffer[eye_index, index_write].DiscardContents();

      TaaUtilities.DrawFullscreenQuad();

      this._reprojection_index[eye_index] = index_write;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination) {
      if (destination != null && source.antiAliasing == destination.antiAliasing) {
        // resolve without additional blit when not end of chain
        this.Resolve(source, destination);
      } else {
        var internal_destination = RenderTexture.GetTemporary(source.width,
                                                              source.height,
                                                              0,
                                                              this.render_texture_format,
                                                              RenderTextureReadWrite.Default,
                                                              source.antiAliasing);
        this.Resolve(source, internal_destination);
        Graphics.Blit(internal_destination, destination);
        RenderTexture.ReleaseTemporary(internal_destination);
      }
    }

    void OnApplicationQuit() {
      this._velocityBuffer.OnApplicationQuit();

      if (this._reprojection_buffer != null) {
        TaaUtilities.ReleaseRenderTarget(ref this._reprojection_buffer[0, 0]);
        TaaUtilities.ReleaseRenderTarget(ref this._reprojection_buffer[0, 1]);
        TaaUtilities.ReleaseRenderTarget(ref this._reprojection_buffer[1, 0]);
        TaaUtilities.ReleaseRenderTarget(ref this._reprojection_buffer[1, 1]);
      }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="NotImplementedException"></exception>
    public override void Render(PostProcessRenderContext context) { throw new NotImplementedException(); }
  }
}
#endif
