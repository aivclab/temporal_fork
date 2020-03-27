// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

#if UNITY_5_5_OR_NEWER
#define SUPPORT_STEREO
#endif

using System;
using PDTAAFork.Scripts.MonoBehaviours;
using UnityEngine;

namespace PDTAAFork.Scripts.Classes {
  [Serializable]
  public class VelocityBuffer {
    #if UNITY_PS4
    private const RenderTextureFormat velocityFormat = RenderTextureFormat.RGHalf;
    #else
    const RenderTextureFormat _velocity_format = RenderTextureFormat.RGFloat;
    #endif

    Shader _velocity_shader;
    Material _velocity_material;
    RenderTexture[] _velocity_buffer;
    RenderTexture[] _velocity_neighbor_max;

    bool[] _param_initialized;
    Vector4[] _param_projection_extents;
    Matrix4x4[] _param_curr_v;
    Matrix4x4[] _param_curr_vp;
    Matrix4x4[] _param_prev_vp;
    Matrix4x4[] _param_prev_vp_no_flip;

    int _active_eye_index = -1;

    public RenderTexture ActiveVelocityBuffer {
      get { return this._active_eye_index != -1 ? this._velocity_buffer[this._active_eye_index] : null; }
    }

    public RenderTexture ActiveVelocityNeighborMax {
      get {
        return this._active_eye_index != -1 ? this._velocity_neighbor_max[this._active_eye_index] : null;
      }
    }

    public enum NeighborMaxSupport {
      Tile_size10_,
      Tile_size20_,
      Tile_size40_,
    };

    [SerializeField] bool neighborMaxGen = true;
    [SerializeField] NeighborMaxSupport neighborMaxSupport = NeighborMaxSupport.Tile_size20_;
    [SerializeField] bool useUnityVelocityBuffer = true;

    float _time_scale_next_frame;
    public float TimeScale { get; private set; }

    #if UNITY_EDITOR && PD_TAA_FORK_DEBUG
    [Header("Stats")] [SerializeField] int numResident = 0;
    [SerializeField] int numRendered = 0;
    [SerializeField] int numDrawCalls = 0;
    #endif

    static readonly int _prev_vp_no_flip = Shader.PropertyToID("_PrevVP_NoFlip");
    static readonly int _prev_vp = Shader.PropertyToID("_PrevVP");
    static readonly int _curr_vp = Shader.PropertyToID("_CurrVP");
    static readonly int _curr_v = Shader.PropertyToID("_CurrV");
    static readonly int _projection_extents = Shader.PropertyToID("_ProjectionExtents");
    static readonly int _prev_m = Shader.PropertyToID("_PrevM");
    static readonly int _curr_m = Shader.PropertyToID("_CurrM");
    static readonly int _velocity_tex = Shader.PropertyToID("_VelocityTex");
    static readonly int _velocity_tex_texel_size = Shader.PropertyToID("_VelocityTex_TexelSize");

    public void Clear() {
      TaaUtilities.EnsureArray(ref this._param_initialized, 2);
      this._param_initialized[0] = false;
      this._param_initialized[1] = false;
    }

    public void Start() {
      this._time_scale_next_frame = Time.timeScale;

      if (!this._velocity_shader) {
        this._velocity_shader = Shader.Find("Playdead/Post/VelocityBuffer");
      }
    }

    public void OnPreRender(ref Camera camera) {
      TaaUtilities.EnsureDepthTexture(camera);

      if (this.useUnityVelocityBuffer) {
        camera.depthTextureMode |= DepthTextureMode.Depth;
        camera.depthTextureMode |= DepthTextureMode.MotionVectors;
      }
    }

    public void OnPostRender(ref Camera camera, ref FrustumJitter frustum_jitter) {
      TaaUtilities.EnsureArray(ref this._velocity_buffer, 2);
      TaaUtilities.EnsureArray(ref this._velocity_neighbor_max, 2);

      TaaUtilities.EnsureArray(ref this._param_initialized, 2, initial_value : false);
      TaaUtilities.EnsureArray(ref this._param_projection_extents, 2);
      TaaUtilities.EnsureArray(ref this._param_curr_v, 2);
      TaaUtilities.EnsureArray(ref this._param_curr_vp, 2);
      TaaUtilities.EnsureArray(ref this._param_prev_vp, 2);
      TaaUtilities.EnsureArray(ref this._param_prev_vp_no_flip, 2);

      TaaUtilities.EnsureMaterial(ref this._velocity_material, this._velocity_shader);
      if (this._velocity_material == null) {
        return;
      }

      this.TimeScale = this._time_scale_next_frame;
      this._time_scale_next_frame = Time.timeScale == 0.0f ? this._time_scale_next_frame : Time.timeScale;

      #if SUPPORT_STEREO
      var eye_index = camera.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right ? 1 : 0;
      #else
        int eyeIndex = 0;
      #endif
      var buffer_w = camera.pixelWidth;
      var buffer_h = camera.pixelHeight;

      if (TaaUtilities.EnsureRenderTarget(ref this._velocity_buffer[eye_index],
                                          buffer_w,
                                          buffer_h,
                                          _velocity_format,
                                          FilterMode.Point,
                                          depth_bits : 16)) {
        this.Clear();
      }

      TaaUtilities.EnsureKeyword(this._velocity_material, "CAMERA_PERSPECTIVE", !camera.orthographic);
      TaaUtilities.EnsureKeyword(this._velocity_material, "CAMERA_ORTHOGRAPHIC", camera.orthographic);

      TaaUtilities.EnsureKeyword(this._velocity_material,
                                 "TILESIZE_10",
                                 this.neighborMaxSupport == NeighborMaxSupport.Tile_size10_);
      TaaUtilities.EnsureKeyword(this._velocity_material,
                                 "TILESIZE_20",
                                 this.neighborMaxSupport == NeighborMaxSupport.Tile_size20_);
      TaaUtilities.EnsureKeyword(this._velocity_material,
                                 "TILESIZE_40",
                                 this.neighborMaxSupport == NeighborMaxSupport.Tile_size40_);

      TaaUtilities.EnsureKeyword(this._velocity_material,
                                 "USE_UNITY_VELOCITY_BUFFER",
                                 this.useUnityVelocityBuffer);

      #if SUPPORT_STEREO
      if (camera.stereoEnabled) {
        for (var i = 0; i != 2; i++) {
          var eye = (Camera.StereoscopicEye)i;

          var curr_v = camera.GetStereoViewMatrix(eye);
          var curr_p = GL.GetGPUProjectionMatrix(camera.GetStereoProjectionMatrix(eye), true);
          var curr_p_no_flip = GL.GetGPUProjectionMatrix(camera.GetStereoProjectionMatrix(eye), false);
          var prev_v = this._param_initialized[i] ? this._param_curr_v[i] : curr_v;

          this._param_initialized[i] = true;
          this._param_projection_extents[i] = camera.GetProjectionExtents(eye);
          this._param_curr_v[i] = curr_v;
          this._param_curr_vp[i] = curr_p * curr_v;
          this._param_prev_vp[i] = curr_p * prev_v;
          this._param_prev_vp_no_flip[i] = curr_p_no_flip * prev_v;
        }
      } else
          #endif
      {
        var curr_v = camera.worldToCameraMatrix;
        var curr_p = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        var curr_p_no_flip = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        var prev_v = this._param_initialized[0] ? this._param_curr_v[0] : curr_v;

        this._param_initialized[0] = true;
        this._param_projection_extents[0] = frustum_jitter != null
                                                ? camera.GetProjectionExtents(frustum_jitter._ActiveSample.x,
                                                                              frustum_jitter._ActiveSample.y)
                                                : camera.GetProjectionExtents();
        this._param_curr_v[0] = curr_v;
        this._param_curr_vp[0] = curr_p * curr_v;
        this._param_prev_vp[0] = curr_p * prev_v;
        this._param_prev_vp_no_flip[0] = curr_p_no_flip * prev_v;
      }

      var active_rt = RenderTexture.active;
      RenderTexture.active = this._velocity_buffer[eye_index];

      GL.Clear(true, true, Color.black);

      const int k_prepass = 0;
      const int k_vertices = 1;
      const int k_vertices_skinned = 2;
      const int k_tile_max = 3;
      const int k_neighbor_max = 4;

      // 0: prepass
      #if SUPPORT_STEREO
      this._velocity_material.SetVectorArray(_projection_extents, this._param_projection_extents);
      this._velocity_material.SetMatrixArray(_curr_v, this._param_curr_v);
      this._velocity_material.SetMatrixArray(_curr_vp, this._param_curr_vp);
      this._velocity_material.SetMatrixArray(_prev_vp, this._param_prev_vp);
      this._velocity_material.SetMatrixArray(_prev_vp_no_flip, this._param_prev_vp_no_flip);
      #else
          velocityMaterial.SetVector(ProjectionExtents, paramProjectionExtents[0]);
          velocityMaterial.SetMatrix(CurrV, paramCurrV[0]);
          velocityMaterial.SetMatrix(CurrVp, paramCurrVP[0]);
          velocityMaterial.SetMatrix(PrevVp, paramPrevVP[0]);
          velocityMaterial.SetMatrix(PrevVpNoFlip, paramPrevVP_NoFlip[0]);
      #endif

      this._velocity_material.SetPass(k_prepass);
      TaaUtilities.DrawFullscreenQuad();

      // 1 + 2: vertices + vertices skinned
      var obs = VelocityBufferTag._ActiveObjects;
      #if UNITY_EDITOR && PD_TAA_FORK_DEBUG
      this.numResident = obs.Count;
      this.numRendered = 0;
      this.numDrawCalls = 0;
      #endif
      for (int i = 0, n = obs.Count; i != n; i++) {
        var ob = obs[i];
        if (ob != null && ob.Rendering && ob._Mesh != null) {
          this._velocity_material.SetMatrix(_curr_m, ob._LocalToWorldCurr);
          this._velocity_material.SetMatrix(_prev_m, ob._LocalToWorldPrev);
          this._velocity_material.SetPass(ob._MeshSmrActive ? k_vertices_skinned : k_vertices);

          for (var j = 0; j != ob._Mesh.subMeshCount; j++) {
            Graphics.DrawMeshNow(ob._Mesh, Matrix4x4.identity, j);
            #if UNITY_EDITOR && PD_TAA_FORK_DEBUG
            this.numDrawCalls++;
            #endif
          }
          #if UNITY_EDITOR && PD_TAA_FORK_DEBUG
          this.numRendered++;
          #endif
        }
      }

      // 3 + 4: tilemax + neighbormax
      if (this.neighborMaxGen) {
        var tile_size = 1;

        switch (this.neighborMaxSupport) {
          case NeighborMaxSupport.Tile_size10_:
            tile_size = 10;
            break;
          case NeighborMaxSupport.Tile_size20_:
            tile_size = 20;
            break;
          case NeighborMaxSupport.Tile_size40_:
            tile_size = 40;
            break;
        }

        var neighbor_max_w = buffer_w / tile_size;
        var neighbor_max_h = buffer_h / tile_size;

        TaaUtilities.EnsureRenderTarget(ref this._velocity_neighbor_max[eye_index],
                                        neighbor_max_w,
                                        neighbor_max_h,
                                        _velocity_format,
                                        FilterMode.Bilinear);

        // tilemax
        var tile_max = RenderTexture.GetTemporary(neighbor_max_w,
                                                  neighbor_max_h,
                                                  0,
                                                  _velocity_format);
        RenderTexture.active = tile_max;
        {
          this._velocity_material.SetTexture(_velocity_tex, this._velocity_buffer[eye_index]);
          this._velocity_material.SetVector(_velocity_tex_texel_size,
                                            new Vector4(1.0f / buffer_w,
                                                        1.0f / buffer_h,
                                                        0.0f,
                                                        0.0f));
          this._velocity_material.SetPass(k_tile_max);
          TaaUtilities.DrawFullscreenQuad();
        }

        // neighbormax
        RenderTexture.active = this._velocity_neighbor_max[eye_index];
        {
          this._velocity_material.SetTexture(_velocity_tex, tile_max);
          this._velocity_material.SetVector(_velocity_tex_texel_size,
                                            new Vector4(1.0f / neighbor_max_w,
                                                        1.0f / neighbor_max_h,
                                                        0.0f,
                                                        0.0f));
          this._velocity_material.SetPass(k_neighbor_max);
          TaaUtilities.DrawFullscreenQuad();
        }

        RenderTexture.ReleaseTemporary(tile_max);
      } else {
        TaaUtilities.ReleaseRenderTarget(ref this._velocity_neighbor_max[0]);
        TaaUtilities.ReleaseRenderTarget(ref this._velocity_neighbor_max[1]);
      }

      RenderTexture.active = active_rt;

      this._active_eye_index = eye_index;
    }

    public void OnApplicationQuit() {
      if (this._velocity_buffer != null) {
        TaaUtilities.ReleaseRenderTarget(ref this._velocity_buffer[0]);
        TaaUtilities.ReleaseRenderTarget(ref this._velocity_buffer[1]);
      }

      if (this._velocity_neighbor_max != null) {
        TaaUtilities.ReleaseRenderTarget(ref this._velocity_neighbor_max[0]);
        TaaUtilities.ReleaseRenderTarget(ref this._velocity_neighbor_max[1]);
      }
    }
  }
}