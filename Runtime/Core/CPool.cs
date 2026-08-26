using System;
using System.Collections.Generic;

namespace CoffeeBean
{
    /// <summary>
    /// 纯 C# 泛型对象池（无 Unity 依赖，可完整单元测试）。
    ///
    /// - <see cref="Get"/>：空闲栈优先，不足走工厂创建；触发 onGet 回调
    /// - <see cref="Release"/>：归还空闲栈；空闲数量达 <see cref="MaxSize"/> 上限时丢弃
    ///   （不持有，对象生命周期交给调用方 / GC）；触发 onRelease 回调（丢弃时不触发）
    /// - <see cref="Prewarm"/>：预热创建（受 MaxSize 约束），预热对象触发 onRelease 进入"待用态"
    /// - 统计：CountInactive / CountActive / PeakCount
    ///
    /// 线程模型：默认主线程使用；内部无锁，跨线程使用需自行加锁（文档约束）。
    /// 约定：同一对象不得重复 Release。
    /// </summary>
    /// <typeparam name="T">池化对象类型（引用类型）。</typeparam>
    public sealed class CPool<T> : IPool<T> where T : class
    {
        private readonly Func<T> _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly int _maxSize;
        private readonly Stack<T> _inactive;

        private int _activeCount;
        private int _peakCount;

        /// <summary>空闲队列容量上限（0 = 不限制）。</summary>
        public int MaxSize => _maxSize;

        public int CountInactive => _inactive.Count;

        public int CountActive => _activeCount;

        public int PeakCount => _peakCount;

        /// <param name="factory">创建工厂（必填，不可为 null）。</param>
        /// <param name="onGet">借出回调（可选，如复位 / 激活）。</param>
        /// <param name="onRelease">归还回调（可选，如清理 / 失活）。</param>
        /// <param name="prewarmCount">预热数量（可选）。</param>
        /// <param name="maxSize">空闲队列上限（0 = 不限制）。</param>
        public CPool(Func<T> factory, Action<T> onGet = null, Action<T> onRelease = null,
            int prewarmCount = 0, int maxSize = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onGet = onGet;
            _onRelease = onRelease;
            _maxSize = Math.Max(0, maxSize);
            _inactive = new Stack<T>(Math.Max(prewarmCount, 4));

            if (prewarmCount > 0) Prewarm(prewarmCount);
        }

        /// <summary>借出对象：空闲优先，不足走工厂；触发 onGet 回调。</summary>
        public T Get()
        {
            T item = _inactive.Count > 0 ? _inactive.Pop() : _factory();
            _activeCount++;
            if (_activeCount > _peakCount) _peakCount = _activeCount;
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>归还对象：回空闲栈（受 MaxSize 约束）；触发 onRelease 回调。null 忽略。</summary>
        public void Release(T item)
        {
            if (item == null) return; // 幂等：null 忽略
            _activeCount--;
            if (_maxSize > 0 && _inactive.Count >= _maxSize)
            {
                // 溢出丢弃：不持有，不触发 onRelease（对象已不属于池）
                return;
            }
            _onRelease?.Invoke(item);
            _inactive.Push(item);
        }

        /// <summary>预热：预先创建 count 个对象进入空闲队列（受 MaxSize 约束）。</summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_maxSize > 0 && _inactive.Count >= _maxSize) break;
                T item = _factory();
                _onRelease?.Invoke(item); // 预热对象进入"待用态"（与归还一致）
                _inactive.Push(item);
            }
        }

        /// <summary>清空空闲队列（已借出的不受影响）。</summary>
        public void Clear()
        {
            _inactive.Clear();
        }
    }
}
