using NUnit.Framework;
using UnityEngine;

namespace CoffeeBean.Pooling.Tests
{
    /// <summary>CGameObjectPool（Prefab 池）测试（EditMode）：借还 / 复位 / 回调 / 预热 / 上限 / 清空。</summary>
    public class CGameObjectPoolTests
    {
        private sealed class Poolable : MonoBehaviour, IPoolable
        {
            public int SpawnCalls;
            public int DespawnCalls;

            public void OnSpawned() => SpawnCalls++;
            public void OnDespawned() => DespawnCalls++;
        }

        private GameObject _prefab;
        private GameObject _root;
        private CGameObjectPool _pool;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PoolTestPrefab");
            _root = new GameObject("PoolTestRoot");
            _pool = new CGameObjectPool(_prefab, parent: _root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _pool?.Clear();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void Get_CreatesAndActivates()
        {
            GameObject instance = _pool.Get();

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.activeInHierarchy, "借出实例应激活");
            Assert.AreEqual(1, _pool.CountActive);
            Assert.AreEqual(0, _pool.CountInactive);
            Assert.IsNull(instance.transform.parent, "借出实例应脱离池节点（场景根）");
        }

        [Test]
        public void Get_AfterRelease_ReusesSameInstance()
        {
            GameObject first = _pool.Get();
            _pool.Release(first);
            Assert.IsFalse(first.activeInHierarchy, "归还实例应失活");
            Assert.AreEqual(1, _pool.CountInactive);

            GameObject second = _pool.Get();
            Assert.AreSame(first, second, "归还后再次借出应复用同一实例");
            Assert.AreEqual(1, _pool.CountActive);
        }

        [Test]
        public void Get_AppliesPositionAndRotation()
        {
            var pos = new Vector3(1f, 2f, 3f);
            var rot = Quaternion.Euler(0f, 90f, 0f);

            GameObject instance = _pool.Get(pos, rot);

            Assert.AreEqual(pos, instance.transform.position);
            Assert.Less(Quaternion.Angle(rot, instance.transform.rotation), 0.01f,
                "旋转应用应一致（四元数用角度比较，避免浮点逐分量误差）");
        }

        [Test]
        public void Release_ReparentsToPoolRoot()
        {
            GameObject instance = _pool.Get();
            _pool.Release(instance);

            Assert.AreEqual(_root.transform, instance.transform.parent, "归还实例应挂回池节点");
        }

        [Test]
        public void IPoolable_Callbacks_Triggered()
        {
            _prefab.AddComponent<Poolable>();
            var pool = new CGameObjectPool(_prefab, parent: _root.transform);
            try
            {
                GameObject instance = pool.Get();
                // 注意：池返回的是实例（Instantiate 副本），回调计数在实例组件上
                Poolable instancePoolable = instance.GetComponent<Poolable>();
                Assert.AreEqual(1, instancePoolable.SpawnCalls, "借出应触发 OnSpawned");

                pool.Release(instance);
                Assert.AreEqual(1, instancePoolable.DespawnCalls, "归还应触发 OnDespawned");

                // 再次借出同一实例：回调再次触发
                pool.Get();
                Assert.AreEqual(2, instancePoolable.SpawnCalls);
            }
            finally
            {
                pool.Clear();
            }
        }

        [Test]
        public void Prewarm_CreatesExpectedCount()
        {
            var pool = new CGameObjectPool(_prefab, prewarmCount: 4, parent: _root.transform);
            try
            {
                Assert.AreEqual(4, pool.CountInactive);
                GameObject instance = pool.Get();
                Assert.IsTrue(instance.activeInHierarchy, "预热对象借出时应激活");
                Assert.AreEqual(3, pool.CountInactive);
            }
            finally
            {
                pool.Clear();
            }
        }

        [Test]
        public void Release_Overflow_DiscardedFromPool()
        {
            var pool = new CGameObjectPool(_prefab, maxSize: 1, parent: _root.transform);
            try
            {
                GameObject a = pool.Get();
                GameObject b = pool.Get();

                pool.Release(a);
                pool.Release(b); // 空闲已有 1 个 → 超出上限，b 不再被池持有

                Assert.AreEqual(1, pool.CountInactive, "超出上限的归还应被丢弃（不持有）");
                Assert.AreEqual(0, pool.CountActive);
            }
            finally
            {
                pool.Clear();
            }
        }

        [Test]
        public void Release_Null_Ignored()
        {
            Assert.DoesNotThrow(() => _pool.Release(null));
            Assert.AreEqual(0, _pool.CountActive);
        }

        [Test]
        public void Clear_DestroysInactiveOnly()
        {
            GameObject borrowed = _pool.Get();
            GameObject idle = _pool.Get();
            _pool.Release(idle);

            _pool.Clear();

            Assert.AreEqual(0, _pool.CountInactive);
            Assert.AreEqual(1, _pool.CountActive, "已借出的不受 Clear 影响");
            Assert.IsNotNull(borrowed, "已借出实例不应被销毁");
        }

        [Test]
        public void ReleaseDelayed_NonPositiveDelay_Immediate()
        {
            GameObject instance = _pool.Get();
            _pool.ReleaseDelayed(instance, 0f);
            Assert.AreEqual(1, _pool.CountInactive, "delay<=0 应立即归还");
            _pool.ReleaseDelayed(null, 1f); // null 忽略，不抛
        }

        [Test]
        public void ActiveAndPeakCount_TrackCorrectly()
        {
            GameObject a = _pool.Get();
            GameObject b = _pool.Get();
            _pool.Release(a);
            GameObject c = _pool.Get();

            Assert.AreEqual(2, _pool.CountActive);
            Assert.AreEqual(2, _pool.PeakCount);
        }

        [Test]
        public void Constructor_NullPrefab_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new CGameObjectPool(null));
        }
    }
}
