namespace GAS
{
    public interface IGameplayEffectRuntime
    {
        long EntityId { get; }
        GameplayTagContainer OwnedTags { get; }
        IGameplayAttributeOwner AttributeOwner { get; }
    }
}
