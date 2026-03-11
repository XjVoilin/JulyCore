using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UISmartButton : MonoBehaviour,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("Interact")] [SerializeField] private bool interactable = true;

    [Header("Scale Feedback")] public bool enableScale = true;
    public float scaleTarget = 0.9f;
    public float scaleTime = 0.1f;
    public Ease scaleEase = Ease.Linear;

    [Header("Click Cooldown")] public bool enableCooldown = true;
    public float cooldownTime = 0.5f;

    [Header("Event")] public UnityEvent onClick = new();

    private Vector3 _originScale;
    private Vector3 _pressedScale;
    private float _lastClickTime;

    private void Awake()
    {
        _originScale = transform.localScale;
        _pressedScale = _originScale * scaleTarget;
        _targetScale = _originScale;
    }

    private void OnDestroy()
    {
        _scaleTweener?.Kill();
    }

    #region Pointer

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!interactable || !enableScale)
            return;

        // 按下时直接设置缩放（立即响应）
        _scaleTweener?.Kill();
        transform.localScale = _pressedScale;
        _targetScale = _pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!interactable || !enableScale)
            return;

        // 抬起时用动画恢复
        PlayScaleRestore();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!interactable || !enableScale)
            return;

        // 离开时用动画恢复
        PlayScaleRestore();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable)
            return;

        if (!TryProcessClick())
            return;

        onClick?.Invoke();
    }

    #endregion

    #region Public API

    public void SetInteractable(bool value)
    {
        if (interactable == value)
            return;

        interactable = value;

        if (!interactable)
        {
            _scaleTweener?.Kill();
            transform.localScale = _originScale;
        }
    }

    #endregion

    #region Internal

    private Tweener _scaleTweener;
    private Vector3 _targetScale;

    private void PlayScaleRestore()
    {
        // 如果已经是原始大小，跳过
        if (_targetScale == _originScale)
            return;

        _targetScale = _originScale;

        // Kill 当前动画
        _scaleTweener?.Kill();

        // 从当前位置平滑恢复到原始大小
        _scaleTweener = transform.DOScale(_originScale, scaleTime)
            .SetEase(scaleEase)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private bool TryProcessClick()
    {
        if (!enableCooldown)
            return true;

        var now = Time.unscaledTime;
        if (now - _lastClickTime < cooldownTime)
            return false;

        _lastClickTime = now;
        return true;
    }

    #endregion
}