using System;
using Cysharp.Threading.Tasks;
using JulyCore.Core.Config;
using JulyCore.Core.Launch;
using UnityEngine;

namespace JulyCore.Core
{
    public abstract class JulyGameEntry : MonoBehaviour
    {
        [Header("框架配置文件")]
        [SerializeField]
        protected FrameworkConfig frameworkConfig;

        private bool _coreReady;
        private bool _isInit;

        protected bool IsInitialized => _isInit;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            RunPipeline().Forget();
        }

        private async UniTask RunPipeline()
        {
            try
            {
                var context = FrameworkContext._instance = new FrameworkContext(frameworkConfig);

                var ctx = new LaunchContext(
                    frameworkConfig,
                    destroyCancellationToken,
                    context.Registry,
                    context.ModuleService,
                    context.ProviderService,
                    context);

                ctx.OnCoreReady = () => _coreReady = true;

                var pipeline = new LaunchPipeline();
                ConfigurePipeline(pipeline);

                if (await pipeline.ExecuteAsync(ctx))
                {
                    _isInit = true;
                    JLogger.Log("[Launch] Complete");
                }
            }
            catch (OperationCanceledException)
            {
                JLogger.Log("[Launch] Cancelled");
            }
            catch (Exception ex)
            {
                JLogger.LogException(ex);
            }
        }

        protected abstract void ConfigurePipeline(LaunchPipeline pipeline);

        protected virtual void Update()
        {
            if (!_coreReady) return;
            FrameworkContext.Instance.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        protected virtual void OnDestroy()
        {
            ShutdownFramework().Forget();
        }

        private async UniTask ShutdownFramework()
        {
            try
            {
                if (FrameworkContext.Instance != null)
                    await FrameworkContext.Instance.ShutdownAsync();
                JLogger.Log("[Launch] Framework shutdown");
            }
            catch (Exception ex)
            {
                JLogger.LogException(ex);
            }
        }
    }
}
