// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

using UnityEngine;

namespace PDTAAFork.Scripts.Classes {
  /// <summary>
  ///
  /// </summary>
  public static class TaaUtilities {
    /// <summary>
    ///
    /// </summary>
    /// <param name="shader_name"></param>
    /// <returns></returns>
    public static Material CreateMaterial(string shader_name) {
      if (string.IsNullOrEmpty(shader_name)) {
        return null;
      }

      var material = new Material(Shader.Find(shader_name)) {hideFlags = HideFlags.HideAndDontSave};
      return material;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="array"></param>
    /// <param name="size"></param>
    /// <param name="initial_value"></param>
    /// <typeparam name="T"></typeparam>
    public static void EnsureArray<T>(ref T[] array, int size, T initial_value = default(T)) {
      if (array == null || array.Length != size) {
        array = new T[size];
        for (var i = 0; i != size; i++) {
          array[i] = initial_value;
        }
      }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="array"></param>
    /// <param name="size0"></param>
    /// <param name="size1"></param>
    /// <param name="default_value"></param>
    /// <typeparam name="T"></typeparam>
    public static void EnsureArray<T>(ref T[,] array, int size0, int size1, T default_value = default(T)) {
      if (array == null || array.Length != size0 * size1) {
        array = new T[size0, size1];
        for (var i = 0; i != size0; i++) {
          for (var j = 0; j != size1; j++) {
            array[i, j] = default_value;
          }
        }
      }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="material"></param>
    /// <param name="shader"></param>
    public static void EnsureMaterial(ref Material material, Shader shader) {
      if (shader != null) {
        if (material == null || material.shader != shader) {
          material = new Material(shader);
        }

        if (material != null) {
          material.hideFlags = HideFlags.DontSave;
        }
      } else {
        Debug.LogWarning("missing shader", material);
      }
    }

    public static void EnsureDepthTexture(Camera camera) {
      if ((camera.depthTextureMode & DepthTextureMode.Depth) == 0) {
        camera.depthTextureMode |= DepthTextureMode.Depth;
      }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="material"></param>
    /// <param name="name"></param>
    /// <param name="enabled"></param>
    public static void EnsureKeyword(Material material, string name, bool enabled) {
      if (enabled != material.IsKeywordEnabled(name)) {
        if (enabled) {
          material.EnableKeyword(name);
        } else {
          material.DisableKeyword(name);
        }
      }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="rt"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="format"></param>
    /// <param name="filter_mode"></param>
    /// <param name="depth_bits"></param>
    /// <param name="anti_aliasing"></param>
    /// <returns></returns>
    public static bool EnsureRenderTarget(ref RenderTexture rt,
                                          int width,
                                          int height,
                                          RenderTextureFormat format,
                                          FilterMode filter_mode,
                                          int depth_bits = 0,
                                          int anti_aliasing = 1) {
      if (rt != null
          && (rt.width != width
              || rt.height != height
              || rt.format != format
              || rt.filterMode != filter_mode
              || rt.antiAliasing != anti_aliasing)) {
        RenderTexture.ReleaseTemporary(rt);
        rt = null;
      }

      if (rt == null) {
        rt = RenderTexture.GetTemporary(width,
                                        height,
                                        depth_bits,
                                        format,
                                        RenderTextureReadWrite.Default,
                                        anti_aliasing);
        rt.filterMode = filter_mode;
        rt.wrapMode = TextureWrapMode.Clamp;
        return true; // new target
      }

      return false; // same target
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="rt"></param>
    public static void ReleaseRenderTarget(ref RenderTexture rt) {
      if (rt != null) {
        RenderTexture.ReleaseTemporary(rt);
        rt = null;
      }
    }

    /// <summary>
    ///
    /// </summary>
    public static void DrawFullscreenQuad() {
      GL.PushMatrix();
      GL.LoadOrtho();
      GL.Begin(GL.QUADS);

      GL.MultiTexCoord2(0, 0.0f, 0.0f);
      GL.Vertex3(0.0f, 0.0f, 0.0f); // BL

      GL.MultiTexCoord2(0, 1.0f, 0.0f);
      GL.Vertex3(1.0f, 0.0f, 0.0f); // BR

      GL.MultiTexCoord2(0, 1.0f, 1.0f);
      GL.Vertex3(1.0f, 1.0f, 0.0f); // TR

      GL.MultiTexCoord2(0, 0.0f, 1.0f);
      GL.Vertex3(0.0f, 1.0f, 0.0f); // TL

      GL.End();
      GL.PopMatrix();
    }
  }
}