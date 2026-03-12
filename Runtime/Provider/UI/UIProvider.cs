using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Core.Config;
using JulyCore.Data.UI;
using JulyCore.Provider.Base;
using JulyCore.Provider.Pool;
using JulyCore.Provider.Resource;
using JulyCore.Provider.UI.Animation;
using UnityEngine;
using UnityEngine.UI;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// UI信息类（技术层数据结构）
    /// </summary>
    public class UIInfo
    {
        public UIBase UI { get; set; }

        public GameObject GameObject => UI?.gameObject;

        public WindowIdentifier WindowIdentifier { get; set; }

        public Type UIType { get; set; }

        public UILayer Layer { get; set; }
        public object Param { get; set; }

        public CanvasGroup CanvasGroup { get; set; }

        public UIAnimationType CloseAnimationType { get; set; } = UIAnimationType.None;

        public bool IsValid => UI != null && UI.IsOpened;

        public void Visible(bool isShow)
        {
            if (CanvasGroup)
            {
                CanvasGroup.alpha = isShow ? 1 : 0f;
                CanvasGroup.interactable = isShow;
                CanvasGroup.blocksRaycasts = isShow;
            }

            GameObject.SetActive(isShow);
        }

        public void SetInteractable(bool interactable)
        {
            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = interactable;
                CanvasGroup.blocksRaycasts = interactable;
            }
        }
    }

    /// <summary>
    /// Unity UI提供者实现
    /// 纯技术执行层：负责资源加载、UI实例化、GameObject操作
    /// 不包含任何业务语义，不维护业务状态
    /// </summary>
    internal class UIProvider : ProviderBase, IUIProvider
    {
        public override int Priority => Frameworkconst.PriorityUIProvider;
        protected override LogChannel LogChannel => LogChannel.UI;
        public Camera UICamera => _uiCamera;

        #region 常量

        private const string UIRootName = "UIRoot";
        private const string UICameraName = "UICamera";
        private const string LayerNamePrefix = "Layer_";
        private const string UIMaskName = "UIMask";

        #endregion

        #region 私有字段

        private readonly IResourceProvider _resourceProvider;
        private readonly IPoolProvider _poolProvider;
        private IUIResourcePathResolver _pathResolver;
        private Transform _uiRoot;
        private Camera _uiCamera;

        // 技术层数据存储
        private readonly Dictionary<UILayer, Transform> _layerRoots = new Dictionary<UILayer, Transform>();
        private readonly Dictionary<UILayer, Transform> _contentRoots = new Dictionary<UILayer, Transform>();
        private readonly Dictionary<WindowIdentifier, UIInfo> _uiInfos = new Dictionary<WindowIdentifier, UIInfo>();
        private readonly Dictionary<int, WindowIdentifier> _idToIdentifier = new Dictionary<int, WindowIdentifier>();
        private readonly Dictionary<Type, GameObject> _preloadedPrefabs = new Dictionary<Type, GameObject>();

        // 遮罩管理（全局单遮罩）
        private readonly List<MaskRequest> _maskRequests = new List<MaskRequest>();
        private GameObject _activeMask;

        // Tip 管理
        private TipManager _tipManager;

        // UI 配置
        private UIConfig _uiConfig;

        #endregion

        /// <summary>
        /// 构造函数（依赖通过 DI 容器注入）
        /// </summary>
        public UIProvider(IResourceProvider resourceProvider,IPoolProvider  poolProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _poolProvider =  poolProvider ?? throw new ArgumentNullException(nameof(poolProvider));
        }

        protected override async UniTask OnInitAsync()
        {
            // 读取 UI 配置
            _uiConfig = FrameworkContext.Instance?.FrameworkConfig?.UIConfig;

            // 初始化资源路径解析器（默认使用约定路径）
            _pathResolver = new DefaultUIResourcePathResolver();

            // 创建或获取UIRoot
            CreateUIRoot();

            // 初始化 Tip 管理器
            await InitTipManager();
        }

        private async UniTask InitTipManager()
        {
            var tipConfig = FrameworkContext.Instance?.FrameworkConfig?.TipConfig;
            if (tipConfig == null)
            {
                Log($"[{Name}] TipConfig 未配置，Tip 功能不可用");
                return;
            }

            _tipManager = new TipManager(tipConfig, _uiConfig, _resourceProvider, _poolProvider, _uiRoot, this);
            await _tipManager.InitAsync();
        }

        public async UniTask<UIBase> OpenAsync(UIOpenOptions options, CancellationToken cancellationToken = default)
        {
            ValidateOpenOptions(options);

            var windowIdentifier = options.WindowIdentifier;
            var uiType = ResolveUIType(windowIdentifier.WindowName);

            if (TryGetUIInfo(windowIdentifier, out var existingInfo))
            {
                await CloseInternalAsync(existingInfo, UIAnimationType.None, cancellationToken);
            }

            var prefab = await LoadWindowPrefab(uiType, cancellationToken);
            return await CreateUIWindow(options, uiType, prefab, cancellationToken);
        }

        /// <summary>
        /// 加载UI预制体（优先使用预加载的）
        /// </summary>
        /// <param name="uiType">类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预制体GameObject</returns>
        private async UniTask<GameObject> LoadWindowPrefab(Type uiType, CancellationToken cancellationToken = default)
        {
            // 优先使用预加载的预制体
            if (_preloadedPrefabs.TryGetValue(uiType, out var preloadedPrefab) && preloadedPrefab != null)
            {
                return preloadedPrefab;
            }

            // 从资源提供者加载
            var resourcePath = _pathResolver.GetResourcePath(uiType);
            GameObject prefab;
            
            try
            {
                prefab = await _resourceProvider.LoadAsync<GameObject>(resourcePath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogWarning($"[{Name}] 加载UI {uiType.Name} 被取消");
                throw;
            }
            catch (Exception ex)
            {
                var msg = $"[{Name}] 加载UI {uiType.Name} 失败: {ex.Message}";
                LogError(msg);
                throw new JulyException(msg, ex);
            }

            if (prefab == null)
            {
                var msg = $"[{Name}] UI预制体未找到: {resourcePath} (类型: {uiType.Name})";
                LogError(msg);
                throw new JulyException(msg);
            }

            return prefab;
        }

        /// <summary>
        /// 创建window
        /// </summary>
        /// <param name="options">参数</param>
        /// <param name="uiType">窗口对应脚本的类型</param>
        /// <param name="prefab">窗口的prefab</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        private async UniTask<UIBase> CreateUIWindow(UIOpenOptions options, Type uiType, GameObject prefab,
            CancellationToken cancellationToken = default)
        {
            var windowIdentifier = options.WindowIdentifier;
            var contentRoot = GetOrCreateContentRoot(options.Layer);
            UIBase component = null;
            try
            {
                var instance = UnityEngine.Object.Instantiate(prefab, contentRoot);
                instance.transform.SetParent(contentRoot, false);
                instance.name = windowIdentifier.WindowName;
                component = instance.GetComponent(uiType) as UIBase;

                if (component == null)
                {
                    var msg = $"[{Name}] UI {uiType.Name} 预制体上未找到组件 {uiType.Name}";
                    LogError(msg);
                    UnityEngine.Object.Destroy(instance);
                    throw new JulyException(msg);
                }

                var uiInfo = InitializeUIInfo(component);
                uiInfo.Visible(true);

                SetupUIInfo(uiInfo, options, uiType, windowIdentifier);
                RegisterUI(windowIdentifier, uiInfo);

                if (options.ShowMask)
                {
                    RequestMask(windowIdentifier, options.Layer, options.MaskColor, options.ClickMaskToClose);
                }

                if (options.Data != null)
                {
                    TrySetUIParam(component, options.Data);
                }

                SafeCallOnBeforeOpen(component);
                await TryPlayOpenAnimationAsync(uiInfo, options.OpenAnimationType, cancellationToken);

                component.Open();
                return component;
            }
            catch (Exception)
            {
                ReleaseMask(windowIdentifier);
                if (component != null)
                    UnityEngine.Object.Destroy(component.gameObject);
                throw;
            }
        }

        /// <summary>
        /// 关闭并销毁UI（通过WindowIdentifier，同步版本）
        /// </summary>
        public void Close(WindowIdentifier identifier, bool destroy = true)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            CloseInternal(identifier, GetCloseAnimationType(identifier));
        }

        /// <summary>
        /// 关闭并销毁UI（通过WindowIdentifier，异步版本，等待动画完成）
        /// </summary>
        public async UniTask CloseAsync(WindowIdentifier identifier, bool destroy = true,
            CancellationToken cancellationToken = default)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            await CloseInternalAsync(identifier, GetCloseAnimationType(identifier), cancellationToken);
        }

        private void CloseInternal(WindowIdentifier identifier, UIAnimationType animationType)
        {
            CloseInternalAsync(identifier, animationType, CancellationToken.None).Forget();
        }

        private async UniTask CloseInternalAsync(WindowIdentifier identifier,
            UIAnimationType animationType,
            CancellationToken cancellationToken)
        {
            if (!TryGetUIInfo(identifier, out var uiInfo))
            {
                LogWarning($"[{Name}] 尝试关闭未打开的UI: {identifier}");
                return;
            }

            await CloseInternalAsync(uiInfo, animationType, cancellationToken);
        }

        private async UniTask CloseInternalAsync(UIInfo uiInfo,
            UIAnimationType animationType,
            CancellationToken cancellationToken)
        {
            var identifier = uiInfo.WindowIdentifier;

            if (!uiInfo.IsValid)
            {
                LogWarning($"[{Name}] UI {identifier} 已无效（可能已被销毁），直接清理记录");
                CloseInternalCore(identifier);
                return;
            }

            SafeCallOnClose(uiInfo.UI);
            await TryPlayCloseAnimationAsync(uiInfo, animationType, cancellationToken);
            SafeCallOnAfterClose(uiInfo.UI);
            CloseInternalCore(identifier);
        }

        private void CloseInternalCore(WindowIdentifier identifier)
        {
            if (!TryGetUIInfo(identifier, out var uiInfo))
            {
                return;
            }

            ReleaseMask(identifier);
            DestroyUI(identifier, uiInfo.UI);
        }

        private void DestroyUI(WindowIdentifier identifier, UIBase ui)
        {
            _uiInfos.Remove(identifier);

            if (_idToIdentifier.TryGetValue(identifier.ID, out var existingIdentifier) &&
                existingIdentifier == identifier)
            {
                _idToIdentifier.Remove(identifier.ID);
            }

            if (ui != null)
            {
                UnityEngine.Object.Destroy(ui.gameObject);
            }
        }


        public bool TryGet(WindowIdentifier identifier, out UIBase ui)
        {
            ui = null;
            if (identifier == null || !TryGetUIInfo(identifier, out var uiInfo))
            {
                return false;
            }

            ui = uiInfo.UI;
            return ui != null;
        }

        public bool IsOpen(WindowIdentifier identifier)
        {
            return identifier != null && 
                   TryGetUIInfo(identifier, out var uiInfo) && 
                   uiInfo.IsValid;
        }

        public bool IsPreloaded(Type uiType)
        {
            if (uiType == null)
            {
                throw new ArgumentNullException(nameof(uiType));
            }

            return _preloadedPrefabs.TryGetValue(uiType, out var prefab) && prefab != null;
        }

        public async UniTask<bool> PreloadAsync(Type uiType, CancellationToken cancellationToken = default)
        {
            if (uiType == null)
            {
                throw new ArgumentNullException(nameof(uiType));
            }

            if (!typeof(UIBase).IsAssignableFrom(uiType))
            {
                throw new ArgumentException($"类型 {uiType.Name} 必须继承自UIBase", nameof(uiType));
            }

            // 如果已预加载，直接返回
            if (_preloadedPrefabs.ContainsKey(uiType))
            {
                return true;
            }

            try
            {
                var resourcePath = _pathResolver.GetResourcePath(uiType);
                var prefab = await _resourceProvider.LoadAsync<GameObject>(resourcePath, cancellationToken);
                if (prefab != null)
                {
                    _preloadedPrefabs[uiType] = prefab;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"[{Name}] 预加载UI {uiType.Name} 失败: {ex.Message}");
            }

            return false;
        }

        public async UniTask PreloadBatchAsync(Type[] uiTypes, CancellationToken cancellationToken = default)
        {
            if (uiTypes == null || uiTypes.Length == 0)
            {
                return;
            }

            var tasks = new List<UniTask<bool>>();
            foreach (var uiType in uiTypes)
            {
                // 包装每个任务，捕获异常，避免单个任务失败影响其他任务
                tasks.Add(SafePreloadAsync(uiType, cancellationToken));
            }

            // 等待所有任务完成并获取结果（即使有失败的任务也会继续执行）
            var results = await UniTask.WhenAll(tasks);

            // 统计成功和失败的数量
            var successCount = results.Count(r => r);
            var failCount = results.Length - successCount;

            if (failCount > 0)
            {
                LogWarning($"[{Name}] 批量预加载完成，成功: {successCount}, 失败: {failCount}, 总计: {uiTypes.Length}");
            }
        }

        /// <summary>
        /// 安全的预加载方法（包装PreloadAsync，捕获所有异常）
        /// </summary>
        private async UniTask<bool> SafePreloadAsync(Type uiType, CancellationToken cancellationToken)
        {
            try
            {
                return await PreloadAsync(uiType, cancellationToken);
            }
            catch (Exception ex)
            {
                // 捕获所有异常（包括参数验证异常），记录日志但不抛出
                var typeName = uiType?.Name ?? "Unknown";
                LogError($"[{Name}] 预加载UI {typeName} 时发生异常: {ex.Message}");
                return false;
            }
        }

        public void ReleasePreload(Type uiType)
        {
            if (uiType == null)
            {
                throw new ArgumentNullException(nameof(uiType));
            }

            if (_preloadedPrefabs.Remove(uiType, out var prefab))
            {
                if (prefab != null)
                {
                    _resourceProvider.Unload(prefab);
                }
            }
        }

        protected override UniTask OnShutdownAsync()
        {
            // 释放所有预加载的资源
            foreach (var prefab in _preloadedPrefabs.Values)
            {
                if (prefab != null)
                {
                    _resourceProvider.Unload(prefab);
                }
            }

            _preloadedPrefabs.Clear();

            // 清理遮罩
            _maskRequests.Clear();
            if (_activeMask != null)
            {
                UnityEngine.Object.Destroy(_activeMask);
                _activeMask = null;
            }

            _uiInfos.Clear();
            _idToIdentifier.Clear();
            _contentRoots.Clear();
            _layerRoots.Clear();
            _uiCamera = null;
            if (_uiRoot != null)
            {
                UnityEngine.Object.Destroy(_uiRoot.gameObject);
                _uiRoot = null;
            }

            return base.OnShutdownAsync();
        }

        private void CreateUIRoot()
        {
            // 查找场景中是否已有UIRoot
            var existingRoot = GameObject.Find(UIRootName);
            if (existingRoot != null)
            {
                _uiRoot = existingRoot.transform;
            }
            else
            {
                var rootObj = new GameObject(UIRootName);
                rootObj.layer = LayerMask.NameToLayer("UI");
                rootObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                rootObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                _uiRoot = rootObj.transform;
                UnityEngine.Object.DontDestroyOnLoad(rootObj);
            }

            CreateUICamera();
        }

        private void CreateUICamera()
        {
            var cameraObj = new GameObject(UICameraName);
            cameraObj.layer = LayerMask.NameToLayer("UI");
            cameraObj.transform.SetParent(_uiRoot, false);

            _uiCamera = cameraObj.AddComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.Depth;
            _uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            _uiCamera.orthographic = true;
            _uiCamera.depth = _uiConfig?.UICameraDepth ?? 10f;
            _uiCamera.orthographicSize = 9.6f;
            _uiCamera.nearClipPlane = 0.3f;
            _uiCamera.farClipPlane = 1000f;

            var audioListener = cameraObj.GetComponent<AudioListener>();
            if (audioListener != null)
            {
                UnityEngine.Object.Destroy(audioListener);
            }
        }

        private Transform GetOrCreateLayerRoot(UILayer layer)
        {
            if (_layerRoots.TryGetValue(layer, out var root) && root != null)
            {
                return root;
            }

            var layerName = $"{LayerNamePrefix}{layer}";
            var layerObj = new GameObject(layerName)
            {
                layer = LayerMask.NameToLayer("UI")
            };

            // 配置Canvas组件（使用 UI 相机）
            var canvas = layerObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _uiCamera;
            canvas.sortingOrder = (int)layer;
            canvas.planeDistance = _uiConfig?.PlaneDistance ?? 100f;
            
            var scaler = layerObj.AddComponent<CanvasScaler>();
            ApplyCanvasScaler(scaler);
            layerObj.AddComponent<GraphicRaycaster>();
            
            layerObj.transform.SetParent(_uiRoot, false);
            
            var result = layerObj.transform;
            _layerRoots[layer] = result;

            return result;
        }

        /// <summary>
        /// 获取或创建 Layer 的内容根节点（带 SafeArea 适配）
        /// 面板和遮罩都放在此节点下，保证兄弟排序正确
        /// </summary>
        private Transform GetOrCreateContentRoot(UILayer layer)
        {
            if (_contentRoots.TryGetValue(layer, out var root) && root != null)
            {
                return root;
            }

            var layerRoot = GetOrCreateLayerRoot(layer);

            var safeAreaObj = new GameObject("SafeArea");
            safeAreaObj.layer = LayerMask.NameToLayer("UI");
            safeAreaObj.transform.SetParent(layerRoot, false);

            var rect = safeAreaObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            safeAreaObj.AddComponent<SafeAreaAdapter>();

            _contentRoots[layer] = safeAreaObj.transform;
            return safeAreaObj.transform;
        }

        /// <summary>
        /// 设置UI信息
        /// </summary>
        private void SetupUIInfo(UIInfo uiInfo, UIOpenOptions options, Type uiType, WindowIdentifier windowIdentifier)
        {
            uiInfo.Layer = options.Layer;
            uiInfo.Param = options.Data;
            uiInfo.CloseAnimationType = options.CloseAnimationType;
            uiInfo.UIType = uiType;
            uiInfo.WindowIdentifier = windowIdentifier;
        }

        /// <summary>
        /// 注册UI到字典和映射
        /// </summary>
        private void RegisterUI(WindowIdentifier windowIdentifier, UIInfo uiInfo)
        {
            _uiInfos[windowIdentifier] = uiInfo;
            _idToIdentifier[windowIdentifier.ID] = windowIdentifier;
        }


        /// <summary>
        /// 尝试设置UI参数（通过UIBase基类）
        /// </summary>
        private void TrySetUIParam(UIBase ui, object param)
        {
            if (ui == null)
            {
                return;
            }

            try
            {
                ui.SetParam(param);
            }
            catch (Exception ex)
            {
                LogWarning($"[{Name}] 设置UI参数失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化UI信息并缓存组件（性能优化）
        /// </summary>
        private UIInfo InitializeUIInfo(UIBase ui)
        {
            if (ui == null)
            {
                throw new ArgumentNullException(nameof(ui));
            }

            var uiInfo = new UIInfo
            {
                UI = ui
            };

            // 获取或添加CanvasGroup（用于管理UI的显示/隐藏、alpha、交互等）
            var gameObject = ui.gameObject;
            uiInfo.CanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (uiInfo.CanvasGroup == null)
            {
                uiInfo.CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            return uiInfo;
        }

        /// <summary>
        /// 尝试播放打开动画（使用策略模式，异步等待完成）
        /// 在动画期间禁用交互，动画完成后或异常时恢复交互
        /// </summary>
        private async UniTask TryPlayOpenAnimationAsync(UIInfo uiInfo, UIAnimationType animationType,
            CancellationToken cancellationToken = default)
        {
            if (uiInfo?.UI == null)
            {
                return;
            }

            // 动画开始前：禁用交互，防止用户在动画期间点击
            uiInfo.SetInteractable(false);

            try
            {
                var strategy = UIAnimationStrategyFactory.CreateStrategy(animationType);
                if (strategy?.IsSupported(uiInfo.UI) == true)
                {
                    await strategy.PlayOpenAnimationAsync(uiInfo, cancellationToken);
                }

                // 动画完成后：启用交互
                uiInfo.SetInteractable(true);
            }
            catch (Exception ex)
            {
                // 异常时：确保恢复交互状态（防止窗口卡在不可交互状态）
                uiInfo.SetInteractable(true);
                LogWarning($"[{Name}] 播放打开动画失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试播放关闭动画（使用策略模式，异步等待完成）
        /// 在动画期间禁用交互，动画完成后保持禁用（因为窗口要关闭）
        /// </summary>
        private async UniTask TryPlayCloseAnimationAsync(UIInfo uiInfo, UIAnimationType animationType,
            CancellationToken cancellationToken = default)
        {
            if (uiInfo?.UI == null)
            {
                return;
            }

            // 动画开始前：禁用交互，防止用户在动画期间点击
            uiInfo.SetInteractable(false);

            try
            {
                var strategy = UIAnimationStrategyFactory.CreateStrategy(animationType);
                if (strategy?.IsSupported(uiInfo.UI) == true)
                {
                    await strategy.PlayCloseAnimationAsync(uiInfo, cancellationToken);
                }

                // 关闭动画完成后：保持禁用状态（窗口即将关闭，不需要恢复交互）
            }
            catch (Exception ex)
            {
                // 异常时：确保保持禁用状态（窗口要关闭，不应该恢复交互）
                uiInfo.SetInteractable(false);
                LogWarning($"[{Name}] 播放关闭动画失败: {ex.Message}");
            }
        }

        #region 遮罩管理

        private struct MaskRequest
        {
            public WindowIdentifier Identifier;
            public UILayer Layer;
            public Color Color;
            public bool ClickToClose;
        }

        private void RequestMask(WindowIdentifier identifier, UILayer layer, Color color, bool clickToClose)
        {
            _maskRequests.Add(new MaskRequest
            {
                Identifier = identifier,
                Layer = layer,
                Color = color,
                ClickToClose = clickToClose
            });
            RefreshMask();
        }

        private void ReleaseMask(WindowIdentifier identifier)
        {
            var removed = false;
            for (var i = _maskRequests.Count - 1; i >= 0; i--)
            {
                if (_maskRequests[i].Identifier == identifier)
                {
                    _maskRequests.RemoveAt(i);
                    removed = true;
                    break;
                }
            }

            if (removed)
            {
                RefreshMask();
            }
        }

        /// <summary>
        /// 根据遮罩请求栈重建遮罩
        /// 始终只保留一个遮罩，定位在最顶部请求遮罩的面板背后
        /// </summary>
        private void RefreshMask()
        {
            if (_activeMask != null)
            {
                UnityEngine.Object.Destroy(_activeMask);
                _activeMask = null;
            }

            if (_maskRequests.Count == 0) return;

            var top = _maskRequests[^1];

            if (!TryGetUIInfo(top.Identifier, out var uiInfo) || uiInfo.UI == null) return;

            var contentRoot = GetOrCreateContentRoot(top.Layer);
            _activeMask = CreateMaskGameObject(contentRoot, top.Color, top.ClickToClose, top.Identifier);
            var panelIndex = uiInfo.UI.transform.GetSiblingIndex();
            _activeMask.transform.SetSiblingIndex(panelIndex);
        }

        private GameObject CreateMaskGameObject(Transform parent, Color maskColor, bool clickToClose,
            WindowIdentifier identifier)
        {
            var maskObj = new GameObject(UIMaskName);
            maskObj.transform.SetParent(parent, false);

            var rectTransform = maskObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(5000, 5000);
            rectTransform.anchoredPosition = Vector2.zero;

            var image = maskObj.AddComponent<Image>();
            image.color = maskColor;

            if (clickToClose && identifier != null)
            {
                var button = maskObj.AddComponent<Button>();
                var id = identifier;
                button.onClick.AddListener(() =>
                {
                    if (TryGetUIInfo(id, out var info) && info.IsValid)
                    {
                        GF.UI.Close(id.ID, true);
                    }
                });
            }

            return maskObj;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将 FrameworkConfig 中的设计分辨率配置应用到 CanvasScaler
        /// </summary>
        private void ApplyCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null || _uiConfig == null) return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _uiConfig.DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = _uiConfig.ScreenMatchMode;
        }

        /// <summary>
        /// 验证打开选项参数（增强健壮性）
        /// </summary>
        private void ValidateOpenOptions(UIOpenOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.WindowIdentifier == null)
            {
                throw new ArgumentException("UIOpenOptions.WindowIdentifier不能为null", nameof(options));
            }
        }

        /// <summary>
        /// 解析UI类型（统一方法，增强可读性）
        /// </summary>
        private Type ResolveUIType(string windowName)
        {
            var uiType = _pathResolver.GetUIType(windowName);
            if (uiType == null)
            {
                var msg =
                    $"[{Name}] 无法找到窗口名称 '{windowName}' 对应的UI类型。请确保WindowName与UI类型名称一致，或实现自定义的IUIResourcePathResolver";
                LogError(msg);
                throw new JulyException(msg);
            }

            if (!typeof(UIBase).IsAssignableFrom(uiType))
            {
                throw new JulyException($"类型 {uiType.Name} 必须继承自UIBase");
            }

            return uiType;
        }



        /// <summary>
        /// 尝试获取UI信息（统一方法）
        /// </summary>
        public bool TryGetUIInfo(WindowIdentifier identifier, out UIInfo uiInfo)
        {
            uiInfo = null;
            return identifier != null &&
                   _uiInfos.TryGetValue(identifier, out uiInfo) &&
                   uiInfo != null;
        }

        /// <summary>
        /// 通过UI实例查找对应的WindowIdentifier
        /// </summary>
        private WindowIdentifier FindIdentifierByUI(UIBase ui)
        {
            if (ui == null)
            {
                return null;
            }

            // 遍历所有UIInfo，查找匹配的UI实例
            foreach (var kvp in _uiInfos)
            {
                if (kvp.Value?.UI == ui)
                {
                    return kvp.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取关闭动画类型
        /// </summary>
        private UIAnimationType GetCloseAnimationType(WindowIdentifier identifier)
        {
            return identifier != null && _uiInfos.TryGetValue(identifier, out var info) && info != null
                ? info.CloseAnimationType
                : UIAnimationType.None;
        }

        /// <summary>
        /// 安全调用UI生命周期方法（通用方法，减少代码冗余）
        /// </summary>
        /// <param name="ui">UI实例</param>
        /// <param name="action">要执行的生命周期方法</param>
        /// <param name="methodName">方法名称（用于日志）</param>
        private void SafeCallUILifecycle(UIBase ui, Action<UIBase> action, string methodName)
        {
            if (ui == null)
            {
                return;
            }

            try
            {
                action(ui);
            }
            catch (Exception ex)
            {
                LogWarning($"[{ui.name}] 调用UI {methodName}失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全调用UI的OnBeforeOpen方法
        /// </summary>
        private void SafeCallOnBeforeOpen(UIBase ui)
        {
            SafeCallUILifecycle(ui, u => u.BeforeOpen(), nameof(UIBase.BeforeOpen));
        }

        public void ShowUI(WindowIdentifier identifier)
        {
            LogWarning($"[{Name}] ShowUI 已废弃，关闭即销毁模式下不支持重新显示");
        }

        private void SafeCallOnClose(UIBase ui)
        {
            SafeCallUILifecycle(ui, u => u.Close(), nameof(UIBase.Close));
        }

        /// <summary>
        /// 安全调用UI的OnAfterClose方法
        /// </summary>
        private void SafeCallOnAfterClose(UIBase ui)
        {
            SafeCallUILifecycle(ui, u => u.AfterClose(), nameof(UIBase.AfterClose));
        }

        #endregion

        #region Tip

        /// <summary>
        /// 显示 Tip 提示
        /// </summary>
        public void ShowTip(string message, float duration = 0)
        {
            if (_tipManager == null)
            {
                LogWarning($"[{Name}] TipManager 未初始化，无法显示 Tip");
                return;
            }

            _tipManager.Show(message, duration);
        }

        #endregion
    }

    /// <summary>
    /// Tip 管理器（UIProvider 内部使用）
    /// 使用框架对象池管理 TipItem
    /// </summary>
    internal class TipManager
    {
        private readonly TipConfig _config;
        private readonly Core.Config.UIConfig _uiConfig;
        private readonly IResourceProvider _resourceProvider;
        private readonly IPoolProvider _poolProvider;
        private readonly Transform _uiRoot;

        private GameObject _tipPrefab;
        private Transform _tipContainer;
        private bool _isInitialized;

        // 使用框架对象池
        private IObjectPool<UITipItem> _tipPool;
        private readonly List<UITipItem> _activeTips = new();

        public TipManager(TipConfig config, Core.Config.UIConfig uiConfig, IResourceProvider resourceProvider, IPoolProvider poolProvider, Transform uiRoot, ProviderBase provider)
        {
            _config = config;
            _uiConfig = uiConfig;
            _resourceProvider = resourceProvider;
            _poolProvider = poolProvider;
            _uiRoot = uiRoot;
        }

        public async UniTask InitAsync()
        {
            CreateTipContainer();
            await PreloadTipPrefab();
            CreateTipPool();
            _isInitialized = true;
        }

        private void CreateTipContainer()
        {
            var containerObj = new GameObject("TipContainer");
            containerObj.transform.SetParent(_uiRoot, false);

            // 配置 Canvas（Overlay 模式，始终渲染在所有 Camera Canvas 之上）
            var canvas = containerObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = containerObj.AddComponent<CanvasScaler>();
            if (_uiConfig != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = _uiConfig.DesignResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = _uiConfig.ScreenMatchMode;
            }
            containerObj.AddComponent<GraphicRaycaster>().enabled = false; // Tip 不接收射线

            // 设置 RectTransform
            var rect = containerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 100);

            _tipContainer = containerObj.transform;
        }

        private async UniTask PreloadTipPrefab()
        {
            if (_resourceProvider == null || string.IsNullOrEmpty(_config.TipPrefabPath))
            {
                return;
            }

            try
            {
                _tipPrefab = await _resourceProvider.LoadAsync<GameObject>(_config.TipPrefabPath);
            }
            catch (Exception ex)
            {
                JLogger.LogWarning($"[TipManager] 加载 Tip 预制体失败: {ex.Message}");
            }
        }

        private void CreateTipPool()
        {
            if (_tipPrefab == null) return;

            _tipPool = _poolProvider.CreatePool(
                createFunc: CreateTipItem,
                onGet: OnGetTip,
                onReturn: OnReturnTip,
                onDestroy: OnDestroyTip,
                maxSize: _config.PoolMaxSize
            );
        }

        private UITipItem CreateTipItem()
        {
            var go = UnityEngine.Object.Instantiate(_tipPrefab, _tipContainer);
            var tip = go.GetComponent<UITipItem>();
            if (tip == null)
            {
                tip = go.AddComponent<UITipItem>();
            }
            return tip;
        }

        private void OnGetTip(UITipItem tip)
        {
            if (tip != null)
            {
                tip.transform.SetParent(_tipContainer, false);
                tip.gameObject.SetActive(true);
            }
        }

        private void OnReturnTip(UITipItem tip)
        {
            if (tip != null)
            {
                tip.gameObject.SetActive(false);
                tip.Reset();
            }
        }

        private void OnDestroyTip(UITipItem tip)
        {
            if (tip != null)
            {
                UnityEngine.Object.Destroy(tip.gameObject);
            }
        }

        public void Show(string message, float duration = 0)
        {
            if (!_isInitialized || _tipPool == null)
            {
                JLogger.LogWarning($"[TipManager] Tip 未初始化或预制体未加载: {message}");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var tip = _tipPool.Get();
            if (tip == null)
            {
                return;
            }

            // 上移现有 Tip
            MoveUpExistingTips(tip.GetHeight() + _config.Spacing);

            // 显示新 Tip（带入场动画）
            var showDuration = duration > 0 ? duration : _config.DefaultDuration;
            tip.Show(message, showDuration, _config.FadeOutDuration, OnTipComplete, 
                _config.EnterOffset, _config.EnterDuration);

            _activeTips.Add(tip);
        }

        private void MoveUpExistingTips(float offset)
        {
            foreach (var tip in _activeTips)
            {
                if (tip != null && tip.gameObject.activeSelf)
                {
                    tip.MoveUp(offset, _config.MoveUpDuration);
                }
            }
        }
  
        private void OnTipComplete(UITipItem tip)
        {
            _activeTips.Remove(tip);
            _tipPool?.Return(tip);
        }
    }
}