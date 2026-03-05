using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace JulyCore.Core
{
    /// <summary>
    /// Result 扩展方法
    /// 提供将异步操作转换为 Result 模式的工具方法
    /// </summary>
    public static class ResultExtensions
    {
        #region UniTask 扩展

        /// <summary>
        /// 将 UniTask 转换为 FrameworkResult
        /// 自动捕获异常并转换为 Result
        /// </summary>
        public static async UniTask<FrameworkResult> ToResultAsync(this UniTask task)
        {
            try
            {
                await task;
                return FrameworkResult.Success();
            }
            catch (OperationCanceledException)
            {
                return FrameworkResult.Failure(FrameworkErrorCode.Cancelled);
            }
            catch (Exception ex)
            {
                return FrameworkResult.FromException(ex);
            }
        }

        /// <summary>
        /// 将 UniTask转换为 FrameworkResult;
        /// 自动捕获异常并转换为 Result
        /// </summary>
        public static async UniTask<FrameworkResult<T>> ToResultAsync<T>(this UniTask<T> task)
        {
            try
            {
                var result = await task;
                return FrameworkResult<T>.Success(result);
            }
            catch (OperationCanceledException)
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.Cancelled);
            }
            catch (Exception ex)
            {
                return FrameworkResult<T>.FromException(ex);
            }
        }

        /// <summary>
        /// 将 UniTask 转换为 FrameworkResult，支持自定义错误码
        /// </summary>
        public static async UniTask<FrameworkResult<T>> ToResultAsync<T>(
            this UniTask<T> task,
            FrameworkErrorCode defaultErrorCode)
        {
            try
            {
                var result = await task;
                return FrameworkResult<T>.Success(result);
            }
            catch (OperationCanceledException)
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.Cancelled);
            }
            catch (Exception ex)
            {
                var errorCode = ex is JulyException je ? je.ErrorCode : defaultErrorCode;
                return FrameworkResult<T>.Failure(errorCode, ex.Message, ex);
            }
        }

        #endregion

        #region 安全执行

        /// <summary>
        /// 安全执行异步操作，返回 Result
        /// </summary>
        public static async UniTask<FrameworkResult> TryExecuteAsync(
            Func<UniTask> action,
            FrameworkErrorCode defaultErrorCode = FrameworkErrorCode.Unknown)
        {
            try
            {
                await action();
                return FrameworkResult.Success();
            }
            catch (OperationCanceledException)
            {
                return FrameworkResult.Failure(FrameworkErrorCode.Cancelled);
            }
            catch (Exception ex)
            {
                var errorCode = ex is JulyException je ? je.ErrorCode : defaultErrorCode;
                return FrameworkResult.Failure(errorCode, ex.Message);
            }
        }

        /// <summary>
        /// 安全执行异步操作，返回 Result
        /// </summary>
        public static async UniTask<FrameworkResult<T>> TryExecuteAsync<T>(
            Func<UniTask<T>> action,
            FrameworkErrorCode defaultErrorCode = FrameworkErrorCode.Unknown)
        {
            try
            {
                var result = await action();
                return FrameworkResult<T>.Success(result);
            }
            catch (OperationCanceledException)
            {
                return FrameworkResult<T>.Failure(FrameworkErrorCode.Cancelled);
            }
            catch (Exception ex)
            {
                var errorCode = ex is JulyException je ? je.ErrorCode : defaultErrorCode;
                return FrameworkResult<T>.Failure(errorCode, ex.Message, ex);
            }
        }

        /// <summary>
        /// 安全执行同步操作，返回 Result
        /// </summary>
        public static FrameworkResult TryExecute(
            Action action,
            FrameworkErrorCode defaultErrorCode = FrameworkErrorCode.Unknown)
        {
            try
            {
                action();
                return FrameworkResult.Success();
            }
            catch (Exception ex)
            {
                var errorCode = ex is JulyException je ? je.ErrorCode : defaultErrorCode;
                return FrameworkResult.Failure(errorCode, ex.Message);
            }
        }

        /// <summary>
        /// 安全执行同步操作，返回 Result
        /// </summary>
        public static FrameworkResult<T> TryExecute<T>(
            Func<T> action,
            FrameworkErrorCode defaultErrorCode = FrameworkErrorCode.Unknown)
        {
            try
            {
                var result = action();
                return FrameworkResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                var errorCode = ex is JulyException je ? je.ErrorCode : defaultErrorCode;
                return FrameworkResult<T>.Failure(errorCode, ex.Message, ex);
            }
        }

        #endregion

        #region Result 链式操作

        /// <summary>
        /// 如果成功则执行下一个操作
        /// </summary>
        public static async UniTask<FrameworkResult> ThenAsync(
            this FrameworkResult result,
            Func<UniTask> nextAction)
        {
            if (result.IsFailure)
            {
                return result;
            }

            return await TryExecuteAsync(nextAction);
        }

        /// <summary>
        /// 如果成功则执行下一个操作
        /// </summary>
        public static async UniTask<FrameworkResult<TNew>> ThenAsync<T, TNew>(
            this FrameworkResult<T> result,
            Func<T, UniTask<TNew>> nextAction)
        {
            if (result.IsFailure)
            {
                return FrameworkResult<TNew>.Failure(result.ErrorCode, result.Message, result.Exception);
            }

            return await TryExecuteAsync(() => nextAction(result.Value));
        }

        /// <summary>
        /// 如果失败则执行恢复操作
        /// </summary>
        public static async UniTask<FrameworkResult<T>> RecoverAsync<T>(
            this FrameworkResult<T> result,
            Func<FrameworkErrorCode, UniTask<T>> recoveryAction)
        {
            if (result.IsSuccess)
            {
                return result;
            }

            return await TryExecuteAsync(() => recoveryAction(result.ErrorCode));
        }

        #endregion
    }
}

