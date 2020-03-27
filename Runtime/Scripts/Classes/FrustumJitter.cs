// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

#if UNITY_5_5_OR_NEWER
#define SUPPORT_STEREO
#endif

using System;
using UnityEngine;

namespace PDTAAFork.Scripts.Classes {
  /// <summary>
  ///
  /// </summary>
  [Serializable]
  public class FrustumJitter {
    #region Static point data

    static float[] _points_still = {0.5f, 0.5f,};

    static float[] _points_uniform2 = {
                                          -0.25f,
                                          -0.25f, //ll
                                          0.25f,
                                          0.25f, //ur
                                      };

    static float[] _points_uniform4 = {
                                          -0.25f,
                                          -0.25f, //ll
                                          0.25f,
                                          -0.25f, //lr
                                          0.25f,
                                          0.25f, //ur
                                          -0.25f,
                                          0.25f, //ul
                                      };

    static float[] _points_uniform4_helix = {
                                                -0.25f,
                                                -0.25f, //ll  3  1
                                                0.25f,
                                                0.25f, //ur   \/|
                                                0.25f,
                                                -0.25f, //lr   /\|
                                                -0.25f,
                                                0.25f, //ul  0  2
                                            };

    static float[] _points_uniform4_double_helix = {
                                                       -0.25f,
                                                       -0.25f, //ll  3  1
                                                       0.25f,
                                                       0.25f, //ur   \/|
                                                       0.25f,
                                                       -0.25f, //lr   /\|
                                                       -0.25f,
                                                       0.25f, //ul  0  2
                                                       -0.25f,
                                                       -0.25f, //ll  6--7
                                                       0.25f,
                                                       -0.25f, //lr   \
                                                       -0.25f,
                                                       0.25f, //ul    \
                                                       0.25f,
                                                       0.25f, //ur  4--5
                                                   };

    static float[] _points_skew_butterfly = {
                                                -0.250f,
                                                -0.250f,
                                                0.250f,
                                                0.250f,
                                                0.125f,
                                                -0.125f,
                                                -0.125f,
                                                0.125f,
                                            };

    static float[] _points_rotated4 = {
                                          -0.125f,
                                          -0.375f, //ll
                                          0.375f,
                                          -0.125f, //lr
                                          0.125f,
                                          0.375f, //ur
                                          -0.375f,
                                          0.125f, //ul
                                      };

    static float[] _points_rotated4_helix = {
                                                -0.125f,
                                                -0.375f, //ll  3  1
                                                0.125f,
                                                0.375f, //ur   \/|
                                                0.375f,
                                                -0.125f, //lr   /\|
                                                -0.375f,
                                                0.125f, //ul  0  2
                                            };

    static float[] _points_rotated4_helix2 = {
                                                 -0.125f,
                                                 -0.375f, //ll  2--1
                                                 0.125f,
                                                 0.375f, //ur   \/
                                                 -0.375f,
                                                 0.125f, //ul   /\
                                                 0.375f,
                                                 -0.125f, //lr  0  3
                                             };

    static float[] _points_poisson10 = {
                                           -0.16795960f * 0.25f,
                                           0.65544910f * 0.25f,
                                           -0.69096030f * 0.25f,
                                           0.59015970f * 0.25f,
                                           0.49843820f * 0.25f,
                                           0.83099720f * 0.25f,
                                           0.17230150f * 0.25f,
                                           -0.03882703f * 0.25f,
                                           -0.60772670f * 0.25f,
                                           -0.06013587f * 0.25f,
                                           0.65606390f * 0.25f,
                                           0.24007600f * 0.25f,
                                           0.80348370f * 0.25f,
                                           -0.48096900f * 0.25f,
                                           0.33436540f * 0.25f,
                                           -0.73007030f * 0.25f,
                                           -0.47839520f * 0.25f,
                                           -0.56005300f * 0.25f,
                                           -0.12388120f * 0.25f,
                                           -0.96633990f * 0.25f,
                                       };

    static float[] _points_pentagram = {
                                           0.000000f * 0.5f,
                                           0.525731f * 0.5f, // head
                                           -0.309017f * 0.5f,
                                           -0.425325f * 0.5f, // lleg
                                           0.500000f * 0.5f,
                                           0.162460f * 0.5f, // rarm
                                           -0.500000f * 0.5f,
                                           0.162460f * 0.5f, // larm
                                           0.309017f * 0.5f,
                                           -0.425325f * 0.5f, // rleg
                                       };

    static float[] _points_halton_2_3_x8 = new float[8 * 2];
    static float[] _points_halton_2_3_x16 = new float[16 * 2];
    static float[] _points_halton_2_3_x32 = new float[32 * 2];
    static float[] _points_halton_2_3_x256 = new float[256 * 2];

    static float[] _points_motion_perp2 = {
                                              0.00f,
                                              -0.25f,
                                              0.00f,
                                              0.25f,
                                          };

    #endregion

    #region Static point data, static initialization

    static void TransformPattern(float[] seq, float theta, float scale) {
      var cs = Mathf.Cos(theta);
      var sn = Mathf.Sin(theta);
      for (int i = 0, j = 1, n = seq.Length; i != n; i += 2, j += 2) {
        var x = scale * seq[i];
        var y = scale * seq[j];
        seq[i] = x * cs - y * sn;
        seq[j] = x * sn + y * cs;
      }
    }

    // http://en.wikipedia.org/wiki/Halton_sequence
    static float HaltonSeq(int prime, int index = 1 /* NOT! zero-based */) {
      var r = 0.0f;
      var f = 1.0f;
      var i = index;
      while (i > 0) {
        f /= prime;
        r += f * (i % prime);
        i = (int)Mathf.Floor(i / (float)prime);
      }

      return r;
    }

    static void InitializeHalton_2_3(float[] seq) {
      for (int i = 0, n = seq.Length / 2; i != n; i++) {
        var u = HaltonSeq(2, i + 1) - 0.5f;
        var v = HaltonSeq(3, i + 1) - 0.5f;
        seq[2 * i + 0] = u;
        seq[2 * i + 1] = v;
      }
    }

    static FrustumJitter() {
      // points_Pentagram
      var vh = new Vector2(_points_pentagram[0] - _points_pentagram[2],
                           _points_pentagram[1] - _points_pentagram[3]);
      var vu = new Vector2(0.0f, 1.0f);
      TransformPattern(_points_pentagram, Mathf.Deg2Rad * (0.5f * Vector2.Angle(vu, vh)), 1.0f);

      // points_Halton_2_3_xN
      InitializeHalton_2_3(_points_halton_2_3_x8);
      InitializeHalton_2_3(_points_halton_2_3_x16);
      InitializeHalton_2_3(_points_halton_2_3_x32);
      InitializeHalton_2_3(_points_halton_2_3_x256);
    }

    #endregion

    #region Static point data accessors

    /// <summary>
    ///
    /// </summary>
    public enum Pattern {
      Still_,
      Uniform2_,
      Uniform4_,
      Uniform4_helix_,
      Uniform4_double_helix_,
      Skew_butterfly_,
      Rotated4_,
      Rotated4_helix_,
      Rotated4_helix2_,
      Poisson10_,
      Pentagram_,
      Halton_2_3_x8_,
      Halton_2_3_x16_,
      Halton_2_3_x32_,
      Halton_2_3_x256_,
      Motion_perp2_,
    };

    static float[] AccessPointData(Pattern pattern) {
      switch (pattern) {
        case Pattern.Still_:
          return _points_still;
        case Pattern.Uniform2_:
          return _points_uniform2;
        case Pattern.Uniform4_:
          return _points_uniform4;
        case Pattern.Uniform4_helix_:
          return _points_uniform4_helix;
        case Pattern.Uniform4_double_helix_:
          return _points_uniform4_double_helix;
        case Pattern.Skew_butterfly_:
          return _points_skew_butterfly;
        case Pattern.Rotated4_:
          return _points_rotated4;
        case Pattern.Rotated4_helix_:
          return _points_rotated4_helix;
        case Pattern.Rotated4_helix2_:
          return _points_rotated4_helix2;
        case Pattern.Poisson10_:
          return _points_poisson10;
        case Pattern.Pentagram_:
          return _points_pentagram;
        case Pattern.Halton_2_3_x8_:
          return _points_halton_2_3_x8;
        case Pattern.Halton_2_3_x16_:
          return _points_halton_2_3_x16;
        case Pattern.Halton_2_3_x32_:
          return _points_halton_2_3_x32;
        case Pattern.Halton_2_3_x256_:
          return _points_halton_2_3_x256;
        case Pattern.Motion_perp2_:
          return _points_motion_perp2;
        default:
          Debug.LogError("missing point distribution");
          return _points_halton_2_3_x16;
      }
    }

    public static int AccessLength(Pattern p) { return AccessPointData(p).Length / 2; }

    public Vector2 Sample(Pattern p, int index) {
      var points = AccessPointData(p);
      var n = points.Length / 2;
      var i = index % n;

      this.scalar = new Vector2(this.patternScale, this.patternScale);

      if (this.motionAdaptiveJitter) {
        this.scalar.x =
            Mathf.Max(this.scalar.x
                      * (1
                         / ((Mathf.Abs(this._cam_pos_delta.x) + this.camWorldPosDelta.magnitude)
                            * this.adaptiveJitterStrength
                            * 100
                            + 1)),
                      this.adaptiveJitterMin);
        this.scalar.y =
            Mathf.Max(this.scalar.y
                      * (1
                         / ((Mathf.Abs(this._cam_pos_delta.y) + this.camWorldPosDelta.magnitude)
                            * this.adaptiveJitterStrength
                            * 100
                            + 1)),
                      this.adaptiveJitterMin);
      }

      var x = this.scalar.x * points[2 * i + 0];
      var y = this.scalar.y * points[2 * i + 1];

      if (p != Pattern.Motion_perp2_) {
        return new Vector2(x, y);
      }

      return new Vector2(x, y).Rotate(Vector2.right.SignedAngle(this._focal_motion_dir));
    }

    #endregion

    Vector3 _focal_motion_pos = Vector3.zero;
    Vector3 _focal_motion_dir = Vector3.right;
    Vector3 _cam_pos_delta = Vector3.zero;

    [SerializeField] Pattern pattern = Pattern.Halton_2_3_x16_;
    [SerializeField] [Range(0.0f, 9.9f)] float patternScale = .8f;
    [SerializeField] bool motionAdaptiveJitter = true;
    [SerializeField] [Range(0.0f, 9.9f)] float adaptiveJitterMin = .2f;
    [SerializeField] [Range(0.0f, 9.9f)] float adaptiveJitterStrength = 0.39f;

    [NonSerialized, HideInInspector]
    public Vector4 _ActiveSample = Vector4.zero; // xy = current sample, zw = previous sample

    [NonSerialized, HideInInspector] public int _ActiveIndex = -2;

    [SerializeField] Vector2 scalar;

    [SerializeField] Vector3 camWorldPosDelta;
    [SerializeField] Vector3 oldCamWorldPos;

    public void OnPreCull(ref Camera camera) {
      camera.ResetWorldToCameraMatrix();
      camera.ResetProjectionMatrix();
      camera.nonJitteredProjectionMatrix = camera.projectionMatrix;

      // update motion dir

      var old_world = this._focal_motion_pos;
      var new_world = camera.transform.TransformVector(camera.nearClipPlane * Vector3.forward);
      this._focal_motion_pos = new_world;

      this.camWorldPosDelta = this.oldCamWorldPos - camera.transform.position;
      this.oldCamWorldPos = camera.transform.position;

      Vector3 old_point = camera.worldToCameraMatrix * old_world;
      Vector3 new_point = camera.worldToCameraMatrix * new_world;

      this._cam_pos_delta = new_point - old_point;
      var new_delta = this._cam_pos_delta.WithZ(0.0f);

      var mag = new_delta.magnitude;
      if (mag != 0.0f) {
        var dir = new_delta / mag; // yes, apparently this is necessary instead of newDelta.normalized...
        // because facepalm
        if (dir.sqrMagnitude != 0.0f) {
          this._focal_motion_dir = Vector3.Slerp(this._focal_motion_dir, dir, 0.2f);
          //Debug.Log("CHANGE focalMotionDir " + focalMotionDir.ToString("G4") + " delta was " + newDelta.ToString("G4") + " delta.mag " + newDelta.magnitude);
        }
      }

      // update jitter
      #if SUPPORT_STEREO
      if (camera.stereoEnabled) {
        this.Clear(ref camera);
      } else
          #endif
      {
        if (this._ActiveIndex == -2) {
          this._ActiveSample = Vector4.zero;
          this._ActiveIndex += 1;

          camera.projectionMatrix = camera.GetProjectionMatrix();
        } else {
          this._ActiveIndex += 1;
          this._ActiveIndex %= AccessLength(this.pattern);

          var sample = this.Sample(this.pattern, this._ActiveIndex);
          this._ActiveSample.z = this._ActiveSample.x;
          this._ActiveSample.w = this._ActiveSample.y;
          this._ActiveSample.x = sample.x;
          this._ActiveSample.y = sample.y;

          camera.projectionMatrix = camera.GetProjectionMatrix(sample.x, sample.y);
        }
      }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="camera"></param>
    public void Clear(ref Camera camera) {
      camera?.ResetProjectionMatrix();

      this._ActiveSample = Vector4.zero;
      this._ActiveIndex = -2;
    }
  }
}