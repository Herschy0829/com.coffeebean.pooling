using System;
using System.Collections.Generic;
using CoffeeBean.Pooling;
using UnityEngine;

namespace CoffeeBean.Pooling.Demo
{
    /// <summary>
    /// 对象池示例（场景挂载后运行，IMGUI 演示界面）：
    ///
    /// 1. **纯 C# 对象池（CPool）**：借 / 还 / 预热，观察工厂调用次数与活跃统计
    /// 2. **Prefab 池（CGameObjectPool）**：借出方块、归还、延迟 1 秒自动归还
    /// </summary>
    public sealed class PoolingDemo : MonoBehaviour
    {
        private sealed class DemoItem
        {
            public static int Created;
            public readonly int Id = ++Created;
            public bool InUse;
        }

        // ===== CPool 演示 =====
        private CPool<DemoItem> _itemPool;
        private readonly List<DemoItem> _borrowed = new List<DemoItem>();
        private string _poolStatus = "";

        // ===== CGameObjectPool 演示 =====
        private GameObject _prefab;
        private CGameObjectPool _goPool;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _goPoolStatus = "";

        private void Awake()
        {
            // 纯 C# 对象池：工厂 + 借出/归还回调
            _itemPool = new CPool<DemoItem>(
                factory: () => new DemoItem(),
                onGet: item => { item.InUse = true; },
                onRelease: item => { item.InUse = false; },
                prewarmCount: 3);

            // Prefab 池：动态建一个 Cube 当模板
            _prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _prefab.name = "DemoCube";
            _prefab.SetActive(false); // 模板本身不显示
            _goPool = new CGameObjectPool(_prefab, prewarmCount: 2, maxSize: 8);
        }

        private void OnDestroy()
        {
            _goPool?.Clear();
            if (_prefab != null) Destroy(_prefab);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 480, 400));

            // ===== CPool =====
            GUILayout.Label("<b>CPool&lt;DemoItem&gt;（纯 C# 对象池）</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("借出 Get()", GUILayout.Width(100))) BorrowItem();
            if (GUILayout.Button("归还 Release()", GUILayout.Width(110))) ReleaseItem();
            if (GUILayout.Button("预热 +5", GUILayout.Width(80))) { _itemPool.Prewarm(5); RefreshPoolStatus(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(_poolStatus);
            GUILayout.Space(8);

            // ===== CGameObjectPool =====
            GUILayout.Label("<b>CGameObjectPool（Prefab 池，上限 8）</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("借出 Get()", GUILayout.Width(100))) SpawnCube();
            if (GUILayout.Button("归还 Release()", GUILayout.Width(110))) ReleaseCube();
            if (GUILayout.Button("延迟 1s 归还", GUILayout.Width(100))) DelayReleaseCube();
            GUILayout.EndHorizontal();
            GUILayout.Label(_goPoolStatus);
            GUILayout.Space(8);

            GUILayout.Label("<b>说明</b>");
            GUILayout.Label("- 预热后工厂调用次数不变（对象复用）");
            GUILayout.Label("- 归还后对象失活挂回池节点，再借出时复位");
            GUILayout.Label("- 延迟归还：1 秒后自动回池（特效/弹道用完即回）");

            GUILayout.EndArea();
        }

        // ========== CPool ==========

        private void BorrowItem()
        {
            _borrowed.Add(_itemPool.Get());
            RefreshPoolStatus();
        }

        private void ReleaseItem()
        {
            if (_borrowed.Count == 0) return;
            DemoItem item = _borrowed[_borrowed.Count - 1];
            _borrowed.RemoveAt(_borrowed.Count - 1);
            _itemPool.Release(item);
            RefreshPoolStatus();
        }

        private void RefreshPoolStatus()
            => _poolStatus = $"空闲={_itemPool.CountInactive} 活跃={_itemPool.CountActive} 峰值={_itemPool.PeakCount} 工厂创建={DemoItem.Created}";

        // ========== CGameObjectPool ==========

        private void SpawnCube()
        {
            Vector3 pos = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0.5f, UnityEngine.Random.Range(-2f, 2f));
            _spawned.Add(_goPool.Get(pos, Quaternion.identity));
            RefreshGoPoolStatus();
        }

        private void ReleaseCube()
        {
            if (_spawned.Count == 0) return;
            GameObject go = _spawned[_spawned.Count - 1];
            _spawned.RemoveAt(_spawned.Count - 1);
            _goPool.Release(go);
            RefreshGoPoolStatus();
        }

        private void DelayReleaseCube()
        {
            if (_spawned.Count == 0) return;
            GameObject go = _spawned[_spawned.Count - 1];
            _spawned.RemoveAt(_spawned.Count - 1);
            _goPool.ReleaseDelayed(go, 1f);
            RefreshGoPoolStatus();
        }

        private void RefreshGoPoolStatus()
            => _goPoolStatus = $"空闲={_goPool.CountInactive} 活跃={_goPool.CountActive} 峰值={_goPool.PeakCount} 场上={_spawned.Count}";
    }
}
