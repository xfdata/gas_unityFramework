using BattleFoundation;
using UnityEngine;

namespace Battle
{
    /// <summary>
    /// IBattleLog 的默认实现，走 UnityEngine.Debug。
    /// 此类依赖 UnityEngine，不进 L0 BattleCore asmdef（noEngineReferences=true），
    /// 留在 Assembly-CSharp 中，由上层（BattleEngine 子类或 Bootstrap）注入到战斗系统。
    /// </summary>
    public class DefaultBattleLog : IBattleLog
    {
        public void Info(string message)
        {
            Debug.Log(message);
        }

        public void Warning(string message)
        {
            Debug.LogWarning(message);
        }

        public void Error(string message)
        {
            Debug.LogError(message);
        }
    }
}
