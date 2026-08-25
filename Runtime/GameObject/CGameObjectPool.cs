using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeBean.Pooling
{
    /// <summary>
    /// GameObject / Prefab 对象池（解决 Instantiate/Destroy 高频开销）：
    ///
    /// - <see cref="Get(position, rotation)"/>：空闲优先，不足 Instantiate；激活 + 复位 transform
    ///   + 触发 <see cref="IPoolable.OnSpawned"/>（实例根节点实现了 IPoolable 时）
    /// - <see cref="Release(instance)"/>：失活 + 挂回池节点 + 触发 <see cref="IPoolable.OnDespawned"/>；
    ///   空闲数量达 <see cref="MaxSize"/> 上限时直接 Destroy（不持有）
    /// - <see cref="ReleaseDelayed(instance, seconds)"/>：延迟自动归还（协程，播放模式下生效，
    ///   适合特效 / 弹道等"用完即回"场景）
    /// - <see cref="Prewarm(count)"/> 预热、<see cref="Clear"/> 全部销毁
    ///
    /// 约束：**Unity API 必须主线程调用**；池不跨场景（场景卸载时池实例销毁，需在场景切换前
    /// <see cref="Clear"/> 或重建池；已销毁实例在 Get 时会被跳过并回退工厂）。
    /// 约定：同一实例不得重复 Release。
    /// </summary>
    public sealed class CGameObjectPool : IPool<GameObject>
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;
        private readonly Stack<GameObject> _inactive;

        private int _activeCount;
        private int _peakCount;

        /// <summary>空闲队列容量上限（0 = 不限制）。</summary>
        public int MaxSize => _maxSize;

        public int CountInactive => _inactive.Count;

        public int CountActive => _activeCount;

        public int PeakCount => _peakCount;

        /// <param name="prefab">池化 Prefab（必填）。</param>
        /// <param name="prewarmCount">预热数量（可选）。</param>
        /// <param name="parent">池节点 Transform（null 时自动创建）。</param>
        /// <param name="maxSize">空闲队列上限（0 = 不限制）。</param>
        public CGameObjectPool(GameObject prefab, int prewarmCount = 0, Transform parent = null, int maxSize = 0)
        {
            _prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            _maxSize = Math.Max(0, maxSize);
            _inactive = new Stack<GameObject>(Math.Max(prewarmCount, 4));
            _parent = parent != null ? parent : CreatePoolRoot();

            if (prewarmCount > 0) Prewarm(prewarmCount);
        }

        private Transform CreatePoolRoot()
        {
            var root = new GameObject($"[Pool] {_prefab.name}");
            return root.transform;
        }

        /// <summary>借出（世界原点）。</summary>
        public GameObject Get() => Get(Vector3.zero, Quaternion.identity);

        /// <summary>借出并放置到指定位置 / 旋转：激活 + 复位 + 触发 OnSpawned。</summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance = TakeInactive();
            if (instance == null) instance = UnityEngine.Object.Instantiate(_prefab);

            instance.transform.SetParent(null, false); // 脱离池节点，保持世界坐标
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            _activeCount++;
            if (_activeCount > _peakCount) _peakCount = _activeCount;
            GetPoolable(instance)?.OnSpawned();
            return instance;
        }

        /// <summary>归还：失活 + 挂回池节点 + 触发 OnDespawned；超出上限直接销毁。null 忽略。</summary>
        public void Release(GameObject instance)
        {
            if (instance == null) return; // 幂等（含 Unity 假空：已销毁实例）
            _activeCount--;

            if (_maxSize > 0 && _inactive.Count >= _maxSize)
            {
                DestroyOrImmediate(instance); // 溢出销毁（不持有）
                return;
            }

            GetPoolable(instance)?.OnDespawned();
            instance.SetActive(false);
            instance.transform.SetParent(_parent, false);
            _inactive.Push(instance);
        }

        /// <summary>延迟自动归还（协程；播放模式下生效）。delaySeconds &lt;= 0 时立即归还。</summary>
        public void ReleaseDelayed(GameObject instance, float delaySeconds)
        {
            if (instance == null) return;
            if (delaySeconds <= 0f)
            {
                Release(instance);
                return;
            }
            EnsureRunner().RunDelayed(instance, this, delaySeconds);
        }

        /// <summary>预热：预先实例化 count 个对象进入空闲队列（受 MaxSize 约束），触发 OnDespawned 进入待用态。</summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_maxSize > 0 && _inactive.Count >= _maxSize) break;
                GameObject instance = UnityEngine.Object.Instantiate(_prefab);
                GetPoolable(instance)?.OnDespawned();
                instance.SetActive(false);
                instance.transform.SetParent(_parent, false);
                _inactive.Push(instance);
            }
        }

        /// <summary>清空：销毁全部空闲实例（已借出的不受影响）。</summary>
        public void Clear()
        {
            while (_inactive.Count > 0)
            {
                GameObject instance = _inactive.Pop();
                if (instance != null) DestroyOrImmediate(instance);
            }
        }

        // ========== 内部 ==========

        /// <summary>播放模式用 Destroy（帧末销毁），编辑器 / 测试环境用 DestroyImmediate（立即清理）。</summary>
        private static void DestroyOrImmediate(GameObject instance)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
        }

        /// <summary>取空闲实例；跳过已销毁（Unity 假空）的残留。</summary>
        private GameObject TakeInactive()
        {
            while (_inactive.Count > 0)
            {
                GameObject instance = _inactive.Pop();
                if (instance != null) return instance;
            }
            return null;
        }

        private static IPoolable GetPoolable(GameObject instance) => instance.GetComponent<IPoolable>();

        // ========== 延迟归还协程载体（隐藏单例，DontDestroyOnLoad） ==========

        private static PoolRunner _runner;

        private static PoolRunner EnsureRunner()
        {
            if (_runner == null)
            {
                var go = new GameObject("[CoffeeBean] PoolRunner");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<PoolRunner>();
            }
            return _runner;
        }

        private sealed class PoolRunner : MonoBehaviour
        {
            public void RunDelayed(GameObject instance, CGameObjectPool pool, float delaySeconds)
            {
                StartCoroutine(DelayedReleaseRoutine(instance, pool, delaySeconds));
            }

            private static IEnumerator DelayedReleaseRoutine(GameObject instance, CGameObjectPool pool, float delaySeconds)
            {
                yield return new WaitForSeconds(delaySeconds);
                if (instance != null) pool.Release(instance); // 实例可能已被外部销毁
            }
        }
    }
}
