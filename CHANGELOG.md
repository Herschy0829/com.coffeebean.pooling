# Changelog

## [0.1.0] - 2025-xx-xx

### Added
- **`CPool<T>` 纯 C# 泛型对象池**：工厂 + 借出/归还回调、预热（受上限约束）、空闲上限溢出即弃、
  活跃/峰值统计、清空；无 Unity 依赖，可完整单元测试
- **`CGameObjectPool` Prefab 池**：借出（激活 + 复位 transform + `IPoolable.OnSpawned`）、
  归还（失活 + 挂回池节点 + `OnDespawned`）、溢出销毁、预热、清空、统计
- **`IPoolable` 生命周期回调**（可选）：池化 MonoBehaviour 实现即可收到借出/归还通知（零反射）
- **`ReleaseDelayed(instance, seconds)`**：协程延迟自动归还（特效 / 弹道"用完即回"）
- **PoolingDemo 示例**：纯对象池借还 + Prefab 池借还 / 延迟归还演示
- Core 可选集成：Bridge 条件编译（模块标记 + 生命周期，业务按需建池）
- EditMode 测试 21 个：借还复用 / 预热 / 溢出 / 回调 / 统计 / 复位 / 延迟归还
