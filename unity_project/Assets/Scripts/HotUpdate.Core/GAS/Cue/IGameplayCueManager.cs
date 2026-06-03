namespace GAS
{
    public interface IGameplayCueManager
    {
        void HandleCue(GameplayTag cueTag, GameplayCueEventType eventType, in GameplayCuePayload payload);
    }
}
