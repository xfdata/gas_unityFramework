using UnityEngine;

namespace GAS
{
    public abstract class GameplayEffectExecution : ScriptableObject
    {
        public abstract void Execute(GameplayEffectSpec spec);
    }
}
