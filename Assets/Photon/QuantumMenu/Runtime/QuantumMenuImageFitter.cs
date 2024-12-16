namespace Quantum.Menu {
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// To support any sprite aspect ratio in landscape and portrait mode this script calculates new image sizes
  /// aligned to the top/bottom or left/right without changing the aspect ratio.
  /// Any changes in the resolution of the parent rect (Editor resize, screen orientation, etc) will result in a recalculation.
  /// </summary>
  [RequireComponent(typeof(Image))]
  [RequireComponent(typeof(RectTransform))]
  public class QuantumMenuImageFitter : MonoBehaviour {
    private Image _image;
    private RectTransform _parentTransform;
    private RectTransform _rectTransform;
    private Vector2 _resolution;

    /// <summary>
    /// Unity Awake method, cache transforms and image.
    /// </summary>
    public void Awake() {
      _image = GetComponent<Image>();
      _parentTransform = transform.parent.GetComponent<RectTransform>();
      _rectTransform = transform.GetComponent<RectTransform>();
    }

    /// <summary>
    /// A Unity message that will recalculate the aspect ratio when the resolution changes.
    /// </summary>
    public void OnResolutionChanged() {
      CalculateAspect();
    }

    /// <summary>
    /// Unity Start method.
    /// </summary>
    public void Start() {
      CalculateAspect();
    }

    /// <summary>
    /// Unity Update method.
    /// </summary>
    public void Update() {
      if (_resolution.x != _parentTransform.rect.width ||
          _resolution.y != _parentTransform.rect.height) {
        _resolution.x = _parentTransform.rect.width;
        _resolution.y = _parentTransform.rect.height;
        CalculateAspect();
      }
    }

    private void CalculateAspect() {
      if (_image.sprite == null) {
        return;
      }

      var parentAspect = _parentTransform.rect.width / _parentTransform.rect.height;
      var spriteAspect = _image.sprite.rect.width / _image.sprite.rect.height;

      if (spriteAspect >= parentAspect) {
        var a = _parentTransform.rect.height / _image.sprite.rect.height;
        var w = a * _image.sprite.rect.width;
        _rectTransform.sizeDelta = new Vector2(w, _parentTransform.rect.height);
      } else {
        var a = _parentTransform.rect.width / _image.sprite.rect.width;
        var h = a * _image.sprite.rect.height;
        _rectTransform.sizeDelta = new Vector2(_parentTransform.rect.width, h);
      }
    }
  }
}
