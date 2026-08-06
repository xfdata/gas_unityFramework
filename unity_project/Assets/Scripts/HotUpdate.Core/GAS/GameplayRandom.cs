namespace GAS
{
    /// <summary>
    /// Random operations required by the generic GAS runtime.
    /// Integration layers adapt their simulation random source to this contract.
    /// </summary>
    public interface IGameplayRandom
    {
        int Next(int maxValue);
        int Next(int minValue, int maxValue);
        double NextDouble();
    }
}
