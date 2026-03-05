using System;

namespace JulyCore.Core
{
    /// <summary>
    /// 框架统一结果类型（无数据）
    /// </summary>
    public readonly struct FrameworkResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 错误码
        /// </summary>
        public FrameworkErrorCode ErrorCode { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 原始异常（如果有）
        /// </summary>
        public Exception Exception { get; }

        private FrameworkResult(bool isSuccess, FrameworkErrorCode errorCode, string message, Exception exception)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            Message = message;
            Exception = exception;
        }

        #region 工厂方法

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static FrameworkResult Success() => new(true, FrameworkErrorCode.Success, null, null);

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static FrameworkResult Failure(FrameworkErrorCode errorCode, string message = null)
            => new(false, errorCode, message ?? GetDefaultMessage(errorCode), null);

        /// <summary>
        /// 创建失败结果（带异常）
        /// </summary>
        public static FrameworkResult Failure(FrameworkErrorCode errorCode, Exception exception)
            => new(false, errorCode, exception?.Message ?? GetDefaultMessage(errorCode), exception);

        /// <summary>
        /// 从异常创建失败结果
        /// </summary>
        public static FrameworkResult FromException(Exception exception)
        {
            var errorCode = exception switch
            {
                OperationCanceledException => FrameworkErrorCode.Cancelled,
                TimeoutException => FrameworkErrorCode.Timeout,
                ArgumentNullException => FrameworkErrorCode.NullReference,
                ArgumentException => FrameworkErrorCode.InvalidArgument,
                InvalidOperationException => FrameworkErrorCode.InvalidState,
                JulyException je => je.ErrorCode,
                _ => FrameworkErrorCode.Unknown
            };
            return new FrameworkResult(false, errorCode, exception.Message, exception);
        }

        #endregion

        #region 转换方法

        /// <summary>
        /// 转换为泛型结果
        /// </summary>
        public FrameworkResult<T> ToResult<T>(T value = default)
            => IsSuccess ? FrameworkResult<T>.Success(value) : FrameworkResult<T>.Failure(ErrorCode, Message, Exception);

        /// <summary>
        /// 如果失败则抛出异常
        /// </summary>
        public void ThrowIfFailure()
        {
            if (IsFailure)
            {
                throw Exception ?? new JulyException(ErrorCode, Message);
            }
        }

        #endregion

        #region 辅助方法

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
                FrameworkErrorCode.ProviderNotFound => "Provider未找到",
                FrameworkErrorCode.ResourceNotFound => "资源未找到",
                FrameworkErrorCode.ResourceLoadFailed => "资源加载失败",
                FrameworkErrorCode.NetworkConnectionFailed => "网络连接失败",
                FrameworkErrorCode.UINotFound => "UI未找到",
                FrameworkErrorCode.UIOpenFailed => "UI打开失败",
                FrameworkErrorCode.SaveFailed => "数据保存失败",
                FrameworkErrorCode.LoadFailed => "数据加载失败",
                _ => $"错误码: {(int)errorCode}"
            };
        }

        #endregion

        public override string ToString()
            => IsSuccess ? "Success" : $"Failure({ErrorCode}): {Message}";

        /// <summary>
        /// 隐式转换为 bool
        /// </summary>
        public static implicit operator bool(FrameworkResult result) => result.IsSuccess;
    }

    /// <summary>
    /// 框架统一结果类型（带数据）
    /// </summary>
    public readonly struct FrameworkResult<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 结果值（成功时有效）
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 错误码
        /// </summary>
        public FrameworkErrorCode ErrorCode { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 原始异常（如果有）
        /// </summary>
        public Exception Exception { get; }

        private FrameworkResult(bool isSuccess, T value, FrameworkErrorCode errorCode, string message, Exception exception)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorCode = errorCode;
            Message = message;
            Exception = exception;
        }

        #region 工厂方法

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static FrameworkResult<T> Success(T value)
            => new(true, value, FrameworkErrorCode.Success, null, null);

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static FrameworkResult<T> Failure(FrameworkErrorCode errorCode, string message = null, Exception exception = null)
            => new(false, default, errorCode, message ?? GetDefaultMessage(errorCode), exception);

        /// <summary>
        /// 从异常创建失败结果
        /// </summary>
        public static FrameworkResult<T> FromException(Exception exception)
        {
            var errorCode = exception switch
            {
                OperationCanceledException => FrameworkErrorCode.Cancelled,
                TimeoutException => FrameworkErrorCode.Timeout,
                ArgumentNullException => FrameworkErrorCode.NullReference,
                ArgumentException => FrameworkErrorCode.InvalidArgument,
                InvalidOperationException => FrameworkErrorCode.InvalidState,
                JulyException je => je.ErrorCode,
                _ => FrameworkErrorCode.Unknown
            };
            return new FrameworkResult<T>(false, default, errorCode, exception.Message, exception);
        }

        #endregion

        #region 转换方法

        /// <summary>
        /// 转换为无数据结果
        /// </summary>
        public FrameworkResult ToResult()
            => IsSuccess ? FrameworkResult.Success() : FrameworkResult.Failure(ErrorCode, Message);

        /// <summary>
        /// 获取值，失败时返回默认值
        /// </summary>
        public T GetValueOrDefault(T defaultValue = default)
            => IsSuccess ? Value : defaultValue;

        /// <summary>
        /// 获取值，失败时抛出异常
        /// </summary>
        public T GetValueOrThrow()
        {
            if (IsFailure)
            {
                throw Exception ?? new JulyException(ErrorCode, Message);
            }
            return Value;
        }

        // /// <summary>
        // /// 映射成功值
        // /// </summary>
        // public FrameworkResult<TNew> Map<TNew>(Func<T, TNew> mapper)
        // {
        //     if (IsFailure)
        //     {
        //         return FrameworkResult<TNew>.Failure(ErrorCode, Message, Exception);
        //     }
        //     try
        //     {
        //         return FrameworkResult<TNew>.Success(mapper(Value));
        //     }
        //     catch (Exception ex)
        //     {
        //         return FrameworkResult<TNew>.FromException(ex);
        //     }
        // }

        #endregion

        #region 辅助方法

        private static string GetDefaultMessage(FrameworkErrorCode errorCode)
        {
            return errorCode switch
            {
                FrameworkErrorCode.Success => "操作成功",
                FrameworkErrorCode.Unknown => "未知错误",
                _ => $"错误码: {(int)errorCode}"
            };
        }

        #endregion

        public override string ToString()
            => IsSuccess ? $"Success: {Value}" : $"Failure({ErrorCode}): {Message}";

        /// <summary>
        /// 隐式转换为 bool
        /// </summary>
        public static implicit operator bool(FrameworkResult<T> result) => result.IsSuccess;

        /// <summary>
        /// 从值隐式转换
        /// </summary>
        public static implicit operator FrameworkResult<T>(T value) => Success(value);
    }
}

