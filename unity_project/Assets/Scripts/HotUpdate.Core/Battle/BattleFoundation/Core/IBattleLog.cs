namespace BattleFoundation
{
    /// <summary>
    /// 战斗日志抽象接口。L0/L1/L2 纯逻辑层统一通过此接口输出日志，
    /// 禁止直接调用 UnityEngine.Debug.Log*。
    /// 默认实现 DefaultBattleLog（依赖 UnityEngine.Debug）由上层注入。
    /// </summary>
    public interface IBattleLog
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
