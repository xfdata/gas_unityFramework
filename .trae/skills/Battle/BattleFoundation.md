# BattleFoundation

相关源码：

```text
Assets/Scripts/HotUpdate.Game/BattleFoundation/Core/BattleEngine.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Core/BattleContext.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Core/BattleCore.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Core/BattleSystemManager.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Core/EBattlePhase.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Entity/BattleEntity.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Entity/EntityManager.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Event/BattleEventBus.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Command/BattleCommand.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Config/BattleFoundationConfig.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Rule/BattleRule.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Replay/BattleRecord.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Data/BattleAttributeSet.cs
Assets/Scripts/HotUpdate.Game/BattleFoundation/Utils/BattleRandom.cs
```

BattleFoundation 是战斗核心基础框架，提供引擎循环、实体模型、系统管理、事件总线、命令队列、胜负规则、回放系统、属性系统和确定性随机。

## 1. BattleEngine

`BattleEngine` 是战斗的核心引擎（abstract class），负责整个战斗生命周期。

### 基础属性

```csharp
public abstract class BattleEngine : Disposable
{
    public EBattlePhase Phase { get; }       // 当前阶段
    public BattleContext Context { get; }     // 运行时上下文
    public BattleRuntimeSettings Settings { get; } // 运行时配置
    public float ElapsedTime { get; }         // 累计运行时间
    public float DeltaTime { get; }           // 当前帧时间增量
    public int FrameIndex { get; }            // 帧序号
    public bool IsPaused { get; }             // 是否暂停
    public int RandomSeed { get; }            // 随机种子
    public float TimeScale { get; set; }      // 时间缩放（默认 1）
    public BattleRecorder Recorder { get; }   // 录像器
    public BattlePlayback Playback { get; }   // 回放器
    public IBattleReplayAdapter ReplayAdapter { get; } // 回放适配器
}
```

### EBattlePhase 阶段转换

```csharp
public enum EBattlePhase
{
    Uninitialized,
    Initializing,
    Preloading,
    Ready,
    Running,
    Paused,
    Replaying,
    Ended,
    Disposed,
}
```

转换规则：

```text
Uninitialized -> Initializing -> Ready
Ready -> Preloading / Running / Replaying
Preloading -> Ready
Running -> Paused / Replaying / Ended
Paused -> Running / Replaying / Ended
Replaying -> Ended
Ended -> Replaying
任何 -> Disposed（终态）
```

### EBattleTickMode

```csharp
public enum EBattleTickMode
{
    RealTime,   // 每帧 Tick，deltaTime 为缩放后的 unityDeltaTime
    FrameSync,  // 帧同步模式，按 FrameSyncStep 固定步长 Tick
    TurnBased,  // 回合制，手动调 TickTurn()
}
```

### 初始化流程

```csharp
public virtual void Initialize()
{
    InitializeRuntime(CreateRuntimeSettings());
}

// 子类可重写
protected virtual BattleContext CreateContext() => new BattleContext();
protected virtual BattleRuntimeSettings CreateRuntimeSettings()
{
    return _config != null ? _config.CreateRuntimeSettings() : new BattleRuntimeSettings();
}
protected virtual void OnInitialize() { }
```

`Initialize()` 内部流程：

1. `ResolveSettings(settings)` — RandomSeed=0 时自动生成（Environment.TickCount）。
2. `ChangePhase(EBattlePhase.Initializing)`。
3. `Context = CreateContext()` -> `Context.Initialize(this, settings)`。
4. 如果 `EnableReplay`，创建 `BattleRecorder`。
5. `OnInitialize()`（子类重写）。
6. `ChangePhase(EBattlePhase.Ready)`。

### StartBattle

```csharp
public virtual void StartBattle()
{
    // 仅 Phase == Ready 时有效
    ElapsedTime = 0f; DeltaTime = 0f; FrameIndex = 0; IsPaused = false;
    OnBeforeBattleStart();
    ChangePhase(EBattlePhase.Running);
    Context.Start();
    Recorder?.StartRecording();
    OnBattleStart();
    Recorder?.RecordFrame(FrameRecordData.Create(FrameIndex, ElapsedTime, Context, ReplayAdapter));
}
```

### Tick 循环

```csharp
public void UpdateFromUnity(float unityDeltaTime)
{
    float scaledDeltaTime = Mathf.Max(0f, unityDeltaTime) * Mathf.Max(0f, TimeScale);

    if (Phase == EBattlePhase.Replaying)
    {
        Playback?.Update(scaledDeltaTime);
        return;
    }

    if (Phase != EBattlePhase.Running || IsPaused) return;

    switch (TickMode)
    {
        case EBattleTickMode.FrameSync:
            // 积攒 deltaTime 到 FrameSyncStep 后 TickSimulation
            UpdateFrameSync(scaledDeltaTime);
            break;
        case EBattleTickMode.RealTime:
            TickSimulation(scaledDeltaTime);
            break;
        case EBattleTickMode.TurnBased:
            // 手动调用 TickTurn()
            break;
    }
}

public void TickFixed(float fixedDeltaTime)
{
    if (Phase != EBattlePhase.Running || IsPaused) return;
    TickSimulation(Mathf.Max(0f, fixedDeltaTime));
}

private void TickSimulation(float deltaTime)
{
    DeltaTime = deltaTime;
    ElapsedTime += deltaTime;
    FrameIndex++;
    ExecutePendingCommands();
    Context.Update(deltaTime);
    OnUpdate(deltaTime);       // 子类重写
    Context.LateUpdate(deltaTime);
    OnLateUpdate(deltaTime);   // 子类重写
    CheckEndConditions();      // 遍历 Rules[]
    Recorder?.RecordFrame(FrameRecordData.Create(...));
}
```

### Pause / Resume

```csharp
Pause()  -> IsPaused = true; DeltaTime = 0; ChangePhase(Paused)
Resume() -> IsPaused = false; ChangePhase(Running)
```

### EndBattle

```csharp
public virtual void EndBattle(EBattleResult result)
{
    Recorder?.StopRecording(result);
    Playback?.Stop();
    ChangePhase(EBattlePhase.Ended);
    OnBattleEnd(result);
    OnBattleEnded?.Invoke(this);
}
```

## 2. BattleContext

```csharp
public class BattleContext : Disposable, IBattleContext
{
    public BattleEngine Engine { get; }
    public EntityManager EntityManager { get; }
    public BattleEventBus EventBus { get; }
    public BattleSystemManager SystemManager { get; }
    public BattleRandom Random { get; }

    public T AddSystem<T>(T system) where T : IBattleSystem;
    public T GetSystem<T>() where T : class, IBattleSystem;
    public void Start();       // SystemManager.Start()
    public void Update(float dt);    // SystemManager.Update(dt)
    public void LateUpdate(float dt); // SystemManager.LateUpdate(dt)
}
```

`IBattleContext` 接口：

```csharp
public interface IBattleContext
{
    BattleEngine Engine { get; }
    EntityManager EntityManager { get; }
    BattleEventBus EventBus { get; }
    BattleSystemManager SystemManager { get; }
    BattleRandom Random { get; }
    T AddSystem<T>(T system) where T : IBattleSystem;
    T GetSystem<T>() where T : class, IBattleSystem;
    void Start();
    void Update(float deltaTime);
    void LateUpdate(float deltaTime);
    void Dispose();
}
```

## 3. BattleSystemManager

```csharp
public class BattleSystemManager : Disposable
{
    public IReadOnlyList<IBattleSystem> Systems { get; }

    public void Register<T>(T system) where T : IBattleSystem;
    public void EnsureCanRegister(IBattleSystem system); // 重复注册抛异常
    public T Get<T>() where T : class, IBattleSystem;
    public bool Has<T>() where T : IBattleSystem;
    public void Remove<T>(T system) where T : IBattleSystem;

    public void Start();        // 遍历并启动所有 system
    public void Update(float dt);    // 按注册顺序遍历
    public void LateUpdate(float dt); // 按注册顺序遍历
}
```

注意：

- 同类型系统只能注册一个（GetType() 作为 key）。
- `Get<T>` 先精确匹配 Type，再遍历 `is T`。
- `Start()` 仅调用一次，后续注册的 system 不再自动 Start（除非 `_started` 状态下手动注册，代码中在 Register 里检查 `_started` 后再 Start）。

## 4. IBattleSystem

```csharp
public interface IBattleSystem
{
    void Initialize(IBattleContext context);
    void Start();
    void Update(float deltaTime);
    void LateUpdate(float deltaTime);
    void Dispose();
}
```

## 5. BattleEntity

```csharp
public abstract class BattleEntity : Disposable
{
    public int Id { get; }
    public EEntityCamp Camp { get; }
    public EEntityType EntityType { get; }
    public virtual bool IsAlive { get; set; } = true;
    public virtual Vector3 Position { get; set; }
    public virtual Quaternion Rotation { get; set; }
    public BattleEngine Engine { get; set; }

    public void SetId(int id);
    public void SetCamp(EEntityCamp camp);
    public void SetEntityType(EEntityType type);

    public T AddComponent<T>(T component) where T : EntityComponent;
    public T AddComponent<T>() where T : EntityComponent, new();
    public bool RemoveComponent<T>() where T : EntityComponent;
    public T Get<T>() where T : EntityComponent;
    public bool Has<T>() where T : EntityComponent;
    public IReadOnlyList<EntityComponent> Components { get; }

    // 生命周期
    public virtual void Initialize();      // 遍历 components.Initialize()
    public virtual void Start();           // 遍历 components.Start()
    public virtual void Update(float dt);  // 遍历 components.Update()
    public virtual void LateUpdate(float dt); // 遍历 components.LateUpdate()
    public virtual void Die();

    // 对象池
    public virtual void ActivateForPool(int id, EEntityCamp camp, EEntityType type);
    public virtual void DeactivateForPool();
}
```

### EntityComponent

```csharp
public class EntityComponent : Disposable
{
    public BattleEntity Owner { get; }
    public bool IsActive { get; } = true;

    public virtual void Attach(BattleEntity owner);
    public virtual void Initialize();
    public virtual void Start();
    public virtual void Update(float deltaTime);
    public virtual void LateUpdate(float deltaTime);
    public virtual void ActivateForPool(BattleEntity owner);
    public virtual void DeactivateForPool();
}
```

### EEntityCamp / EEntityType

```csharp
public enum EEntityCamp
{
    None, Ally, Enemy, Neutral,
}

public enum EEntityType
{
    Unknown, Hero, Monster, Boss, Summon, Structure, Projectile,
}
```

## 6. EntityManager

```csharp
public class EntityManager : Disposable
{
    public IReadOnlyList<BattleEntity> All { get; }
    public int Count { get; }

    public int GenerateId();               // 生成不重复的 entityId
    public void AddEntity(BattleEntity entity);
    public void AddEntityFromPool(BattleEntity entity);
    public void RemoveEntity(BattleEntity entity);
    public void RemoveEntityFromPool(BattleEntity entity);

    public BattleEntity GetById(int id);
    public IReadOnlyList<BattleEntity> GetByCamp(EEntityCamp camp);
    public List<BattleEntity> GetAliveByCamp(EEntityCamp camp);
    public int AliveCountByCamp(EEntityCamp camp);

    public List<BattleEntity> FindInRange(Vector3 center, float radius, EEntityCamp camp);
    public BattleEntity FindNearest(Vector3 center, EEntityCamp camp);

    public void Clear();                    // 清空所有 entity
}
```

Entity 添加时会自动发射 `BattleEventIds.EntityCreated` 事件，移除时发射 `BattleEventIds.EntityRemoved`。

## 7. BattleEventBus

```csharp
public class BattleEventBus : Disposable
{
    public void On<T>(int eventId, Action<T> handler);   // 注册
    public void Off<T>(int eventId, Action<T> handler);  // 注销
    public void Emit<T>(int eventId, T data);             // 发射
    public void ClearAll();                               // 清空所有 handler
}
```

预定义事件 ID：

```csharp
public static class BattleEventIds
{
    public const int EntityCreated = 1001;
    public const int EntityRemoved = 1002;
    public const int EntityDied = 1003;
    public const int DamageDealt = 2001;
    public const int Healed = 2002;
    public const int AbilityActivated = 3001;
    public const int BuffApplied = 3002;
    public const int BuffRemoved = 3003;
    public const int CommandExecuted = 4001;
    public const int PhaseChanged = 5001;
    public const int RuleTriggered = 5002;
}
```

同一个 eventId 只能注册一种类型的 handler（payload type 不一致会抛异常）。

## 8. BattleCommand

帧同步命令系统。

```csharp
public abstract class BattleCommand
{
    public int SourceEntityId { get; }
    public int TargetEntityId { get; }
    public int CommandFrame { get; set; }
    public byte CommandType { get; }     // 由子类的 GetCommandTypeId() 返回
    public bool IsConsumed { get; }      // Execute 后变为 true

    public void Execute(BattleEngine engine); // 只执行一次
    protected abstract void OnExecute(BattleEngine engine); // 子类实现
    protected abstract byte GetCommandTypeId();             // 子类实现
    public virtual void Reset();           // 恢复到可复用状态
}

public class CommandQueue
{
    public int Count { get; }
    public void Enqueue(BattleCommand command);
    public bool TryDequeue(out BattleCommand command);
    public void Clear();
}
```

`BattleEngine` 在每帧 `TickSimulation` 开始时调用 `ExecutePendingCommands()` 处理队列中的命令。

## 9. BattleRuntimeSettings / BattleFoundationConfig

```csharp
public class BattleRuntimeSettings
{
    public EBattleTickMode TickMode = EBattleTickMode.RealTime;
    public float FrameSyncStep = 0.033333f;    // 帧同步步长（默认 1/30）
    public float InitialTimeScale = 1f;
    public bool EnableReplay = true;
    public int RandomSeed;

    public BattleRuntimeSettings Clone();
}
```

```csharp
[CreateAssetMenu(menuName = "BattleFoundation/Battle Config")]
public class BattleFoundationConfig : ScriptableObject
{
    public EBattleTickMode TickMode { get; }
    public float FrameSyncStep { get; }
    public bool EnableReplay { get; }
    public float GlobalTimeScale { get; }
    public int RandomSeed { get; }
    public bool EnableEntityPool { get; }
    public int PoolInitialCapacity { get; }
    public bool EnableDebugLog { get; }
    public bool EnableGizmos { get; }

    public BattleRuntimeSettings CreateRuntimeSettings();
}
```

## 10. BattleRule

胜负条件规则。

```csharp
public enum EBattleResult
{
    None, Win, Lose, Draw, Timeout,
}

public abstract class BattleRuleBase : Disposable
{
    public bool IsTriggered { get; }
    public EBattleResult Result { get; }
    public float ElapsedTime { get; }

    public void Initialize(BattleEngine engine);  // 重置状态
    public void Update(float deltaTime);          // 未触发时调 OnUpdate

    protected abstract void OnUpdate(float deltaTime);
    protected void Trigger(EBattleResult result); // 触发胜负条件
    public EBattleResult GetBattleResult();
}
```

预置规则：

- `TimeoutRule(float timeLimit)` — 超时判定（ElapsedTime >= timeLimit 时 Trigger Timeout）。
- `AllEnemiesDeadRule(float checkInterval = 0.5f)` — 判定敌方或友方全灭（Win/Lose）。
- `WinLoseConditionBase` — 条件基类，子类实现 `Evaluate()` 返回 `Condition`。

## 11. BattleRandom

确定性随机数生成器（xorshift 算法）。

```csharp
public class BattleRandom
{
    public BattleRandom(int seed);
    public void SetSeed(int seed);
    public int GetSeed();

    public float Range(float min, float max);  // [min, max)
    public int Range(int min, int max);         // [min, max)
    public int Range(int max);                  // [0, max)
    public float Value { get; }                 // 0~1，单次结果

    public Vector2 InsideUnitCircle();          // 单位圆内随机点
    public Vector3 InsideUnitSphere();          // 单位球内随机点
}
```

种子相同则输出序列完全相同，支持确定性模拟。

## 12. BattleAttribute / BattleAttributeSet

### BattleAttribute

```csharp
public class BattleAttribute : Disposable
{
    public float BaseValue { get; set; }       // 基础值，修改触发 OnChanged
    public float Value { get; }                // 最终值（BaseValue + Modifiers + MinMax clamp）

    public void SetMinMax(float min, float max);
    public void AddModifier(AttributeModifier modifier);
    public bool RemoveModifier(AttributeModifier modifier);
    public int RemoveModifiersFromSource(object source);
    public void ClearModifiers();
}
```

最终值计算顺序：

```csharp
if (hasOverride)
    result = overrideValue + additive;
else
    result = (baseValue + additive) * multiplicative;
// clamp to [min, max]
```

### AttributeModifier

```csharp
public struct AttributeModifier
{
    public EAttributeModifierOp Op;  // Add / Multiply / Override
    public float Value;
    public object Source;            // 来源标记
}
```

`EAttributeModifierOp`:

```csharp
public enum EAttributeModifierOp
{
    Add,       // additive 累加
    Multiply,  // multiplicative *= (1 + value)
    Override,  // result = value，不再使用 baseValue
}
```

### BattleAttributeSet

```csharp
public class BattleAttributeSet : EntityComponent
{
    public void RegisterAttribute(int attrId, float baseValue = 0f);
    public BattleAttribute GetAttribute(int attrId);
    public float GetValue(int attrId);
    public float GetBaseValue(int attrId);
    public void SetBaseValue(int attrId, float value);
    public void AddModifier(int attrId, AttributeModifier modifier);
    public void RemoveModifier(int attrId, AttributeModifier modifier);
    public int RemoveModifiersFromSource(object source);
    public IReadOnlyDictionary<int, BattleAttribute> GetAllAttributes();

    public AttributeSnapshot Snapshot();
    public void RestoreSnapshot(AttributeSnapshot snapshot);
}
```

`AttributeSnapshot`:

```csharp
[Serializable]
public class AttributeSnapshot
{
    public List<int> AttributeIds;
    public List<float> BaseValues;
}
```

### CommonAttributeIds

```csharp
public static class CommonAttributeIds
{
    public const int HP = 1, MaxHP = 2, ATK = 3, DEF = 4;
    public const int MoveSpeed = 5, AttackSpeed = 6, AttackRange = 7;
    public const int CritRate = 8, CritDamage = 9;
    public const int DamageUp1 = 10, DamageUp2 = 11;
    public const int DamageReduce1 = 12, DamageReduce2 = 13, AbsoluteReduce = 14;
}
```

## 13. 回放系统

### BattleRecord

```csharp
[Serializable]
public class BattleRecord
{
    public int BattleId;
    public string BattleType;
    public EBattleTickMode TickMode;
    public float FixedDeltaTime;
    public float TimeScale;
    public int RandomSeed;
    public List<FrameRecordData> Frames;
    public EBattleResult FinalResult;
    public float TotalDuration;
    public int FrameCount { get; }
}
```

### FrameRecordData

```csharp
[Serializable]
public class FrameRecordData
{
    public int FrameIndex;
    public float Timestamp;
    public List<EntitySnapshot> Entities;

    public static FrameRecordData Create(int frameIndex, float timestamp,
        BattleContext context, IBattleReplayAdapter adapter = null);
}
```

### EntitySnapshot

```csharp
[Serializable]
public class EntitySnapshot
{
    public int EntityId;
    public EEntityCamp Camp;
    public EEntityType EntityType;
    public bool IsAlive;
    public string SpawnKey;
    public string PayloadJson;
    public AttributeSnapshot FoundationAttributes;

    // Position/Rotation 以 float 分量存储
    public static EntitySnapshot Capture(BattleEntity entity);
    public void ApplyBaseState(BattleEntity entity);
}
```

### BattleRecorder

```csharp
public class BattleRecorder : Disposable
{
    public bool IsRecording { get; }
    public BattleRecord Record { get; }

    public void Initialize(BattleEngine engine);
    public void StartRecording();                      // 创建新 BattleRecord
    public void RecordFrame(FrameRecordData frame);     // 记录一帧
    public void StopRecording(EBattleResult result);    // 停止并写入结果
    public BattleRecord GetRecord();                    // 获取录像数据
}
```

### BattlePlayback

```csharp
public class BattlePlayback : Disposable
{
    public bool IsPlaying { get; }

    public void Initialize(BattleRecord record, BattleContext context,
        IBattleReplayAdapter adapter, Action<EBattleResult> onCompleted);
    public void Start();
    public void Update(float deltaTime);  // 按 Timestamp 推进帧
    public void Stop();
}
```

回放内部逻辑（`ApplyFrame`）：

1. 遍历 `frame.Entities`，通过 `GetById` 查找已有 Entity，不存在则 `adapter.CreateEntity(snapshot, context)`。
2. 调用 `snapshot.ApplyBaseState(entity)` 恢复位置/旋转/属性。
3. 调用 `adapter.ApplyEntity(entity, snapshot)` 使业务层恢复 GAS 等数据。
4. 不在当前帧中的 Entity 通过 `adapter.RemoveEntity(entity, context)` 或直接 `RemoveEntity` 移除。

### IBattleReplayAdapter

```csharp
public interface IBattleReplayAdapter
{
    void CaptureEntity(BattleEntity entity, EntitySnapshot snapshot);
    BattleEntity CreateEntity(EntitySnapshot snapshot, BattleContext context);
    void ApplyEntity(BattleEntity entity, EntitySnapshot snapshot);
    void RemoveEntity(BattleEntity entity, BattleContext context);
}
```

## 14. 注意事项

- `BattleEngine` 的 `_pendingCommands` 在 `TickSimulation` 开始时执行，执行完成后才运行 `Context.Update`。
- `BattleSystemManager.Update()` 和 `LateUpdate()` 按注册顺序遍历，系统间可依赖顺序（如 PathfindingSystem 在 MovementComponent 之前）。
- `BattleEntity` 的 Component 通过 `GetType()` 去重，同类型只能添加一个。
- `BattleEntity` 支持对象池：`DeactivateForPool()` 重置组件状态，`ActivateForPool(id, camp, type)` 重新激活。
- `EntityManager.Dispose()` 会先遍历 `All` 调用每个 Entity 的 `Dispose()`。
- `BattleEventBus` handler 列表在 Emit 时会被复制到数组再遍历，防止 emit 中修改 handler 导致异常。
- `BattleRandom` 中的 `InsideUnitSphere()` 体积权重，radius 使用 `pow(random01, 1/3)` 保证均匀分布。
- `BattlePlayback` 按 `Timestamp` 推进，不按 FrameIndex 推进。
- `EntitySnapshot` 中的 `SpawnKey` 和 `PayloadJson` 为业务层预留字段。
- `BattleFoundationConfig` 的 `_globalTimeScale` 会被继承到 `BattleRuntimeSettings.InitialTimeScale` 中，支持运行时通过 `BattleEngine.TimeScale` 覆盖。