# JulyGF 框架单元测试

## 📋 目录结构

```
Tests/
├── README.md                          # 测试说明文档
├── Core/                              # 核心服务测试
│   ├── EventBusTests.cs              # 事件总线测试
│   ├── DependencyContainerTests.cs   # 依赖注入容器测试
│   ├── ModuleServiceTests.cs         # 模块服务测试
│   └── ProviderServiceTests.cs       # 提供者服务测试
├── Provider/                          # Provider层测试
│   ├── UI/                           # UI Provider测试
│   │   ├── UIProviderTests.cs       # UI提供者测试
│   │   ├── UIPoolTests.cs           # UI对象池测试
│   │   └── Animation/                # 动画策略测试
│   │       ├── FadeAnimationStrategyTests.cs
│   │       ├── ScaleAnimationStrategyTests.cs
│   │       ├── SlideAnimationStrategyTests.cs
│   │       └── AnimatorAnimationStrategyTests.cs
│   └── Config/                       # Config Provider测试
│       └── ConfigProviderTests.cs
├── Module/                            # Module层测试
│   ├── ConfigModuleTests.cs
│   └── DataModuleTests.cs
└── Utils/                             # 测试工具类
    ├── TestHelpers.cs                # 测试辅助方法
    ├── MockUIBase.cs                  # Mock UI对象
    └── TestEvent.cs                   # 测试事件类
```

## 🧪 测试框架

### 使用的测试框架
- **NUnit 3.x** - 主要测试框架
- **Unity Test Framework** - Unity特定测试支持
- **Moq** (可选) - Mock对象框架

### 安装依赖

在 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.unity.test-framework": "1.1.33",
    "com.unity.nunit": "1.0.0"
  },
  "testables": [
    "com.unity.test-framework"
  ]
}
```

## 🚀 运行测试

### Unity Editor中运行
1. 打开 `Window > Test Runner`
2. 选择 `EditMode` 或 `PlayMode`
3. 点击 `Run All` 或运行单个测试

### 命令行运行
```bash
Unity -runTests -batchmode -projectPath . -testResults TestResults.xml
```

## 📝 测试编写规范

### 1. 测试类命名
- 测试类名：`被测试类名 + Tests`
- 命名空间：`JulyGF.Tests.模块名`

### 2. 测试方法命名
- 格式：`方法名_条件_预期结果`
- 示例：`Subscribe_ValidHandler_ShouldAddToHandlers`

### 3. 测试结构（AAA模式）
```csharp
[Test]
public void MethodName_Condition_ExpectedResult()
{
    // Arrange - 准备测试数据
    var service = new Service();
    
    // Act - 执行测试操作
    var result = service.Method();
    
    // Assert - 验证结果
    Assert.AreEqual(expected, result);
}
```

### 4. 异步测试
```csharp
[UnityTest]
public IEnumerator AsyncMethod_ShouldComplete()
{
    // 使用UnityTest和IEnumerator
    yield return service.AsyncMethod().ToCoroutine();
    Assert.IsTrue(service.IsComplete);
}
```

## 🎯 测试覆盖范围

### Core层（优先级：高）
- ✅ EventBus - 事件订阅/发布/取消订阅
- ✅ DependencyContainer - 服务注册/解析
- ✅ ModuleService - 模块生命周期管理
- ✅ ProviderService - Provider注册/获取

### Provider层（优先级：中）
- ✅ UIProvider - UI打开/关闭/生命周期
- ✅ UIPool - 对象池获取/回收
- ✅ UI动画策略 - 各种动画类型

### Module层（优先级：低）
- ⚠️ ConfigModule - 配置加载/获取
- ⚠️ DataModule - 数据序列化/反序列化

## 📊 测试覆盖率目标

- **核心服务**：> 90%
- **Provider层**：> 80%
- **Module层**：> 70%
- **整体覆盖率**：> 80%

## 🔧 测试工具类

### TestHelpers
提供通用的测试辅助方法：
- `CreateMockUI()` - 创建Mock UI对象
- `WaitForAsync()` - 等待异步操作完成
- `CreateTestEvent()` - 创建测试事件

### Mock对象
- `MockUIBase` - UI基类Mock
- `MockModule` - Module Mock
- `MockProvider` - Provider Mock

## 📌 注意事项

1. **Unity相关测试**：使用 `[UnityTest]` 和 `IEnumerator`
2. **异步测试**：使用 `UniTask.ToCoroutine()` 转换
3. **Mock对象**：避免依赖Unity组件的测试使用Mock
4. **清理资源**：每个测试后清理创建的对象
5. **线程安全**：测试多线程场景时注意同步

## 🔍 持续集成

测试可以在CI/CD流程中自动运行：
- GitHub Actions
- Jenkins
- Unity Cloud Build

