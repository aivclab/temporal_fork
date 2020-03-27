// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

#if UNITY_5_5_OR_NEWER
#define SUPPORT_STEREO
#endif
using UnityEngine;

namespace PDTAAFork.Scripts.Classes {
  public static class Vector2Extension {
    // positive if v2 is on the left side of v1
    public static float SignedAngle(this Vector2 v1, Vector2 v2) {
      var n1 = v1.normalized;
      var n2 = v2.normalized;

      var dot = Vector2.Dot(n1, n2);
      if (dot > 1.0f) {
        dot = 1.0f;
      }

      if (dot < -1.0f) {
        dot = -1.0f;
      }

      var theta = Mathf.Acos(dot);
      var sgn = Vector2.Dot(new Vector2(-n1.y, n1.x), n2);
      if (sgn >= 0.0f) {
        return theta;
      }

      return -theta;
    }

    public static Vector2 Rotate(this Vector2 v, float theta) {
      var cs = Mathf.Cos(theta);
      var sn = Mathf.Sin(theta);
      var x1 = v.x * cs - v.y * sn;
      var y1 = v.x * sn + v.y * cs;
      return new Vector2(x1, y1);
    }
  }

  public static class Vector3Extension {
    public static Vector3 WithX(this Vector3 v, float x) { return new Vector3(x, v.y, v.z); }

    public static Vector3 WithY(this Vector3 v, float y) { return new Vector3(v.x, y, v.z); }

    public static Vector3 WithZ(this Vector3 v, float z) { return new Vector3(v.x, v.y, z); }
  }

  public static class Matrix4X4Extension {
    public static Matrix4x4 GetPerspectiveProjection(float left,
                                                     float right,
                                                     float bottom,
                                                     float top,
                                                     float near,
                                                     float far) {
      var x = 2.0f * near / (right - left);
      var y = 2.0f * near / (top - bottom);
      var a = (right + left) / (right - left);
      var b = (top + bottom) / (top - bottom);
      var c = -(far + near) / (far - near);
      var d = -(2.0f * far * near) / (far - near);
      var e = -1.0f;

      var m = new Matrix4x4();
      m[0, 0] = x;
      m[0, 1] = 0;
      m[0, 2] = a;
      m[0, 3] = 0;
      m[1, 0] = 0;
      m[1, 1] = y;
      m[1, 2] = b;
      m[1, 3] = 0;
      m[2, 0] = 0;
      m[2, 1] = 0;
      m[2, 2] = c;
      m[2, 3] = d;
      m[3, 0] = 0;
      m[3, 1] = 0;
      m[3, 2] = e;
      m[3, 3] = 0;
      return m;
    }

    public static Matrix4x4 GetOrthographicProjection(float left,
                                                      float right,
                                                      float bottom,
                                                      float top,
                                                      float near,
                                                      float far) {
      var x = 2.0f / (right - left);
      var y = 2.0f / (top - bottom);
      var z = -2.0f / (far - near);
      var a = -(right + left) / (right - left);
      var b = -(top + bottom) / (top - bottom);
      var c = -(far + near) / (far - near);
      var d = 1.0f;

      var m = new Matrix4x4();
      m[0, 0] = x;
      m[0, 1] = 0;
      m[0, 2] = 0;
      m[0, 3] = a;
      m[1, 0] = 0;
      m[1, 1] = y;
      m[1, 2] = 0;
      m[1, 3] = b;
      m[2, 0] = 0;
      m[2, 1] = 0;
      m[2, 2] = z;
      m[2, 3] = c;
      m[3, 0] = 0;
      m[3, 1] = 0;
      m[3, 2] = 0;
      m[3, 3] = d;
      return m;
    }
  }

  public static class CameraExtension {
    public static Vector4 GetProjectionExtents(this Camera camera) {
      return GetProjectionExtents(camera, 0.0f, 0.0f);
    }

    public static Vector4
        GetProjectionExtents(this Camera camera, float texel_offset_x, float texel_offset_y) {
      if (camera == null) {
        return Vector4.zero;
      }

      var one_extent_y = camera.orthographic
                             ? camera.orthographicSize
                             : Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView);
      var one_extent_x = one_extent_y * camera.aspect;
      var texel_size_x = one_extent_x / (0.5f * camera.pixelWidth);
      var texel_size_y = one_extent_y / (0.5f * camera.pixelHeight);
      var one_jitter_x = texel_size_x * texel_offset_x;
      var one_jitter_y = texel_size_y * texel_offset_y;

      return new Vector4(one_extent_x,
                         one_extent_y,
                         one_jitter_x,
                         one_jitter_y); // xy = frustum extents at distance 1, zw = jitter at distance 1
    }

    #if SUPPORT_STEREO
    public static Vector4 GetProjectionExtents(this Camera camera, Camera.StereoscopicEye eye) {
      return GetProjectionExtents(camera,
                                  eye,
                                  0.0f,
                                  0.0f);
    }

    public static Vector4 GetProjectionExtents(this Camera camera,
                                               Camera.StereoscopicEye eye,
                                               float texel_offset_x,
                                               float texel_offset_y) {
      var inv = Matrix4x4.Inverse(camera.GetStereoProjectionMatrix(eye));
      var ray00 = inv.MultiplyPoint3x4(new Vector3(-1.0f, -1.0f, 0.95f));
      var ray11 = inv.MultiplyPoint3x4(new Vector3(1.0f, 1.0f, 0.95f));

      ray00 /= -ray00.z;
      ray11 /= -ray11.z;

      var one_extent_x = 0.5f * (ray11.x - ray00.x);
      var one_extent_y = 0.5f * (ray11.y - ray00.y);
      var texel_size_x = one_extent_x / (0.5f * camera.pixelWidth);
      var texel_size_y = one_extent_y / (0.5f * camera.pixelHeight);
      var one_jitter_x = 0.5f * (ray11.x + ray00.x) + texel_size_x * texel_offset_x;
      var one_jitter_y = 0.5f * (ray11.y + ray00.y) + texel_size_y * texel_offset_y;

      return new Vector4(one_extent_x,
                         one_extent_y,
                         one_jitter_x,
                         one_jitter_y); // xy = frustum extents at distance 1, zw = jitter at distance 1
    }
    #endif

    public static Matrix4x4 GetProjectionMatrix(this Camera camera) {
      return GetProjectionMatrix(camera, 0.0f, 0.0f);
    }

    public static Matrix4x4 GetProjectionMatrix(this Camera camera,
                                                float texel_offset_x,
                                                float texel_offset_y) {
      if (camera == null) {
        return Matrix4x4.identity;
      }

      var extents = GetProjectionExtents(camera, texel_offset_x, texel_offset_y);

      var cf = camera.farClipPlane;
      var cn = camera.nearClipPlane;
      var xm = extents.z - extents.x;
      var xp = extents.z + extents.x;
      var ym = extents.w - extents.y;
      var yp = extents.w + extents.y;

      if (camera.orthographic) {
        return Matrix4X4Extension.GetOrthographicProjection(xm,
                                                            xp,
                                                            ym,
                                                            yp,
                                                            cn,
                                                            cf);
      }

      return Matrix4X4Extension.GetPerspectiveProjection(xm * cn,
                                                         xp * cn,
                                                         ym * cn,
                                                         yp * cn,
                                                         cn,
                                                         cf);
    }
  }
}