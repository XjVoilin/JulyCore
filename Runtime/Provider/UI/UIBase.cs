using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Data.UI;
using UnityEngine;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// UI基类（抽象类）
    /// 所有UI组件应继承此类，提供统一的参数传递和生命周期管理
    /// 关闭即销毁，每次打开都是全新实例
    ///
    /// 生命周期：
    ///   打开: OnBeforeOpen → [动画] → OnOpen
    ///   关闭: OnClose      → [动画] → OnAfterClose → [销毁]
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        private object Data { get; set; }

        /// <summary>
        /// UI是否已打开
        /// </summary>
        public bool IsOpened { get; private set; }

        /// <summary>
        /// 设置UI参数（由UIProvider调用）
        /// </summary>
        internal virtual void SetParam(object data)
        {
            Data = data;
        }

        /// <summary>
        /// UI打开前调用（在播放打开动画之前）
        /// 用于初始化：注册按钮监听、订阅事件、缓存组件引用、刷新数据
        /// </summary>
        internal void BeforeOpen()
        {
            OnBeforeOpen();
        }

        protected virtual void OnBeforeOpen()
        {
        }

        /// <summary>
        /// UI打开时调用（在播放打开动画之后）
        /// </summary>
        internal void Open()
        {
            IsOpened = true;
            OnOpen();
        }

        protected virtual void OnOpen()
        {
        }

        /// <summary>
        /// UI关闭时调用（在播放关闭动画之前）
        /// </summary>
        internal void Close()
        {
            if (!IsOpened) return;
            IsOpened = false;
            GF.Event.UnsubscribeAll(this);
            OnClose();
        }

        protected virtual void OnClose()
        {
        }

        /// <summary>
        /// UI关闭后调用（在播放关闭动画之后）
        /// </summary>
        internal void AfterClose()
        {
            OnAfterClose();
        }

        protected virtual void OnAfterClose()
        {
        }

        protected virtual void OnDestroy()
        {
            Close();
        }

        /// <summary>
        /// 获取参数（泛型版本，方便使用）
        /// </summary>
        protected T GetData<T>()
        {
            if (Data is T data)
            {
                return data;
            }
            return default;
        }

        /// <summary>
        /// 关闭并销毁当前窗口
        /// </summary>
        /// <param name="animationType">关闭动画类型覆盖，null 则走配置</param>
        protected void CloseWindow(UIAnimationType? animationType = null)
        {
            if (!IsOpened) return;
            GF.UI.Close(this, true, animationType);
        }

        /// <summary>
        /// 关闭并销毁当前窗口（异步版本，等待动画完成）
        /// </summary>
        protected UniTask CloseWindowAsync(CancellationToken cancellationToken = default)
        {
            if (!IsOpened) return UniTask.CompletedTask;
            return GF.UI.CloseAsync(this, true, cancellationToken);
        }
    }
}
