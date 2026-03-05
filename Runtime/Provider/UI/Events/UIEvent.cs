using JulyCore.Core;
using JulyCore.Data.UI;

namespace JulyCore.Provider.UI.Events
{
    /// <summary>
    /// UI打开事件
    /// </summary>
    public class UIOpenEvent : IEvent
    {
        public WindowIdentifier Identifier { get; set; }

        /// <summary>
        /// UI层级
        /// </summary>
        public UILayer Layer { get; set; }

        /// <summary>
        /// 打开参数
        /// </summary>
        public object Param { get; set; }
    }

    /// <summary>
    /// UI关闭事件
    /// </summary>
    public class UICloseEvent : IEvent
    {
        public WindowIdentifier Identifier { get; set; }

        /// <summary>
        /// UI层级
        /// </summary>
        public UILayer Layer { get; set; }
        
        /// <summary>
        /// 是否被销毁
        /// </summary>
        public bool IsDestroyed { get; set; }
    }
}
      