// Copyright (c) <2015> <Playdead>
// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE.TXT)
// AUTHOR: Lasse Jon Fuglsang Pedersen <lasse@playdead.com>

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PDTAAFork.Scripts.MonoBehaviours {
  /// <inheritdoc />
  /// <summary>
  /// </summary>
  [AddComponentMenu("Playdead/VelocityBufferTag")]
  public class VelocityBufferTag : MonoBehaviour {
    #if UNITY_5_6_OR_NEWER
    static List<Vector3> _temporary_vertex_storage = new List<Vector3>(512);
    #endif
    public static List<VelocityBufferTag> _ActiveObjects = new List<VelocityBufferTag>(128);

    Transform _transform;

    [NonSerialized, HideInInspector] public SkinnedMeshRenderer _MeshSmr;
    [NonSerialized, HideInInspector] public bool _MeshSmrActive;
    [NonSerialized, HideInInspector] public Mesh _Mesh;
    [NonSerialized, HideInInspector] public Matrix4x4 _LocalToWorldPrev;
    [NonSerialized, HideInInspector] public Matrix4x4 _LocalToWorldCurr;

    const int _frames_not_rendered_sleep_threshold = 60;
    int _frames_not_rendered = _frames_not_rendered_sleep_threshold;
    public bool Rendering { get { return this._frames_not_rendered < _frames_not_rendered_sleep_threshold; } }

    void Reset() {
      this._transform = this.transform;

      var smr = this.GetComponent<SkinnedMeshRenderer>();
      if (smr != null) {
        if (this._Mesh == null || this._MeshSmrActive == false) {
          this._Mesh = new Mesh {hideFlags = HideFlags.HideAndDontSave};
        }

        this._MeshSmrActive = true;
        this._MeshSmr = smr;
      } else {
        var mf = this.GetComponent<MeshFilter>();
        if (mf != null) {
          this._Mesh = mf.sharedMesh;
        } else {
          this._Mesh = null;
        }

        this._MeshSmrActive = false;
        this._MeshSmr = null;
      }

      // force restart
      this._frames_not_rendered = _frames_not_rendered_sleep_threshold;
    }

    void Awake() { this.Reset(); }

    void TagUpdate(bool restart) {
      if (this._MeshSmrActive && this._MeshSmr == null) {
        this.Reset();
      }

      if (this._MeshSmrActive) {
        if (restart) {
          this._MeshSmr.BakeMesh(this._Mesh);
          #if UNITY_5_6_OR_NEWER
          this._Mesh.GetVertices(_temporary_vertex_storage);
          this._Mesh.SetNormals(_temporary_vertex_storage);
          #else
            mesh.normals = mesh.vertices;// garbage ahoy
          #endif
        } else {
          #if UNITY_5_6_OR_NEWER
          this._Mesh.GetVertices(_temporary_vertex_storage);
          this._MeshSmr.BakeMesh(this._Mesh);
          this._Mesh.SetNormals(_temporary_vertex_storage);
          #else
            Vector3[] vs = mesh.vertices;// garbage ahoy
            meshSmr.BakeMesh(mesh);
            mesh.normals = vs;
          #endif
        }
      }

      if (restart) {
        this._LocalToWorldCurr = this._transform.localToWorldMatrix;
        this._LocalToWorldPrev = this._LocalToWorldCurr;
      } else {
        this._LocalToWorldPrev = this._LocalToWorldCurr;
        this._LocalToWorldCurr = this._transform.localToWorldMatrix;
      }
    }

    void LateUpdate() {
      if (this._frames_not_rendered < _frames_not_rendered_sleep_threshold) {
        this._frames_not_rendered++;
        this.TagUpdate(restart : false);
      }
    }

    void OnWillRenderObject() {
      if (Camera.current != Camera.main) {
        return; // ignore anything but main cam
      }

      if (this._frames_not_rendered >= _frames_not_rendered_sleep_threshold) {
        this.TagUpdate(restart : true);
      }

      this._frames_not_rendered = 0;
    }

    void OnEnable() { _ActiveObjects.Add(this); }

    void OnDisable() {
      _ActiveObjects.Remove(this);

      // force restart
      this._frames_not_rendered = _frames_not_rendered_sleep_threshold;
    }
  }
}