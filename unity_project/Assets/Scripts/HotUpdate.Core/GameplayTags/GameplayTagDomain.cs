/// <summary>
/// Independent GameplayTag libraries. Tags only match within the same domain.
/// Add new values when introducing a new GameplayTagDatabase library.
/// </summary>
public enum GameplayTagDomain : byte
{
    None = 0,
    Global = 1,
    Combat = 2,
}
