using System;
using System.Threading;
using Cysharp.Threading.Tasks;


    /// <summary>
    /// 一个 GameplayMode 对应一个大玩法上下文
    /// 只有三个核心生命周期：Load → Enter → Exit
    /// 其他需求通过 Context.Events 实时注册实现
    /// </summary>
    public interface IGameplayMode : IDisposable
    {
        GameplayModeId Id { get; }
        UniTask LoadAsync(GameplaySwitchRequest request, CancellationToken token);
        UniTask EnterAsync(GameplaySwitchRequest request, CancellationToken token);
        UniTask ExitAsync(GameplaySwitchRequest nextRequest, CancellationToken token);
    }
