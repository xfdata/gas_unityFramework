using System;

namespace BattleFoundation
{
    /// <summary>
    /// L0 内部基类，与 Framework.Disposable 行为一致。
    /// L0 asmdef 设 noEngineReferences=true，不能引用 Assembly-CSharp 中的 Framework.Disposable，
    /// 因此 L0 内部自带此基类。Common 目录的 Disposable 保留给 Assembly-CSharp 的其他代码。
    /// </summary>
    public class Disposable : IDisposable
    {
        private bool m_IsDisposed;
        protected virtual void OnDispose() { }
        public bool IsDisposed => m_IsDisposed;
        public void Dispose()
        {
            if (m_IsDisposed)
                return;

            m_IsDisposed = true;
            OnDispose();
        }
    }
}
