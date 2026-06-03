namespace GAS
{
    public struct GameplayEffectApplyResult
    {
        public bool Success;
        public bool WasInstant;
        public int RuntimeEffectId;

        public static readonly GameplayEffectApplyResult Failed =
            new GameplayEffectApplyResult { Success = false };

        public static GameplayEffectApplyResult InstantEffect()
        {
            return new GameplayEffectApplyResult
            {
                Success = true,
                WasInstant = true,
                RuntimeEffectId = 0,
            };
        }

        public static GameplayEffectApplyResult ActiveEffect(int runtimeEffectId)
        {
            return new GameplayEffectApplyResult
            {
                Success = true,
                WasInstant = false,
                RuntimeEffectId = runtimeEffectId,
            };
        }
    }
}
