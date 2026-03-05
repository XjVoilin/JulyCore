using System;

namespace JulyCore.Core
{
    /// <summary>
    /// 框架自定义异常类
    /// </summary>
    public class JulyException : Exception
    {
        /// <summary>
        /// 错误码
        /// </summary>
        public FrameworkErrorCode ErrorCode { get; }

        /// <summary>
        /// 创建无参异常
        /// </summary>
        public JulyException()
        {
            ErrorCode = FrameworkErrorCode.Unknown;
        }

        /// <summary>
        /// 创建带消息的异常
        /// </summary>
        public JulyException(string message) : base(message)
        {
            ErrorCode = FrameworkErrorCode.Unknown;
        }

        /// <summary>
        /// 创建带消息和内部异常的异常
        /// </summary>
        public JulyException(string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = FrameworkErrorCode.Unknown;
        }

        /// <summary>
        /// 创建带错误码的异常
        /// </summary>
        public JulyException(FrameworkErrorCode errorCode, string message = null)
            : base(message ?? GetDefaultMessage(errorCode))
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 创建带错误码和内部异常的异常
        /// </summary>
        public JulyException(FrameworkErrorCode errorCode, string message, Exception innerException)
            : base(message ?? GetDefaultMessage(errorCode), innerException)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 从 FrameworkResult 创建异常
        /// </summary>
        public static JulyException FromResult(FrameworkResult result)
        {
            return new JulyException(result.ErrorCode, result.Message, result.Exception);
        }

        /// <summary>
        /// 从 FrameworkResult&lt;T&gt; 创建异常
        /// </summary>
        public static JulyException FromResult<T>(FrameworkResult<T> result)
        {
            return new JulyException(result.ErrorCode, result.Message, result.Exception);
        }

        private static string GetDefaultMessage(FrameworkErrorCode errorCode)
        {
            return errorCode switch
            {
                FrameworkErrorCode.Success => "操作成功",
                FrameworkErrorCode.Unknown => "未知错误",
                FrameworkErrorCode.InvalidArgument => "参数无效",
                FrameworkErrorCode.NullReference => "空引用",
                FrameworkErrorCode.Timeout => "操作超时",
                FrameworkErrorCode.Cancelled => "操作被取消",
                FrameworkErrorCode.InvalidState => "状态无效",
                FrameworkErrorCode.NotInitialized => "未初始化",
                FrameworkErrorCode.AlreadyInitialized => "已初始化",
                FrameworkErrorCode.NotSupported => "不支持的操作",
                FrameworkErrorCode.ModuleNotFound => "模块未找到",
                FrameworkErrorCode.ModuleNotInitialized => "模块未初始化",
                FrameworkErrorCode.ModuleInitFailed => "模块初始化失败",
                FrameworkErrorCode.ModuleDependencyError => "模块依赖错误",
                FrameworkErrorCode.ModuleCircularDependency => "模块循环依赖",
                FrameworkErrorCode.ProviderNotFound => "Provider未找到",
                FrameworkErrorCode.ProviderNotInitialized => "Provider未初始化",
                FrameworkErrorCode.ProviderInitFailed => "Provider初始化失败",
                FrameworkErrorCode.ResourceNotFound => "资源未找到",
                FrameworkErrorCode.ResourceLoadFailed => "资源加载失败",
                FrameworkErrorCode.ResourceTypeMismatch => "资源类型错误",
                FrameworkErrorCode.ResourceReleased => "资源已释放",
                FrameworkErrorCode.NetworkConnectionFailed => "网络连接失败",
                FrameworkErrorCode.NetworkDisconnected => "网络已断开",
                FrameworkErrorCode.NetworkRequestFailed => "网络请求失败",
                FrameworkErrorCode.NetworkTimeout => "网络请求超时",
                FrameworkErrorCode.MessageSendFailed => "消息发送失败",
                FrameworkErrorCode.UINotFound => "UI未找到",
                FrameworkErrorCode.UIOpenFailed => "UI打开失败",
                FrameworkErrorCode.UIPrefabLoadFailed => "UI预制体加载失败",
                FrameworkErrorCode.UITypeMismatch => "UI类型错误",
                FrameworkErrorCode.SerializeFailed => "数据序列化失败",
                FrameworkErrorCode.DeserializeFailed => "数据反序列化失败",
                FrameworkErrorCode.SaveFailed => "数据保存失败",
                FrameworkErrorCode.LoadFailed => "数据加载失败",
                FrameworkErrorCode.EncryptFailed => "数据加密失败",
                FrameworkErrorCode.DecryptFailed => "数据解密失败",
                FrameworkErrorCode.ConfigNotFound => "配置未找到",
                FrameworkErrorCode.ConfigFormatError => "配置格式错误",
                FrameworkErrorCode.ConfigInvalidValue => "配置值无效",
                _ => $"错误码: {(int)errorCode}"
            };
        }

        public override string ToString()
        {
            return $"[JulyException] Code: {ErrorCode} ({(int)ErrorCode}), Message: {Message}";
        }
    }
}
