using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore.Provider.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JulyGF.Tests.Utils
{
    /// <summary>
    /// 测试辅助工具类
    /// 提供通用的测试辅助方法
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// 创建Mock UI对象
        /// </summary>
        public static GameObject CreateMockUIGameObject(string name = "TestUI")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            return go;
        }

        /// <summary>
        /// 创建带UIBase组件的Mock UI对象
        /// </summary>
        public static T CreateMockUI<T>(string name = "TestUI") where T : UIBase
        {
            var go = CreateMockUIGameObject(name);
            var ui = go.AddComponent<T>();
            return ui;
        }

        /// <summary>
        /// 等待异步操作完成（用于Unity测试）
        /// </summary>
        public static IEnumerator WaitForUniTask(UniTask task, float timeout = 5f)
        {
            var startTime = Time.time;
            var coroutine = task.ToCoroutine();
            
            while (coroutine.MoveNext() && Time.time - startTime < timeout)
            {
                yield return coroutine.Current;
            }
            
            if (Time.time - startTime >= timeout)
            {
                Assert.Fail("异步操作超时");
            }
        }

        /// <summary>
        /// 等待异步操作完成并返回结果
        /// </summary>
        public static IEnumerator WaitForUniTask<T>(UniTask<T> task, Action<T> onComplete, float timeout = 5f)
        {
            var startTime = Time.time;
            T result = default(T);
            bool completed = false;
            
            task.ContinueWith(r =>
            {
                result = r;
                completed = true;
            }).Forget();
            
            while (!completed && Time.time - startTime < timeout)
            {
                yield return null;
            }
            
            if (Time.time - startTime >= timeout)
            {
                Assert.Fail("异步操作超时");
            }
            else
            {
                onComplete?.Invoke(result);
            }
        }

        /// <summary>
        /// 创建取消令牌（带超时）
        /// </summary>
        public static CancellationToken CreateCancellationToken(float timeoutSeconds = 5f)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return cts.Token;
        }

        /// <summary>
        /// 清理GameObject
        /// </summary>
        public static void DestroyGameObject(GameObject go)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 等待一帧
        /// </summary>
        public static IEnumerator WaitOneFrame()
        {
            yield return null;
        }

        /// <summary>
        /// 等待指定秒数
        /// </summary>
        public static IEnumerator WaitSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}

