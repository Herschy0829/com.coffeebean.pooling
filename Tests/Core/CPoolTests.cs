using System;
using NUnit.Framework;

namespace CoffeeBean.Pooling.Tests
{
    /// <summary>CPool（纯 C# 对象池）测试：借还 / 预热 / 上限 / 回调 / 统计。</summary>
    public class CPoolTests
    {
        private sealed class Item
        {
            public static int Created;
            public bool InUse;
            public Item() => Created++;
        }

        private CPool<Item> _pool;
        private int _getCalls;
        private int _releaseCalls;

        [SetUp]
        public void SetUp()
        {
            Item.Created = 0;
            _getCalls = 0;
            _releaseCalls = 0;
            _pool = new CPool<Item>(
                factory: () => new Item(),
                onGet: item => { _getCalls++; item.InUse = true; },
                onRelease: item => { _releaseCalls++; item.InUse = false; });
        }

        [Test]
        public void Get_CreatesFromFactory()
        {
            Item item = _pool.Get();
            Assert.IsNotNull(item);
            Assert.AreEqual(1, Item.Created);
            Assert.AreEqual(1, _pool.CountActive);
            Assert.IsTrue(item.InUse, "借出应触发 onGet 回调");
            Assert.AreEqual(1, _getCalls);
        }

        [Test]
        public void Get_AfterRelease_ReusesInstance()
        {
            Item first = _pool.Get();
            _pool.Release(first);

            Item second = _pool.Get();

            Assert.AreSame(first, second, "归还后再次借出应复用同一实例（工厂不再调用）");
            Assert.AreEqual(1, Item.Created);
            Assert.AreEqual(1, _pool.CountActive);
            Assert.AreEqual(0, _pool.CountInactive);
        }

        [Test]
        public void Release_TriggersOnRelease()
        {
            Item item = _pool.Get();
            _pool.Release(item);

            Assert.AreEqual(1, _releaseCalls);
            Assert.IsFalse(item.InUse, "归还应触发 onRelease 回调");
            Assert.AreEqual(1, _pool.CountInactive);
            Assert.AreEqual(0, _pool.CountActive);
        }

        [Test]
        public void Release_Null_Ignored()
        {
            Assert.DoesNotThrow(() => _pool.Release(null));
            Assert.AreEqual(0, _pool.CountActive, "null 归还应忽略（不影响计数）");
        }

        [Test]
        public void Prewarm_CreatesExpectedCount()
        {
            var pool = new CPool<Item>(() => new Item(), prewarmCount: 5);
            Assert.AreEqual(5, pool.CountInactive);
            Assert.AreEqual(5, Item.Created);

            Item item = pool.Get();
            Assert.AreEqual(4, pool.CountInactive);
            Assert.AreEqual(5, Item.Created, "预热后借出不应再调工厂");
            Assert.AreEqual(0, _getCalls, "预热不触发 onGet");
        }

        [Test]
        public void Prewarm_RespectsMaxSize()
        {
            var pool = new CPool<Item>(() => new Item(), prewarmCount: 5, maxSize: 3);
            Assert.AreEqual(3, pool.CountInactive, "预热数量受空闲上限约束");
            Assert.AreEqual(3, Item.Created);
        }

        [Test]
        public void Release_Overflow_DiscardsBeyondMaxSize()
        {
            var pool = new CPool<Item>(() => new Item(), maxSize: 2);
            Item a = pool.Get();
            Item b = pool.Get();
            Item c = pool.Get();
            Assert.AreEqual(3, Item.Created);

            pool.Release(a);
            pool.Release(b);
            pool.Release(c); // 空闲已有 2 个 → 第 3 个被丢弃

            Assert.AreEqual(2, pool.CountInactive, "超上限的归还应被丢弃");
            Assert.AreEqual(0, pool.CountActive);

            Item next = pool.Get();
            Assert.AreEqual(3, Item.Created, "池中仍有空闲，不应新建");
            Assert.IsTrue(ReferenceEquals(next, a) || ReferenceEquals(next, b),
                "next 应复用池中空闲对象（被丢弃的是第三个）");
        }

        [Test]
        public void ActiveAndPeakCount_TrackCorrectly()
        {
            Item a = _pool.Get();
            Item b = _pool.Get();
            _pool.Release(a);
            Item c = _pool.Get(); // 复用 a：活跃仍为 2，峰值保持 2

            Assert.AreEqual(2, _pool.CountActive);
            Assert.AreEqual(2, _pool.PeakCount, "峰值 = 同时活跃的最大值");
            Assert.AreEqual(0, _pool.CountInactive, "c 复用了 a，空闲已空");
            Assert.AreSame(a, c, "归还后复用同一实例");
        }

        [Test]
        public void Clear_EmptiesInactive_ActiveUntouched()
        {
            Item a = _pool.Get();
            Item b = _pool.Get();
            _pool.Release(a);

            _pool.Clear();

            Assert.AreEqual(0, _pool.CountInactive);
            Assert.AreEqual(1, _pool.CountActive, "已借出的不受 Clear 影响");
        }

        [Test]
        public void Constructor_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CPool<Item>(null));
        }

        [Test]
        public void ImplementsIPoolContract()
        {
            IPool<Item> asInterface = _pool;
            Item item = asInterface.Get();
            asInterface.Release(item);
            Assert.AreEqual(1, asInterface.CountInactive);
        }
    }
}
