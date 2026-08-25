# CoffeeBean Pooling（com.coffeebean.pooling）

CoffeeBean 框架的对象池模块：**纯 C# 泛型对象池 + GameObject/Prefab 池**，解决高频创建销毁开销。

独立模块（无依赖），可选集成 CoffeeBean Core（Bridge 条件编译）。

> 设计文档：`docs/design-pooling.md`（v0.1）

## 安装

```json
{
  "dependencies": {
    "com.coffeebean.pooling": "https://github.com/Herschy0829/com.coffeebean.pooling.git#v0.1.0"
  }
}
```

## 快速使用

### 纯 C# 对象池 `CPool<T>`

```csharp
using CoffeeBean.Pooling;

// 工厂 + 借出/归还回调（可选）
var bulletPool = new CPool<Bullet>(
    factory: () => new Bullet(),
    onGet: b => b.Reset(),        // 借出时复位
    onRelease: b => b.Active = false,
    prewarmCount: 10,             // 预热 10 个
    maxSize: 50);                 // 空闲上限 50（0 = 不限制）

Bullet bullet = bulletPool.Get();   // 借出（空闲优先，不足走工厂）
bulletPool.Release(bullet);         // 归还（超上限直接丢弃）
```

### Prefab 池 `CGameObjectPool`

```csharp
var effectPool = new CGameObjectPool(effectPrefab, prewarmCount: 5, maxSize: 20);

GameObject go = effectPool.Get(pos, rot);   // 借出：激活 + 复位 + OnSpawned
effectPool.Release(go);                     // 归还：失活 + 挂回池节点 + OnDespawned
effectPool.ReleaseDelayed(go, 1.5f);        // 延迟自动归还（特效"用完即回"）
```

- 池化 Prefab 根节点实现 `IPoolable`（`OnSpawned` / `OnDespawned`）即可收到生命周期通知

## 约束与约定

- **Unity API（GameObject/Transform）必须主线程**；`CPool<T>` 纯逻辑默认主线程
- 同一对象不得重复 `Release`（重复归还会破坏计数）
- 池**不跨场景**：场景卸载时池内实例销毁，切换场景前建议 `Clear()` 或重建池；已销毁实例在 `Get` 时会被跳过并回退工厂
- `ReleaseDelayed` 依赖协程，**播放模式下生效**

## 目录结构

```
Runtime/
├── Core/        IPool.cs（接口）、CPool.cs（纯 C# 泛型池）
├── GameObject/  CGameObjectPool.cs（Prefab 池）、IPoolable.cs（生命周期回调）
└── Bridge/      与 Core 的可选集成（模块标记，业务按需建池）
```

## 测试

EditMode 测试 21 个：借还复用 / 预热（含上限约束）/ 溢出丢弃 / 回调触发 / 统计 / 清空 /
transform 复位 / 延迟归还参数 / Prefab 池借还守恒。

## 版本约定

- SemVer + git tag `vX.Y.Z`；每个版本对应 GitHub Release（CHANGELOG 派生说明）
