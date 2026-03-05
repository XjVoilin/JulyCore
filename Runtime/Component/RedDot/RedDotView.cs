using JulyCore.Core.Events;
using TMPro;
using UnityEngine;

namespace JulyCore.Component.RedDot
{
    /// <summary>
    /// 红点锚点位置
    /// </summary>
    public enum RedDotAnchor
    {
        TopRight,     // 右上角（默认）
        TopLeft,      // 左上角
        BottomRight,  // 右下角
        BottomLeft,   // 左下角
        Center        // 中心
    }

    /// <summary>
    /// 红点视图组件
    /// 挂载到 UI 元素上，自动监听红点变化并显示
    /// </summary>
    public class RedDotView : MonoBehaviour
    {
        [Header("红点配置")]
        [SerializeField] private string _key;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private bool _showNumber = true;

        [Header("位置设置")]
        [SerializeField] private RedDotAnchor _anchor = RedDotAnchor.TopRight;
        [SerializeField] private Vector2 _offset = Vector2.zero;

        private GameObject _instance;
        private TMP_Text _tmpText;
        private UnityEngine.UI.Text _uguiText;
        private int _cachedCount = -1;

        /// <summary>
        /// 当前是否显示红点
        /// </summary>
        public bool IsVisible => _instance != null && _instance.activeSelf;

        /// <summary>
        /// 监听的红点 Key
        /// </summary>
        public string Key => _key;

        #region Unity 生命周期

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_key)) return;
            
            GF.RedDot.OnChanged(OnRedDotChanged, this);
            GF.RedDot.OnEnabledChanged(OnEnabledChanged, this);
            Refresh();
        }

        private void OnDisable()
        {
            GF.RedDot.OffChanged(OnRedDotChanged);
            GF.RedDot.OffEnabledChanged(OnEnabledChanged);
        }

        private void OnDestroy()
        {
            DestroyInstance();
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 设置监听的 Key
        /// </summary>
        public void SetKey(string key)
        {
            if (_key == key) return;
            
            _key = key;
            _cachedCount = -1;
            
            if (isActiveAndEnabled)
            {
                Refresh();
            }
        }

        /// <summary>
        /// 手动刷新显示
        /// </summary>
        public void Refresh()
        {
            if (string.IsNullOrEmpty(_key)) return;
            
            var count = GF.RedDot.GetCount(_key);
            UpdateDisplay(count);
        }

        #endregion

        #region 私有方法

        private void OnRedDotChanged(RedDotChangedEvent evt)
        {
            if (evt.Key == _key)
            {
                // 使用 GetCount 而不是 evt.NewCount，确保检查 IsEnabled 状态
                Refresh();
            }
        }

        private void OnEnabledChanged(RedDotEnabledChangedEvent evt)
        {
            // 全局变更或当前节点/父节点变更时刷新
            if (evt.IsGlobal || IsAffectedByKey(evt.Key))
            {
                Refresh();
            }
        }

        private bool IsAffectedByKey(string changedKey)
        {
            if (changedKey == _key) return true;
            
            // 检查是否是父节点变更
            var node = GF.RedDot.GetNode(_key);
            if (node == null) return false;
            
            var parentKey = node.ParentKey;
            while (!string.IsNullOrEmpty(parentKey))
            {
                if (parentKey == changedKey) return true;
                var parentNode = GF.RedDot.GetNode(parentKey);
                if (parentNode == null) break;
                parentKey = parentNode.ParentKey;
            }
            
            return false;
        }

        private void UpdateDisplay(int count)
        {
            if (count > 0)
            {
                ShowRedDot(count);
            }
            else
            {
                HideRedDot();
            }
        }

        private void ShowRedDot(int count)
        {
            // 首次显示时创建实例
            if (_instance == null && _prefab != null)
            {
                _instance = Instantiate(_prefab, transform);
                ApplyPosition();
                CacheTextComponents();
            }

            if (_instance == null) return;

            _instance.SetActive(true);

            // 数量变化时更新文本
            if (_showNumber && count != _cachedCount)
            {
                _cachedCount = count;
                var text = count > 99 ? "99+" : count.ToString();
                if (_tmpText != null) _tmpText.text = text;
                else if (_uguiText != null) _uguiText.text = text;
            }
        }

        private void ApplyPosition()
        {
            if (_instance == null) return;
            
            var rectTransform = _instance.GetComponent<RectTransform>();
            if (rectTransform == null) return;

            // 根据锚点设置 anchor 和 pivot
            var anchorPos = _anchor switch
            {
                RedDotAnchor.TopRight => new Vector2(1, 1),
                RedDotAnchor.TopLeft => new Vector2(0, 1),
                RedDotAnchor.BottomRight => new Vector2(1, 0),
                RedDotAnchor.BottomLeft => new Vector2(0, 0),
                _ => new Vector2(0.5f,0.5f)
            };

            rectTransform.anchorMin = anchorPos;
            rectTransform.anchorMax = anchorPos;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = _offset;
        }

        private void HideRedDot()
        {
            if (_instance != null)
            {
                _instance.SetActive(false);
            }
            _cachedCount = -1;
        }

        private void DestroyInstance()
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
            }
            _tmpText = null;
            _uguiText = null;
            _cachedCount = -1;
        }

        private void CacheTextComponents()
        {
            if (_instance == null) return;
            _tmpText = _instance.GetComponentInChildren<TMP_Text>();
            _uguiText = _instance.GetComponentInChildren<UnityEngine.UI.Text>();
        }

        #endregion
    }
}
