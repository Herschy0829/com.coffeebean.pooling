namespace CoffeeBean
{
    /// <summary>
    /// 池化对象生命周期回调（可选实现）：挂载在池化 Prefab 的根节点上，
    /// <see cref="CGameObjectPool"/> 在借出 / 归还时自动调用（零反射，GetComponent 定位）。
    /// </summary>
    public interface IPoolable
    {
        /// <summary>从池借出（激活）时调用：复位状态、绑定事件、播放入场效果等。</summary>
        void OnSpawned();

        /// <summary>归还池（失活）时调用：清理状态、解绑事件、停用计时器等。</summary>
        void OnDespawned();
    }
}
