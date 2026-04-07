using JulyCore;
using JulyCore.Core.Events;
using JulyCore.Data.RedDot;
using TMPro;
using UnityEngine;

/// <summary>
/// 红点组件（Prefab 自包含）。
/// 使用方式：将红点 Prefab 拖到目标节点下 → Inspector 选 Key → 完成。
/// Prefab 内含三种视觉子节点，引用在 Prefab 内部闭环，做一次拖好。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIRedDot : MonoBehaviour
{
    private const string NumberOverflow = "99+";

    [Header("红点配置")]
    [SerializeField] private string _key;

    [Header("视觉根（Prefab 内拖好，使用时无需修改）")]
    [SerializeField] private GameObject _visualNormal;
    [SerializeField] private GameObject _visualNumber;
    [SerializeField] private GameObject _visualNew;

    [Header("文案")]
    [SerializeField] private TMP_Text _numberText;

    private int _cachedCount = -1;
    private GameObject _activeVisual;

    /// <summary>当前是否显示红点。</summary>
    public bool IsVisible => _activeVisual != null && _activeVisual.activeSelf;

    /// <summary>监听的红点 Key。</summary>
    public string Key => _key;

    #region Unity 生命周期

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(_key)) return;

        GF.RedDot.OnKeyChanged(_key, OnRedDotChanged, this);
        Refresh();
    }

    private void OnDisable()
    {
        GF.Event.UnsubscribeAll(this);
    }

    #endregion

    #region 公开方法

    /// <summary>运行时切换监听 Key。传空或 null 会隐藏红点。</summary>
    public void SetKey(string key)
    {
        if (_key == key) return;

        _key = key;

        if (!isActiveAndEnabled) return;

        if (string.IsNullOrEmpty(_key))
        {
            HideAll();
            return;
        }

        Refresh();
    }

    /// <summary>手动刷新显示。</summary>
    public void Refresh()
    {
        if (string.IsNullOrEmpty(_key))
        {
            HideAll();
            return;
        }

        var node = GF.RedDot.GetNode(_key);
        var type = node?.Type ?? RedDotType.Normal;
        var count = GF.RedDot.GetCount(_key);
        Present(type, count);
    }

    #endregion

    #region 私有方法

    private void Present(RedDotType type, int count)
    {
        if (count <= 0)
        {
            HideAll();
            return;
        }

        var visual = type switch
        {
            RedDotType.Normal => _visualNormal,
            RedDotType.Number => _visualNumber,
            RedDotType.New => _visualNew,
            _ => _visualNormal
        };

        if (visual == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[UIRedDot] Key「{_key}」类型 {type}，Prefab 中缺少对应视觉根。", this);
#endif
            HideAll();
            return;
        }

        SetAllVisualsActive(false);
        visual.SetActive(true);
        _activeVisual = visual;

        if (type == RedDotType.Number)
        {
            if (count != _cachedCount)
            {
                _cachedCount = count;
                if (_numberText != null)
                    _numberText.text = count > 99 ? NumberOverflow : count.ToString();
            }
        }
        else
        {
            _cachedCount = -1;
        }
    }

    private void HideAll()
    {
        SetAllVisualsActive(false);
        _activeVisual = null;
        _cachedCount = -1;
    }

    private void SetAllVisualsActive(bool active)
    {
        if (_visualNormal != null) _visualNormal.SetActive(active);
        if (_visualNumber != null) _visualNumber.SetActive(active);
        if (_visualNew != null) _visualNew.SetActive(active);
    }

    private void OnRedDotChanged(RedDotChangedEvent evt)
    {
        Refresh();
    }

    #endregion
}
