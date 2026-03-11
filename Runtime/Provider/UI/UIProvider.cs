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
using JulyCore.Provider.UI.Pool;
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

        /// <summary>
        /// 窗口标识符（必须提供，用于唯一标识窗口）
        /// </summary>
        public WindowIdentifier WindowIdentifier { get; set; }

        /// <summary>
        /// UI类型（用于资源路径解析和对象池管理）
        /// </summary>
        public Type UIType { get; set; }

        public UILayer Layer { get; set; }
        public object Param { get; set; }
        public GameObject Mask { get; set; }

        // 缓存的组件引用（性能优化）
        public CanvasGroup CanvasGroup { get; set; }

        // 关闭动画类型（从打开时的UIOpenOptions中保存）
        public UIAnimationType CloseAnimationType { get; set; } = UIAnimationType.None;

        /// <summary>
        /// UI是否有效（存在且已打开）
        /// </summary>
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

        /// <summary>
        /// 设置UI的交互状态（用于动画期间禁用/启用交互）
        /// 同时控制窗口本身和遮罩层的交互状态
        /// </summary>
        /// <param name="interactable">是否可交互</param>
        public void SetInteractable(bool interactable)
        {
            // 控制窗口本身的交互状态
            if (CanvasGroup != null)
            {
                CanvasGroup.interactable = interactable;
                CanvasGroup.blocksRaycasts = interactable;
            }

            // 控制遮罩层的交互状态（如果存在）
            // 遮罩层在CreateMask时会自动添加CanvasGroup，确保统一管理
            if (Mask != null)
            {
                var maskCanvasGroup = Mask.GetComponent<CanvasGroup>();
                if (maskCanvasGroup != null)
                {
                    maskCanvasGroup.interactable = interactable;
                    maskCanvasGroup.blocksRaycasts = interactable;
                }
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

        // 技术层数据存储（不包含业务逻辑）
        private readonly Dictionary<UILayer, Transform> _layerRoots = new Dictionary<UILayer, Transform>();
        private readonly Dictionary<WindowIdentifier, UIInfo> _uiInfos = new Dictionary<WindowIdentifier, UIInfo>();
        private readonly Dictionary<int, WindowIdentifier> _idToIdentifier = new Dictionary<int, WindowIdentifier>();
        private readonly Dictionary<Type, GameObject> _preloadedPrefabs = new Dictionary<Type, GameObject>();
        private readonly UIPool _uiPool = new UIPool(maxSizePerType: 5);

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
            // 参数验证
            ValidateOpenOptions(options);

            var windowIdentifier = options.WindowIdentifier;

            // 通过WindowName自动解析UI类型
            var uiType = ResolveUIType(windowIdentifier.WindowName);

            // 检查是否已存在
            if (!TryGetUIInfo(windowIdentifier, out var existingInfo))
            {
                // 不存在，直接创建新实例
                var newPrefab = await LoadWindowPrefab(uiType, cancellationToken);
                return await CreateUIWindow(options, uiType, newPrefab, cancellationToken);
            }

            // 处理已存在的窗口
            if (options.CloseExisting)
            {
                Log($"[{Name}] UI {windowIdentifier} 已存在，关闭旧实例");
                await CloseInternalAsync(existingInfo, true, UIAnimationType.None, cancellationToken);
                // 关闭后继续创建新实例
            }
            else if (existingInfo.IsValid && existingInfo.UI != null)
            {
                // 已打开，直接返回并更新参数
                Log($"[{Name}] UI {windowIdentifier} 已打开，直接返回");
                if (options.Data != null)
                {
                    TrySetUIParam(existingInfo.UI, options.Data);
                }
                existingInfo.UI.Open();
                return existingInfo.UI;
            }
            else if (existingInfo.UI != null)
            {
                // 存在但未打开，重新显示
                Log($"[{Name}] UI {windowIdentifier} 已存在但未打开，重新显示");
                return await ReopenExistingWindowAsync(existingInfo, options, cancellationToken);
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
                Log($"[{Name}] 使用预加载的UI: {uiType.Name}");
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

            // 实例化UI
            var layerRoot = GetOrCreateLayerRoot(options.Layer);
            UIBase component = null;
            GameObject mask = null;
            try
            {
                // 创建遮罩（如果需要）
                if (options.ShowMask)
                {
                    mask = CreateMask(layerRoot, options.MaskColor, options.ClickMaskToClose, windowIdentifier);
                }

                // 尝试从对象池获取，如果池中没有则实例化
                component = _uiPool.GetOrCreate(uiType, layerRoot);
                if (component == null)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab, layerRoot);
                    instance.transform.SetParent(layerRoot, false);
                    instance.name = windowIdentifier.WindowName;
                    component = instance.GetComponent(uiType) as UIBase;
                    
                    if (component == null)
                    {
                        var msg = $"[{Name}] UI {uiType.Name} 预制体上未找到组件 {uiType.Name}";
                        LogError(msg);
                        CleanupOnError(mask, instance);
                        throw new JulyException(msg);
                    }
                }
                else
                {
                    component.gameObject.name = windowIdentifier.WindowName;
                }

                // 初始化并缓存组件（性能优化）
                var uiInfo = InitializeUIInfo(component);

                // 设置CanvasGroup初始状态（确保UI可见且可交互）
                uiInfo.Visible(true);

                // 传递参数
                if (options.Data != null)
                {
                    TrySetUIParam(component, options.Data);
                }

                // 调用UI的OnBeforeOpen生命周期方法（在播放打开动画之前）
                SafeCallOnBeforeOpen(component);

                // 播放打开动画
                await TryPlayOpenAnimationAsync(uiInfo, options.OpenAnimationType, cancellationToken);

                // 设置UI信息
                SetupUIInfo(uiInfo, options, mask, uiType, windowIdentifier);

                // 保存到字典和映射
                RegisterUI(windowIdentifier, uiInfo, uiType, prefab);

                // 调用UI的OnOpen生命周期方法
                component.Open();

                Log($"[{Name}] UI {windowIdentifier} ({uiType.Name}) 打开成功 (层级: {options.Layer})");
                return component;
            }
            catch (Exception)
            {
                CleanupOnError(mask, component?.gameObject);
                throw;
            }
        }

        /// <summary>
        /// 关闭UI（通过WindowIdentifier，同步版本）
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <param name="destroy">是否销毁</param>
        public void Close(WindowIdentifier identifier, bool destroy = false)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            CloseInternal(identifier, destroy, GetCloseAnimationType(identifier));
        }

        /// <summary>
        /// 关闭UI（通过WindowIdentifier，异步版本，等待动画完成）
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        /// <param name="destroy">是否销毁</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async UniTask CloseAsync(WindowIdentifier identifier, bool destroy = false,
            CancellationToken cancellationToken = default)
        {
            if (identifier == null)
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            await CloseInternalAsync(identifier, destroy, GetCloseAnimationType(identifier), cancellationToken);
        }

        /// <summary>
        /// 内部关闭方法（同步版本）
        /// </summary>
        private void CloseInternal(WindowIdentifier identifier, bool destroy, UIAnimationType animationType)
        {
            // 同步版本：使用fire-and-forget方式（不等待动画完成）
            // None策略会立即完成，其他策略异步执行但不阻塞
            CloseInternalAsync(identifier, destroy, animationType, CancellationToken.None).Forget();
        }

        /// <summary>
        /// 内部关闭方法（异步版本，等待动画完成）
        /// </summary>
        private async UniTask CloseInternalAsync(WindowIdentifier identifier, bool destroy,
            UIAnimationType animationType,
            CancellationToken cancellationToken)
        {
            if (!TryGetUIInfo(identifier, out var uiInfo))
            {
                LogWarning($"[{Name}] 尝试关闭未打开的UI: {identifier}");
                return;
            }

            await CloseInternalAsync(uiInfo, destroy, animationType, cancellationToken);
        }

        private async UniTask CloseInternalAsync(UIInfo uiInfo, bool destroy,
            UIAnimationType animationType,
            CancellationToken cancellationToken)
        {
            var identifier = uiInfo.WindowIdentifier;
            // 检查UI是否有效，如果无效则直接清理字典记录
            if (!uiInfo.IsValid)
            {
                LogWarning($"[{Name}] UI {identifier} 已无效（可能已被销毁），直接清理记录");
                CloseInternalCore(identifier, destroy);
                return;
            }

            // 调用UI的OnClose生命周期方法（在播放关闭动画之前）
            SafeCallOnClose(uiInfo.UI);

            // 总是播放关闭动画（None策略会立即完成）
            await TryPlayCloseAnimationAsync(uiInfo, animationType, cancellationToken);

            // 调用UI的OnAfterClose生命周期方法（在播放关闭动画之后）
            SafeCallOnAfterClose(uiInfo.UI);

            // 执行关闭核心逻辑
            CloseInternalCore(identifier, destroy);
        }

        /// <summary>
        /// 关闭核心逻辑（不包含动画）
        /// </summary>
        private void CloseInternalCore(WindowIdentifier identifier, bool destroy)
        {
            if (!TryGetUIInfo(identifier, out var uiInfo))
            {
                return;
            }

            var ui = uiInfo.UI;
            var uiType = uiInfo.UIType;

            // 销毁遮罩
            DestroyMask(uiInfo);

            if (destroy)
            {
                DestroyUI(identifier, ui, uiType);
            }
            else
            {
                uiInfo.Visible(false);
            }

            // 从栈中移除（已移除，栈管理在Module层）
        }

        /// <summary>
        /// 销毁遮罩
        /// </summary>
        private void DestroyMask(UIInfo uiInfo)
        {
            if (uiInfo.Mask != null)
            {
                UnityEngine.Object.Destroy(uiInfo.Mask);
                uiInfo.Mask = null;
            }
        }

        /// <summary>
        /// 销毁UI实例
        /// </summary>
        private void DestroyUI(WindowIdentifier identifier, UIBase ui, Type uiType)
        {
            // 从所有字典中移除
            _uiInfos.Remove(identifier);
            
            // 只有当该ID对应的identifier是当前identifier时才移除（避免ID冲突）
            if (_idToIdentifier.TryGetValue(identifier.ID, out var existingIdentifier) &&
                existingIdentifier == identifier)
            {
                _idToIdentifier.Remove(identifier.ID);
            }

            if (ui == null)
            {
                return;
            }

            if (uiType != null)
            {
                _uiPool.ReturnToPool(uiType, ui);
                // 如果对象池已满，ReturnToPool方法内部会销毁，这里不需要额外处理
            }
            else
            {
                // 如果没有uiType，直接销毁
                UnityEngine.Object.Destroy(ui.gameObject);
            }

            Log($"[{Name}] UI {identifier} 已销毁");
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
                Log($"[{Name}] UI {uiType.Name} 已预加载");
                return true;
            }

            try
            {
                var resourcePath = _pathResolver.GetResourcePath(uiType);
                var prefab = await _resourceProvider.LoadAsync<GameObject>(resourcePath, cancellationToken);
                if (prefab != null)
                {
                    _preloadedPrefabs[uiType] = prefab;
                    Log($"[{Name}] UI {uiType.Name} 预加载成功");
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
            else
            {
                Log($"[{Name}] 批量预加载完成，共 {uiTypes.Length} 个UI");
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

                Log($"[{Name}] 释放预加载UI: {uiType.Name}");
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

            // 清空对象池
            _uiPool.ClearAllPools();

            // 清空所有字典（确保完全清理）
            _uiInfos.Clear();
            _idToIdentifier.Clear();
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
                Log($"[{Name}] 使用场景中已有的UIRoot");
            }
            else
            {
                var rootObj = new GameObject(UIRootName);
                rootObj.layer = LayerMask.NameToLayer("UI");
                rootObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                rootObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                _uiRoot = rootObj.transform;
                UnityEngine.Object.DontDestroyOnLoad(rootObj);
                Log($"[{Name}] 创建UIRoot");
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

            Log($"[{Name}] 创建UI相机 (depth: {_uiCamera.depth})");
        }

        private Transform GetOrCreateLayerRoot(UILayer layer)
        {
            if (_layerRoots.TryGetValue(layer, out var root) && root != null)
            {
                return root;
            }

            var layerName = $"{LayerNamePrefix}{layer}";
            var layerObj = new GameObject(layerName);
            layerObj.layer = LayerMask.NameToLayer("UI");
            
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

            Log($"[{Name}] 创建UI层级: {layerName} (排序: {(int)layer}), 父节点: {_uiRoot.name}");
            return result;
        }

        /// <summary>
        /// 重新打开已存在但未打开的窗口
        /// </summary>
        private async UniTask<UIBase> ReopenExistingWindowAsync(UIInfo uiInfo, UIOpenOptions options,
            CancellationToken cancellationToken = default)
        {
            var windowIdentifier = options.WindowIdentifier;
            var ui = uiInfo.UI;

            // 确保窗口在正确的层级
            EnsureCorrectLayer(ui, options.Layer);

            // 设置可见
            uiInfo.Visible(true);

            // 创建遮罩（如果需要）
            var mask = CreateOrUpdateMask(uiInfo, options, windowIdentifier);

            // 更新参数
            if (options.Data != null)
            {
                TrySetUIParam(ui, options.Data);
            }

            // 调用UI的OnBeforeOpen生命周期方法（在播放打开动画之前）
            SafeCallOnBeforeOpen(ui);

            // 播放打开动画
            await TryPlayOpenAnimationAsync(uiInfo, options.OpenAnimationType, cancellationToken);

            // 更新UI信息
            UpdateUIInfo(uiInfo, options, mask);

            // 调用UI的OnOpen生命周期方法
            ui.Open();

            Log($"[{Name}] UI {windowIdentifier} ({ui.GetType().Name}) 重新打开成功 (层级: {options.Layer})");
            return ui;
        }

        /// <summary>
        /// 确保UI在正确的层级
        /// </summary>
        private void EnsureCorrectLayer(UIBase ui, UILayer layer)
        {
            var layerRoot = GetOrCreateLayerRoot(layer);
            if (ui.gameObject.transform.parent != layerRoot)
            {
                ui.gameObject.transform.SetParent(layerRoot, false);
            }
        }

        /// <summary>
        /// 创建或更新遮罩
        /// </summary>
        private GameObject CreateOrUpdateMask(UIInfo uiInfo, UIOpenOptions options, WindowIdentifier identifier)
        {
            if (!options.ShowMask)
            {
                return null;
            }

            // 如果已有遮罩，先销毁
            DestroyMask(uiInfo);

            var layerRoot = GetOrCreateLayerRoot(options.Layer);
            return CreateMask(layerRoot, options.MaskColor, options.ClickMaskToClose, identifier);
        }

        /// <summary>
        /// 更新UI信息
        /// </summary>
        private void UpdateUIInfo(UIInfo uiInfo, UIOpenOptions options, GameObject mask)
        {
            uiInfo.Layer = options.Layer;
            uiInfo.Param = options.Data;
            uiInfo.Mask = mask;
            uiInfo.CloseAnimationType = options.CloseAnimationType;
        }


        /// <summary>
        /// 设置UI信息
        /// </summary>
        private void SetupUIInfo(UIInfo uiInfo, UIOpenOptions options, GameObject mask, Type uiType, WindowIdentifier windowIdentifier)
        {
            uiInfo.Layer = options.Layer;
            uiInfo.Param = options.Data;
            uiInfo.Mask = mask;
            uiInfo.CloseAnimationType = options.CloseAnimationType;
            uiInfo.UIType = uiType;
            uiInfo.WindowIdentifier = windowIdentifier;
        }

        /// <summary>
        /// 注册UI到字典和映射
        /// </summary>
        private void RegisterUI(WindowIdentifier windowIdentifier, UIInfo uiInfo, Type uiType, GameObject prefab)
        {
            // 保存到字典
            _uiInfos[windowIdentifier] = uiInfo;

            // 保存ID到WindowIdentifier的映射（用于通过ID快速查找）
            // 如果ID已存在且指向不同的WindowIdentifier，记录警告并覆盖（允许更新）
            if (_idToIdentifier.TryGetValue(windowIdentifier.ID, out var existingIdentifier) &&
                existingIdentifier != windowIdentifier)
            {
                LogWarning(
                    $"[{Name}] 窗口ID {windowIdentifier.ID} 已存在，将覆盖旧的映射 (旧: {existingIdentifier}, 新: {windowIdentifier})");
            }

            _idToIdentifier[windowIdentifier.ID] = windowIdentifier;

            // 注册预制体到对象池（prefab已经验证不为null）
            _uiPool.RegisterPrefab(uiType, prefab);
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
                Log($"[{Name}] UI参数设置成功: {ui.GetType().Name}");
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

        /// <summary>
        /// 创建遮罩背景
        /// </summary>
        private GameObject CreateMask(Transform parent, Color maskColor, bool clickToClose, WindowIdentifier identifier)
        {
            var maskObj = new GameObject(UIMaskName);
            maskObj.transform.SetParent(parent, false);

            // 配置RectTransform为全屏
            var rectTransform = maskObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            // 添加CanvasGroup用于统一管理交互状态（与窗口保持一致）
            var canvasGroup = maskObj.AddComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // 添加Image组件作为遮罩
            var image = maskObj.AddComponent<Image>();
            image.color = maskColor;

            // 如果支持点击关闭，添加Button组件
            if (clickToClose && identifier != null)
            {
                var button = maskObj.AddComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    CloseInternal(identifier, destroy: false, GetCloseAnimationType(identifier));
                });
            }

            // 设置遮罩在最底层
            maskObj.transform.SetAsFirstSibling();

            return maskObj;
        }

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
        /// 清理错误时的资源（增强健壮性）
        /// </summary>
        private void CleanupOnError(GameObject mask, GameObject uiInstance)
        {
            if (mask != null)
            {
                UnityEngine.Object.Destroy(mask);
            }

            if (uiInstance != null)
            {
                UnityEngine.Object.Destroy(uiInstance);
            }
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

        /// <summary>
        /// 显示UI（纯技术操作：设置UI可见并调用Open方法）
        /// </summary>
        /// <param name="identifier">窗口标识符</param>
        public void ShowUI(WindowIdentifier identifier)
        {
            if (identifier == null)
            {
                LogWarning($"[{Name}] ShowUI: identifier为null");
                return;
            }

            if (!TryGetUIInfo(identifier, out var uiInfo))
            {
                LogWarning($"[{Name}] ShowUI: 未找到UI {identifier}");
                return;
            }

            if (uiInfo.UI == null)
            {
                LogWarning($"[{Name}] ShowUI: UI实例为null {identifier}");
                return;
            }

            // 设置可见
            uiInfo.Visible(true);

            // 调用UI的Open方法（如果尚未打开）
            if (!uiInfo.UI.IsOpened)
            {
                uiInfo.UI.Open();
            }

            Log($"[{Name}] 显示UI: {identifier}");
        }

        /// <summary>
        /// 安全调用UI的OnClose方法
        /// </summary>
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

        /// <summary>
        /// 安全调用UI的OnOpen方法
        /// </summary>
        private void SafeCallOnOpen(UIBase ui)
        {
            SafeCallUILifecycle(ui, u => u.Open(), nameof(UIBase.Open));
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