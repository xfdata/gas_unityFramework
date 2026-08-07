namespace GAS
{
    public enum GameplayAbilityActivationFailure
    {
        None,
        InvalidAbility,
        NotGranted,
        Blocked,
        ActivationRequirementsNotMet,
        CostRejected,
        CooldownRejected,
        CommitFailed,
    }

    public readonly struct GameplayAbilityActivationResult
    {
        public readonly GameplayAbilitySpec Spec;
        public readonly GameplayAbilityActivationFailure Failure;

        public bool Success => Failure == GameplayAbilityActivationFailure.None;

        public GameplayAbilityActivationResult(
            GameplayAbilitySpec spec,
            GameplayAbilityActivationFailure failure)
        {
            Spec = spec;
            Failure = failure;
        }

        public static GameplayAbilityActivationResult Failed(GameplayAbilityActivationFailure failure)
        {
            return new GameplayAbilityActivationResult(null, failure);
        }

        public static GameplayAbilityActivationResult Activated(GameplayAbilitySpec spec)
        {
            return new GameplayAbilityActivationResult(spec, GameplayAbilityActivationFailure.None);
        }
    }
}