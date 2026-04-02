# JulyGF 游戏框架

一个专为 Unity 游戏开发设计的模块化框架，采用分层架构设计，提供清晰的职责分离和强大的扩展能力。

## 📋 目录

- [框架概述](#框架概述)
- [核心特性](#核心特性)
- [架构设计](#架构设计)
- [快速开始](#快速开始)
- [核心概念](#核心概念)
- [使用指南](#使用指南)
- [最佳实践](#最佳实践)
- [API 参考](#api-参考)

## 🎯 框架概述

JulyGF 是一个轻量级、高性能的游戏框架，旨在简化 Unity 游戏开发中的模块管理和系统集成。框架采用 **Provider-Module** 分层架构，将底层技术能力与业务逻辑清晰分离。

### 设计理念

- **分层清晰**：Provider（技术层）负责底层能力，Module（业务层）负责功能逻辑
- **低耦合**：通过依赖注入和事件总线实现模块间解耦
- **高性能**：零分配更新路径、缓存优化、线程安全设计
- **易扩展**：接口抽象、优先级系统、依赖关系自动解析

## ✨ 核心特性

### 1. 模块化架构
- **Module（模块）**：游戏功能/系统/域逻辑的封装
- **Provider（提供者）**：底层技术能力的抽象
- 支持模块依赖关系自动解析和拓扑排序
- 支持模块优先级控制执行顺序

### 2. 生命周期管理
完整的生命周期支持：
```
Init → Enable → Update → Disable → Shutdown → Dispose
```

### 3. 依赖注入
- 内置轻量级依赖注入容器
- 支持单例和瞬态服务注册
- 支持工厂模式注册

### 4. 事件总线
- 类型安全的事件系统
- 支持优先级和帧分片
- 线程安全的事件发布/订阅

### 5. 配置管理
- 统一的配置服务接口
- 支持多种存储后端（如 PlayerPrefs）
- 自动保存/加载配置

### 6. 异步支持
- 基于 UniTask 的异步操作
- 支持取消令牌（CancellationToken）
- 完整的异步生命周期管理

### 7. 异常处理
- 统一的异常处理机制
- 模块异常隔离，不影响其他模块
- 可配置的异常处理器

## 🏗️ 架构设计

### 架构层次

```
┌─────────────────────────────────────┐
│         JulyGameEntry               │  Unity MonoBehaviour 入口
│    (游戏入口，注册模块和Provider)    │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│              GF                     │  静态门面类（统一入口）
│    (提供简洁的上层调用接口)          │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│      FrameworkContext               │  框架上下文（单例）
│  (统一管理所有框架服务)              │
└──────┬───────────┬───────────┬──────┘
       │           │           │
   ┌───▼───┐   ┌───▼───┐   ┌───▼───┐
   │Module │   │Provider│   │Event │
   │Service│   │Service │   │Bus   │
   └───┬───┘   └───┬───┘   └───┬───┘
       │           │           │
   ┌───▼───┐   ┌───▼───┐
   │Module │   │Provider│
   │(业务层)│   │(技术层)│
   └───────┘   └───────┘
```

### 核心组件

#### 1. FrameworkContext（框架上下文）
- 单例模式，统一管理所有框架服务
- 负责框架的初始化和关闭
- 提供依赖注入容器访问

#### 2. GF（静态门面类）
- 提供简洁的静态 API
- 隐藏框架内部实现细节
- 统一的上层调用接口

#### 3. ModuleService（模块服务）
- 管理所有 Module 的注册、初始化、更新、关闭
- 支持模块依赖关系解析
- 支持模块优先级排序
- 线程安全的模块管理

#### 4. ProviderService（提供者服务）
- 管理所有 Provider 的注册和生命周期
- 支持多接口注册（一个 Provider 可实现多个接口）
- 自动识别 Provider 实现的接口

#### 5. EventBus（事件总线）
- 类型安全的事件系统
- 支持优先级和帧分片
- 线程安全的事件发布/订阅

#### 6. DependencyContainer（依赖注入容器）
- 支持单例和瞬态服务注册
- 支持工厂模式注册
- 线程安全的服务解析

#### 7. ConfigService（配置服务）
- 统一的配置管理接口
- 支持多种存储后端
- 自动保存/加载配置

## 🚀 快速开始

### 1. 创建游戏入口

继承 `JulyGameEntry` 并实现必要的抽象方法：

```csharp
using JulyGF.Framework.Core;
using UnityEngine;

public class MyGameEntry : JulyGameEntry
{
    protected override void RegisterProviders()
    {
        // 注册 Provider
        GF.RegisterProvider<JsonDataProvider>();
    }

    protected override void RegisterModules()
    {
        // 注册 Module
        GF.RegisterModule<DataModule>();
        // 注册更多模块...
    }

    protected override void Init()
    {
        // 框架初始化完成后的游戏逻辑初始化
        Debug.Log("游戏初始化完成");
    }
}
```

### 2. 创建 Module

继承 `ModuleBase` 并实现业务逻辑：

```csharp
using JulyGF.Framework.Module.Base;
using System.Threading;
using Cysharp.Threading.Tasks;

public class MyModule : ModuleBase
{
    public override string Name => "MyModule";
    public override int Priority => 100; // 优先级（数值越小优先级越高）

    protected override async UniTask OnInitAsync(CancellationToken cancellationToken)
    {
        // 初始化逻辑
        await UniTask.Delay(100, cancellationToken: cancellationToken);
    }

    protected override async UniTask OnEnableAsync(CancellationToken cancellationToken)
    {
        // 启用逻辑
    }

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        // 每帧更新逻辑
    }

    protected override async UniTask OnDisableAsync(CancellationToken cancellationToken)
    {
        // 禁用逻辑
    }

    protected override async UniTask OnShutdownAsync(CancellationToken cancellationToken)
    {
        // 关闭逻辑
    }
}
```

### 3. 创建 Provider

继承 `ProviderBase` 并实现接口：

```csharp
using JulyGF.Framework.Provider.Base;
using System.Threading;
using Cysharp.Threading.Tasks;

// 定义接口
public interface IMyProvider : IProvider
{
    void DoSomething();
}

// 实现 Provider
public class MyProvider : ProviderBase, IMyProvider
{
    public override string Name => "MyProvider";
    public override int Priority => 0;

    protected override async UniTask OnInitAsync(CancellationToken cancellationToken)
    {
        // 初始化逻辑
    }

    public void DoSomething()
    {
        // 提供的能力
    }
}
```

### 4. 使用 Module 和 Provider

```csharp
// 获取 Module
var dataModule = GF.GetModule<DataModule>();

// 获取 Provider
var myProvider = GF.GetProvider<IMyProvider>();
myProvider.DoSomething();

// 使用事件总线
GF.Subscribe<MyEvent>(OnMyEvent);
GF.Publish(new MyEvent { Data = "test" });
```

## 📚 核心概念

### Module（模块）

Module 是游戏功能的封装单元，代表一个完整的业务功能或系统。

**特点：**
- 继承 `ModuleBase` 或实现 `IModule` 接口
- 拥有完整的生命周期：Init → Enable → Update → Disable → Shutdown
- 可以依赖其他 Module（通过 `IModuleDependency` 接口）
- 支持优先级控制执行顺序

**生命周期：**
1. **InitAsync**：初始化模块，加载资源、注册服务等
2. **EnableAsync**：启用模块，开始工作
3. **Update**：每帧更新（仅已启用的模块）
4. **DisableAsync**：禁用模块，暂停工作
5. **ShutdownAsync**：关闭模块，清理资源

### Provider（提供者）

Provider 是底层技术能力的抽象，为 Module 提供技术支持。

**特点：**
- 继承 `ProviderBase` 或实现 `IProvider` 接口
- 可以实现多个接口（多接口注册）
- 在 Module 之前初始化
- 提供可复用的技术能力

**示例：**
- `IDataProvider`：数据序列化/反序列化
- `INetworkProvider`：网络通信
- `IStorageProvider`：数据存储

### 依赖关系

Module 可以声明对其他 Module 的依赖，框架会自动解析依赖关系并按正确顺序初始化。

```csharp
public class MyModule : ModuleBase, IModuleDependency
{
    public Type[] GetDependencies()
    {
        return new[] { typeof(DataModule) }; // 依赖 DataModule
    }
}
```

### 优先级系统

通过 `Priority` 属性控制模块和提供者的执行顺序：
- 数值越小，优先级越高
- 相同优先级时，按注册顺序执行
- 有依赖关系的模块，依赖优先于优先级

### 事件总线

提供类型安全的事件系统，实现模块间解耦通信。

```csharp
// 定义事件
public class PlayerLevelUpEvent
{
    public int NewLevel { get; set; }
}

// 订阅事件
GF.Subscribe<PlayerLevelUpEvent>(OnPlayerLevelUp);

// 带优先级订阅（数值越小优先级越高）
GF.Subscribe<PlayerLevelUpEvent>(OnHighPriorityHandler, priority: -1);

// 发布事件
GF.Publish(new PlayerLevelUpEvent { NewLevel = 10 });

// 取消订阅
GF.Unsubscribe<PlayerLevelUpEvent>(OnPlayerLevelUp);
```

## 📖 使用指南

### 模块注册

在 `JulyGameEntry.RegisterModules()` 中注册模块：

```csharp
protected override void RegisterModules()
{
    GF.RegisterModule<DataModule>();
    GF.RegisterModule<NetworkModule>();
    GF.RegisterModule<UIModule>();
}
```

### Provider 注册

在 `JulyGameEntry.RegisterProviders()` 中注册 Provider：

```csharp
protected override void RegisterProviders()
{
    GF.RegisterProvider<JsonDataProvider>();
    GF.RegisterProvider<HttpNetworkProvider>();
}
```

### 获取 Module/Provider

```csharp
// 获取 Module
var module = GF.GetModule<DataModule>();

// 尝试获取（不会抛异常）
if (GF.TryGetModule<DataModule>(out var dataModule))
{
    // 使用 dataModule
}

// 获取 Provider
var provider = GF.GetProvider<IDataProvider>();

// 尝试获取
if (GF.TryGetProvider<IDataProvider>(out var dataProvider))
{
    // 使用 dataProvider
}
```

### 配置管理

```csharp
// 设置配置
GF.SetConfig("PlayerName", "John");
GF.SetConfig("Volume", 0.8f);

// 获取配置
var playerName = GF.GetConfig<string>("PlayerName", "Default");
var volume = GF.GetConfig<float>("Volume", 1.0f);

// 保存配置
GF.SaveConfig();
```

### 依赖注入

```csharp
// 注册服务
var container = FrameworkContext.Instance.Container;
container.RegisterSingleton<IMyService, MyService>();

// 解析服务
var service = container.Resolve<IMyService>();

// 尝试解析
if (container.TryResolve<IMyService>(out var myService))
{
    // 使用 myService
}
```

## 💡 最佳实践

### 1. 模块设计

- **单一职责**：每个 Module 只负责一个功能域
- **依赖最小化**：尽量减少模块间的依赖关系
- **接口优先**：通过接口访问其他模块，而非直接依赖实现

### 2. Provider 设计

- **技术抽象**：Provider 应该抽象底层技术细节
- **多接口支持**：一个 Provider 可以实现多个接口，提供多种能力
- **无状态设计**：Provider 应该是无状态的，便于复用

### 3. 生命周期管理

- **InitAsync**：只做必要的初始化，避免耗时操作
- **Update**：保持轻量，避免在 Update 中做耗时操作
- **ShutdownAsync**：确保资源正确释放

### 4. 异常处理

- 模块异常不会影响其他模块
- 使用异常处理器统一处理异常
- 记录详细的异常日志

### 5. 性能优化

- 避免在 Update 中分配内存
- 使用对象池管理频繁创建的对象
- 合理使用缓存减少重复计算

### 6. 事件使用

- 使用强类型事件，避免字符串事件
- 及时取消订阅，避免内存泄漏
- 使用优先级控制处理器执行顺序

## 📖 API 参考

### GF（静态门面类）

#### 框架生命周期
- `InitAsync(CancellationToken)`：初始化框架
- `ShutdownAsync(CancellationToken)`：关闭框架
- `Update()`：更新框架（在 Unity Update 中调用）

#### Module 操作
- `RegisterModule<T>()`：注册模块
- `GetModule<T>()`：获取模块
- `TryGetModule<T>(out T)`：尝试获取模块
- `EnableAllModulesAsync(CancellationToken)`：启用所有模块
- `DisableAllModulesAsync(CancellationToken)`：禁用所有模块

#### Provider 操作
- `RegisterProvider<T>()`：注册 Provider
- `GetProvider<T>()`：获取 Provider
- `TryGetProvider<T>(out T)`：尝试获取 Provider

#### 事件总线
- `Subscribe<T>(Action<T>, object)`：订阅事件
- `Subscribe<T>(Action<T>, object, int)`：订阅事件（带优先级）
- `Unsubscribe<T>(Action<T>)`：取消订阅
- `UnsubscribeAll(object)`：取消指定对象的所有订阅
- `Publish<T>(T)`：发布事件

#### 配置服务
- `GetConfig<T>(string, T)`：获取配置
- `SetConfig<T>(string, T)`：设置配置
- `SaveConfig()`：保存配置

### IModule 接口

```csharp
public interface IModule : IDisposable
{
    string Name { get; }
    bool IsInitialized { get; }
    bool IsEnabled { get; }
    int Priority { get; }
    
    UniTask InitAsync(CancellationToken cancellationToken = default);
    void Update(float elapseSeconds, float realElapseSeconds);
    UniTask EnableAsync(CancellationToken cancellationToken = default);
    UniTask DisableAsync(CancellationToken cancellationToken = default);
    UniTask ShutdownAsync(CancellationToken cancellationToken = default);
}
```

### IProvider 接口

```csharp
public interface IProvider
{
    string Name { get; }
    bool IsInitialized { get; }
    
    UniTask InitAsync(CancellationToken cancellationToken = default);
    UniTask ShutdownAsync(CancellationToken cancellationToken = default);
}
```

### IModuleDependency 接口

```csharp
public interface IModuleDependency
{
    Type[] GetDependencies();
}
```

## 🔧 依赖项

- **Unity**：2020.3 或更高版本
- **UniTask**：已包含在 `3rd/UniTask` 目录中

## 📝 许可证

[在此添加许可证信息]

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📧 联系方式

[在此添加联系方式]

---

**注意**：本文档基于框架当前实现编写，如有更新请及时同步文档。

- 测试小龙虾监控仓库提交是否成功
- 再次测试提交来着
