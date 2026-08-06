namespace BattleFoundation
{
    /// <summary>
    /// 战斗确定性随机流的契约。
    /// GAS 通过 BattleCommon 适配到 IGameplayRandom，避免依赖 BattleFoundation。
    /// </summary>
    public interface IRandom
    {
        // BF 侧 API
        float Range(float min, float max);
        int Range(int min, int max);
        int Range(int max);
        float Value { get; }
        Float2 InsideUnitCircle();
        Float3 InsideUnitSphere();

        // GAS 侧 API（兼容原 System.Random 调用方）
        int Next(int maxValue);
        int Next(int minValue, int maxValue);
        double NextDouble();
    }
}
