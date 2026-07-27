using System;

/// <summary>
/// Restricts GameplayTag inspector dropdown to a single domain.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class GameplayTagDomainAttribute : Attribute
{
    public GameplayTagDomain Domain { get; }

    public GameplayTagDomainAttribute(GameplayTagDomain domain)
    {
        Domain = domain;
    }
}
