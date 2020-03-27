using PDTAAFork.Scripts.Classes;
using UnityEngine;

namespace PDTAAFork.Scripts.MonoBehaviours
{
    /// <inheritdoc />
    /// <summary>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("AdaptiveSharpness")]
    [ExecuteInEditMode]
    public class AdaptiveSharpness : MonoBehaviour
    {
        [Tooltip("Amount of Automatic Sharpness added based on relative velocities")]
        [SerializeField]
        [Range(0.0f, 1.5f)]
        float _adaptive_sharpness = 0.6f;

        float _strength = 0.8f;
        float _clamp = 0.005f;

        [SerializeField] [Range(0.0f, 3.0f)] float _strength_min = 0.02f;
        [SerializeField] [Range(0.0f, 3.0f)] float _strength_max = 2.0f;

        [SerializeField] [Range(0.0f, 3.0f)] float _clamp_min = 0.005f;
        [SerializeField] [Range(0.0f, 3.0f)] float _clamp_max = 0.12f;

        Material _material;
        Camera _camera;

        static readonly int _pixel_width = Shader.PropertyToID("_AdaptiveSharpnessPixelWidth");
        static readonly int _pixel_height = Shader.PropertyToID("_AdaptiveSharpnessPixelHeight");
        static readonly int _adaptive_sharpness_strength = Shader.PropertyToID("_AdaptiveSharpnessStrength");
        static readonly int _adaptive_sharpness_clamp = Shader.PropertyToID("_AdaptiveSharpnessMagnitudeClamp");

        void Awake()
        {
            if (!this._material)
            {
                this._material = TaaUtilities.CreateMaterial("PDTAAFork/AdaptiveSharpness");
            }

            if (!this._camera)
            {
                this._camera = this.GetComponent<Camera>();
            }

            this._camera.depthTextureMode |= DepthTextureMode.Depth;
            this._camera.depthTextureMode |= DepthTextureMode.MotionVectors;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (this._adaptive_sharpness > 0)
            {
                this._strength = Mathf.Lerp(this._strength_min, this._strength_max, this._adaptive_sharpness);
                this._clamp = Mathf.Lerp(this._clamp_min, this._clamp_max, this._adaptive_sharpness);

                this._material.SetFloat(_pixel_width, 1.0f / Screen.width);
                this._material.SetFloat(_pixel_height, 1.0f / Screen.height);
                this._material.SetFloat(_adaptive_sharpness_strength, this._strength);
                this._material.SetFloat(_adaptive_sharpness_clamp, this._clamp);
                Graphics.Blit(source,
                    destination,
                    this._material,
                    0);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }
    }
}