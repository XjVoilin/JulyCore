using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JulyCore.Core;
using JulyCore.Provider.Base;
using UnityEngine;

namespace JulyCore.Provider.Platform
{
    public abstract class PlatformProviderBase : ProviderBase, IPlatformProvider
    {
        private readonly Dictionary<Type, IPlatformService> _services = new();

        protected override LogChannel LogChannel => LogChannel.Platform;
        public override int Priority => Frameworkconst.PriorityPlatformProvider;

        public abstract int PlatformType { get; }

        protected void RegisterService<T>(T service) where T : class, IPlatformService
            => _services[typeof(T)] = service;

        public T GetService<T>() where T : class
            => _services.TryGetValue(typeof(T), out var s) ? (T)(object)s : null;

        public virtual Rect GetSafeArea() => Screen.safeArea;

        protected virtual UniTask InitSDKAsync() => UniTask.CompletedTask;

        protected override async UniTask OnInitAsync()
        {
            await InitSDKAsync();

            RegisterServices();

            foreach (var s in _services.Values)
            {
                if (s is INeedGetService needGet)
                    needGet.ServiceGetter = type => _services.GetValueOrDefault(type);
            }

            foreach (var s in _services.Values)
                s.Init();

            foreach (var s in _services.Values)
                s.PostInit();

            var asyncTasks = new List<UniTask>();
            foreach (var s in _services.Values)
                asyncTasks.Add(s.PostInitAsync());
            await UniTask.WhenAll(asyncTasks);
        }

        public void DeferAllServices()
        {
            foreach (var s in _services.Values)
                s.DeferredInit();
        }

        protected abstract void RegisterServices();
    }
}
