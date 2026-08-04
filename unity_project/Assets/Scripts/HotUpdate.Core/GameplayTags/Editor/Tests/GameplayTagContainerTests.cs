#if UNITY_EDITOR
using System;
using NUnit.Framework;

public sealed class GameplayTagContainerTests
{
    [Test]
    public void UnknownTag_ToString_UsesRawFallback()
    {
        var tag = new GameplayTag(GameplayTagDomain.Global, 0xDEADBEEFu, 0xFFFFFFFFu);

        Assert.That(tag.ToString(), Is.EqualTo("Unknown/Global:0xDEADBEEF/0xFFFFFFFF"));
    }

    [Test]
    public void RemoveTags_FromSelf_RemovesEveryDistinctTag()
    {
        var first = new GameplayTag(GameplayTagDomain.Global, 0x01000000u, 0xFF000000u);
        var second = new GameplayTag(GameplayTagDomain.Global, 0x02000000u, 0xFF000000u);
        var container = new GameplayTagContainer(new[] { first, second });

        container.RemoveTags(container);

        Assert.That(container.Count, Is.EqualTo(0));
    }

    [Test]
    public void ListenerRegisteredDuringCallback_WaitsForNextNotification()
    {
        var tag = new GameplayTag(GameplayTagDomain.Global, 0x03000000u, 0xFF000000u);
        var container = new GameplayTagContainer();
        int firstListenerCalls = 0;
        int lateListenerCalls = 0;
        Action<bool> lateListener = _ => lateListenerCalls++;

        container.RegisterListener(tag, _ =>
        {
            firstListenerCalls++;
            container.RegisterListener(tag, lateListener);
        });

        container.AddTag(tag);

        Assert.That(firstListenerCalls, Is.EqualTo(1));
        Assert.That(lateListenerCalls, Is.EqualTo(0));

        container.RemoveTag(tag);

        Assert.That(firstListenerCalls, Is.EqualTo(2));
        Assert.That(lateListenerCalls, Is.EqualTo(1));
    }
}
#endif