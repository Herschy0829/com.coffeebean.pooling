#if COFFEEBEAN_CORE
// CoffeeBean 模块标识 + Core 生命周期集成。
// 本文件所在的 Bridge 程序集仅在安装 Core 时编译（asmdef defineConstraints），
// 因此对象池核心功能不依赖 Core 也能独立工作。
using CoffeeBean;

[assembly: CoffeeBeanModule(
    "com.coffeebean.pooling",
    "0.2.0",
    DisplayName = "Pooling",
    Description = "Object pooling: generic CPool<T> and GameObject pool.",
    Dependencies = new[] { "com.coffeebean.core" }
)]

namespace CoffeeBean
{
    /// <summary>
    /// Core 集成：对象池是泛型 / 实例化服务，无法预注册默认实例，
    /// 业务按需创建（如 CPool&lt;Bullet&gt;、CGameObjectPool(预制体)）。
    /// 本模块标记使 pooling 可被 Core 发现、启停与检查版本兼容。
    /// </summary>
    public sealed class PoolingModule : ICoffeeBeanModule
    {
        public void OnLoad(CoffeeBeanContext context)
        {
            context.Log("CoffeeBean.Pooling integrated (create pools on demand).");
        }

        public void OnStart()
        {
        }

        public void OnShutdown()
        {
        }
    }
}
#endif
