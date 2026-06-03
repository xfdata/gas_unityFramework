using System;
using System.Collections.Generic;

public sealed class GameplayModeRegistry
{
    private readonly Dictionary<GameplayModeId, Func<GameplayContext, IGameplayMode>> _factories
        = new Dictionary<GameplayModeId, Func<GameplayContext, IGameplayMode>>();

    public void Register(GameplayModeId id, Func<GameplayContext, IGameplayMode> factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[id] = factory;
    }

    public IGameplayMode Create(GameplayModeId id, GameplayContext context)
    {
        if (!_factories.TryGetValue(id, out var factory))
            throw new InvalidOperationException($"GameplayMode not registered: {id}");

        var mode = factory(context);
        if (mode == null)
            throw new InvalidOperationException($"GameplayMode factory returned null: {id}");

        if (mode.Id != id)
            throw new InvalidOperationException($"GameplayMode id mismatch. Request={id}, Actual={mode.Id}");

        return mode;
    }
}
