using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JulyCore
{
    /// <summary>
    /// Tip 项组件
    /// 挂载在 Tip 预制体上
    /// </summary>
    public class TipItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _rectTransform;

        private Action<TipItem> _onComplete;
        private Tween _fadeTween;
        private Tweener _moveTweener;

        private void Awake()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_text == null)
                _text = GetComponentInChildren<TextMeshProUGUI>();
        }

        /// <summary>
        /// 显示 Tip
        /// </summary>
        /// <param name="message">提示内容</param>
        /// <param name="duration">显示时长</param>
        /// <param name="fadeOutDuration">淡出时长</param>
        /// <param name="onComplete">完成回调</param>
        /// <param name="enterOffset">入场偏移量（从下方滑入的距离）</param>
        /// <param name="enterDuration">入场动画时长</param>
        public void Show(string message, float duration, float fadeOutDuration, Action<TipItem> onComplete, 
            float enterOffset = 0f, float enterDuration = 0.2f)
        {
            _onComplete = onComplete;

            // 设置文本
            if (_text != null)
            {
                _text.text = message;
            }

            // 重置状态
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            // 确保显示
            gameObject.SetActive(true);

            // 强制刷新布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);

            KillTweens();

            // 入场动画：从下方滑入
            if (_rectTransform != null && enterOffset > 0f)
            {
                _rectTransform.anchoredPosition = new Vector2(0, -enterOffset);
                _moveTweener = _rectTransform.DOAnchorPosY(0f, enterDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
            }
            else if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = Vector2.zero;
            }

            // 延迟后开始淡出
            _fadeTween = DOVirtual.DelayedCall(duration, () =>
            {
                FadeOut(fadeOutDuration);
            }).SetLink(gameObject);
        }

        private void FadeOut(float duration)
        {
            if (_canvasGroup == null)
            {
                Complete();
                return;
            }

            _fadeTween = _canvasGroup.DOFade(0f, duration)
                .SetEase(Ease.OutQuad)
                .OnComplete(Complete)
                .SetLink(gameObject);
        }

        private void Complete()
        {
            _onComplete?.Invoke(this);
            _onComplete = null;
        }

        /// <summary>
        /// 向上移动
        /// </summary>
        /// <param name="offset">移动距离</param>
        /// <param name="duration">动画时长</param>
        public void MoveUp(float offset, float duration)
        {
            if (_rectTransform == null)
            {
                return;
            }

            _moveTweener?.Kill();
            var targetY = _rectTransform.anchoredPosition.y + offset;
            _moveTweener = _rectTransform.DOAnchorPosY(targetY, duration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 获取 Tip 高度
        /// </summary>
        public float GetHeight()
        {
            if (_rectTransform != null)
            {
                return _rectTransform.rect.height;
            }

            return 40f; // 默认高度
        }

        /// <summary>
        /// 重置状态
        /// </summary>
        public void Reset()
        {
            KillTweens();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = Vector2.zero;
            }

            if (_text != null)
            {
                _text.text = string.Empty;
            }

            _onComplete = null;
        }

        private void KillTweens()
        {
            _fadeTween?.Kill();
            _moveTweener?.Kill();
            _fadeTween = null;
            _moveTweener = null;
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
