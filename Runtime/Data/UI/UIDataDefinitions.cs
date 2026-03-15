using System;
using UnityEngine;

namespace JulyCore.Data.UI
{
    /// <summary>
    /// UI层级枚举（手游常用层级）
    /// </summary>
    public enum UILayer
    {
        Background = 0,      // 背景层（如场景背景）
        Normal = 100,        // 普通层（主界面、功能界面）
        Popup = 200,         // 弹窗层（对话框、提示框）
        Top = 300,           // 顶层（系统提示、公告）
        Loading = 400,       // 加载层（加载界面）
        Guide = 500          // 引导层（新手引导、遮罩）
    }

    /// <summary>
    /// UI动画类型枚举
    /// </summary>
    public enum UIAnimationType
    {
        /// <summary>
        /// 无动画
        /// </summary>
        None = 0,

        /// <summary>
        /// 使用Animator组件播放动画
        /// </summary>
        Animator = 1,

        /// <summary>
        /// 淡入淡出动画（使用CanvasGroup的alpha）
        /// </summary>
        Fade = 2,

        /// <summary>
        /// 缩放动画（从0缩放到1）
        /// </summary>
        Scale = 3,

        /// <summary>
        /// 从上方滑入
        /// </summary>
        SlideFromTop = 4,

        /// <summary>
        /// 从下方滑入
        /// </summary>
        SlideFromBottom = 5,

        /// <summary>
        /// 从左侧滑入
        /// </summary>
        SlideFromLeft = 6,

        /// <summary>
        /// 从右侧滑入
        /// </summary>
        SlideFromRight = 7
    }

    /// <summary>
    /// 窗口标识符
    /// 用于唯一标识UI窗口，包含整型ID和字符串WindowName
    /// 实现IEquatable用于字典key，提供高性能的查找和比较
    /// </summary>
    [Serializable]
    public class WindowIdentifier : IEquatable<WindowIdentifier>
    {
        /// <summary>
        /// 窗口ID（整型）
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// 窗口名称（字符串）
        /// </summary>
        public string WindowName { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">窗口ID</param>
        /// <param name="windowName">窗口名称</param>
        /// <exception cref="ArgumentNullException">当windowName为null时抛出</exception>
        public WindowIdentifier(int id, string windowName)
        {
            ID = id;
            WindowName = windowName ?? throw new ArgumentNullException(nameof(windowName), "窗口名称不能为null");
        }

        /// <summary>
        /// 判断两个WindowIdentifier是否相等
        /// </summary>
        public bool Equals(WindowIdentifier other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return ID == other.ID && WindowName == other.WindowName;
        }

        /// <summary>
        /// 重写Equals方法
        /// </summary>
        public override bool Equals(object obj)
        {
            return Equals(obj as WindowIdentifier);
        }

        /// <summary>
        /// 重写GetHashCode方法（用于字典key）
        /// 使用ID和WindowName的组合哈希码，提供高性能查找
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                // 使用素数乘法组合哈希码，减少冲突
                return (ID * 397) ^ (WindowName?.GetHashCode() ?? 0);
            }
        }

        /// <summary>
        /// 重写ToString方法，便于调试和日志
        /// </summary>
        public override string ToString()
        {
            return $"WindowIdentifier(ID={ID}, Name={WindowName})";
        }

        /// <summary>
        /// 相等运算符重载
        /// </summary>
        public static bool operator ==(WindowIdentifier left, WindowIdentifier right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        /// <summary>
        /// 不等运算符重载
        /// </summary>
        public static bool operator !=(WindowIdentifier left, WindowIdentifier right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// UI打开选项
    /// </summary>
    [Serializable]
    public class UIOpenOptions
    {
        /// <summary>
        /// 窗口标识符（包含ID和WindowName）
        /// 必须提供，用于唯一标识窗口
        /// </summary>
        public WindowIdentifier WindowIdentifier { get; set; }

        /// <summary>
        /// UI层级
        /// </summary>
        public UILayer Layer { get; set; } = UILayer.Normal;

        /// <summary>
        /// 是否关闭已存在的实例（如果已打开，则关闭旧的再打开新的）
        /// </summary>
        public bool CloseExisting { get; set; } = false;

        /// <summary>
        /// 是否加入栈管理（用于返回功能）
        /// </summary>
        public bool AddToStack { get; set; } = true;

        /// <summary>
        /// 打开参数（传递给UI的数据）
        /// </summary>
        public object Data { get; set; } = null;

        /// <summary>
        /// 打开动画类型（默认为None，无动画）
        /// </summary>
        public UIAnimationType OpenAnimationType { get; set; } = UIAnimationType.None;

        /// <summary>
        /// 关闭动画类型（默认为None，无动画）
        /// </summary>
        public UIAnimationType CloseAnimationType { get; set; } = UIAnimationType.None;

        /// <summary>
        /// 是否显示遮罩背景（弹窗常用）
        /// </summary>
        public bool ShowMask { get; set; } = false;

        /// <summary>
        /// 遮罩颜色（默认半透明黑色）
        /// </summary>
        public Color MaskColor { get; set; } = new Color(0, 0, 0, 0.5f);

        /// <summary>
        /// 点击遮罩是否关闭UI（默认false）
        /// </summary>
        public bool ClickMaskToClose { get; set; } = false;
    }

    /// <summary>
    /// UI 窗口配置提供者接口。
    /// 项目侧实现此接口，将配置表数据映射为 UIOpenOptions。
    /// </summary>
    public interface IUIWindowConfigProvider
    {
        UIOpenOptions GetUIOpenOptions(int uiWindowID);
    }
}

