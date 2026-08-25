namespace CoffeeBean.Pooling
{
    /// <summary>
    /// 对象池接口（借 / 还 / 预热 / 清空 + 统计）。
    /// 约定：同一对象不得重复 Release；Get 返回的对象必须归还（或自行处理生命周期）。
    /// </summary>
    public interface IPool<T> where T : class
    {
        /// <summary>借出：空闲队列优先，不足走工厂创建。</summary>
        T Get();

        /// <summary>归还：回空闲队列；超出池容量上限时丢弃（由调用方/GC 处理）。</summary>
        void Release(T item);

        /// <summary>预热：预先创建指定数量对象进入空闲队列（受容量上限约束）。</summary>
        void Prewarm(int count);

        /// <summary>清空空闲队列（已借出的不受影响）。</summary>
        void Clear();

        /// <summary>空闲（可借出）对象数。</summary>
        int CountInactive { get; }

        /// <summary>活跃（已借出未归还）对象数。</summary>
        int CountActive { get; }

        /// <summary>活跃峰值（调试 / 调参用）。</summary>
        int PeakCount { get; }
    }
}
