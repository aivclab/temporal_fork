//#define USE_PP

#if USE_PP
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PDTAAFork.Scripts.PostProcessingStack {
  /// <inheritdoc />
  /// <summary>
  /// </summary>
  [RequireComponent(typeof(PostProcessVolume))]
  public class PpStackFrustumJitter : MonoBehaviour {
    TemporalReprojectionEffect _pitch_test_settings;

    void OnEnable() {
      var volume = this.GetComponent<PostProcessVolume>();
      if (volume) {
        if (volume.profile == null) {
          this.enabled = false;
          Debug.Log("Cant load PostProcess volume");
          return;
        }

        var found_effect_settings = volume.profile.TryGetSettings(out this._pitch_test_settings);
        if (!found_effect_settings) {
          this.enabled = false;
          Debug.Log("Cant load PitchTest settings");
          return;
        }

        Debug.Log("Got value: " + this._pitch_test_settings.flip_x.GetValue<float>()); //get current value
        Debug.Log("Setting value to 5");
        this._pitch_test_settings.flip_x.value = 5; //<- modify the value on the img
      }
    }
  }
}
#endif
