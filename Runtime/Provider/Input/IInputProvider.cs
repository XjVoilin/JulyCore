using JulyCore.Core;
using UnityEngine;

namespace JulyCore.Provider.Input
{
    /// <summary>
    /// 输入提供者接口
    /// 纯技术执行层：负责底层输入读取和 EventSystem UI 检测
    /// 不包含任何业务语义（屏蔽计数等），不维护业务状态
    /// 所有业务逻辑（屏蔽管理、过滤规则）由 InputModule 处理
    /// </summary>
    public interface IInputProvider : IProvider
    {
        /// <summary>
        /// 本帧是否有原始 pointer down（不含业务过滤）
        /// Editor/Standalone: Input.GetMouseButtonDown(0)
        /// Mobile: Input.GetTouch(0).phase == Began
        /// </summary>
        bool GetRawPointerDown(out Vector2 screenPos);

        /// <summary>
        /// 本帧 pointer 是否保持按住（不含业务过滤）
        /// </summary>
        bool GetRawPointerHeld(out Vector2 screenPos);

        /// <summary>
        /// 本帧 pointer 是否抬起（不含业务过滤）
        /// </summary>
        bool GetRawPointerUp(out Vector2 screenPos);

        /// <summary>
        /// 当前触摸点数量
        /// </summary>
        int TouchCount { get; }

        /// <summary>
        /// 获取指定索引的原始触摸数据
        /// </summary>
        bool TryGetTouch(int index, out Touch touch);

        /// <summary>
        /// 当前主指针的屏幕坐标
        /// Editor/Standalone: mousePosition；Mobile: touch[0].position
        /// </summary>
        Vector2 PointerScreenPosition { get; }

        /// <summary>
        /// 指针是否在 EventSystem 管理的对象上（原始查询，含 GraphicRaycaster / PhysicsRaycaster 等）
        /// </summary>
        /// <param name="fingerId">触摸 fingerId，-1 表示鼠标</param>
        bool IsPointerOverGameObject(int fingerId = -1);
    }
}
