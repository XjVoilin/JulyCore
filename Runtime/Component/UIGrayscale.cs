using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIGrayscale : MonoBehaviour
{
    [SerializeField] private bool _gray;

    [Tooltip("灰度强度 0-1")]
    [Range(0f, 1f)]
    [SerializeField] private float _grayIntensity = 1f;

    [Tooltip("亮度系数（置灰后的亮度，1=不变，0.5=减半）")]
    [Range(0.3f, 1f)]
    [SerializeField] private float _brightness = 0.6f;

    private Graphic[] _graphics;
    private Color[] _originColors;
    private bool _initialized;

    private void Awake()
    {
        CacheGraphics();
        if (_gray) ApplyGray();
    }

    public void SetGray(bool value)
    {
        if (_gray == value && _initialized) return;
        _gray = value;

        if (_gray) ApplyGray();
        else RestoreColors();
    }

    public void Refresh()
    {
        CacheGraphics();
        if (_gray) ApplyGray();
    }

    private void CacheGraphics()
    {
        _graphics = GetComponentsInChildren<Graphic>(true);
        _originColors = new Color[_graphics.Length];

        for (int i = 0; i < _graphics.Length; i++)
        {
            if (_graphics[i] != null)
                _originColors[i] = _graphics[i].color;
        }

        _initialized = true;
    }

    private void ApplyGray()
    {
        if (!_initialized) return;

        for (int i = 0; i < _graphics.Length; i++)
        {
            var graphic = _graphics[i];
            if (graphic == null) continue;

            var grayColor = ToGray(_originColors[i]);
            
            graphic.CrossFadeColor(grayColor, 0, true, true);
        }
    }

    private void RestoreColors()
    {
        if (!_initialized) return;

        for (int i = 0; i < _graphics.Length; i++)
        {
            var graphic = _graphics[i];
            if (graphic == null) continue;

            graphic.CrossFadeColor(_originColors[i],0, true, true);
        }
    }

    private Color ToGray(Color c)
    {
        var luminance = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        var gray = luminance * _brightness;
        
        return Color.Lerp(c, new Color(gray, gray, gray, c.a), _grayIntensity);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (!_initialized) return;

        if (_gray) ApplyGray();
        else RestoreColors();
    }
#endif
}
