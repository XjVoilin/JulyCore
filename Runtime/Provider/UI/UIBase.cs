using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.UI
{
    /// <summary>
    /// UI基类（抽象类）
    /// 所有UI组件应继承此类，提供统一的参数传递和生命周期管理
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        /// <summary>
        /// UI打开时传递的参数
        /// </summary>
        private object Data { get; set; }

        /// <summary>
        /// UI是否已打开
        /// </summary>
        public bool IsOpened { get; private set; }

        /// <summary>
        /// 设置UI参数（由UIProvider调用）
        /// </summary>
        /// <param name="data">打开参数</param>
        internal virtual void SetParam(object data)
        {
            Data = data;
        }

        /// <summary>
        /// UI打开前调用（在播放打开动画之前）
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
        /// 子类可重写此方法实现自定义逻辑
        /// </summary>
        internal void Close()
        {
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

        /// <summary>
        /// Unity的OnDestroy方法
        /// 子类如需重写，请调用base.OnDestroy()
        /// </summary>
        protected virtual void OnDestroy()
        {
            Close();
        }

        /// <summary>
        /// 获取参数（泛型版本，方便使用）
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <returns>参数值，如果类型不匹配则返回default(T)</returns>
        protected T GetData<T>()
        {
            if (Data is T data)
            {
                return data;
            }
            return default;
        }

        /// <summary>
        /// 关闭当前窗口（同步版本）
        /// 子类可以直接调用此方法来关闭自身
        /// </summary>
        /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
        protected void CloseWindow(bool destroy = false)
        {
            GF.UI.Close(this, destroy);
        }

        /// <summary>
        /// 关闭当前窗口（异步版本，等待动画完成）
        /// 子类可以直接调用此方法来关闭自身
        /// </summary>
        /// <param name="destroy">是否销毁（false则隐藏，可再次显示）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>关闭任务</returns>
        protected UniTask CloseWindowAsync(bool destroy = false, CancellationToken cancellationToken = default)
        {
            return GF.UI.CloseAsync(this, destroy, cancellationToken);
        }
    }
}

