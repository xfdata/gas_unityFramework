namespace BattleCommon
{
    public enum CombatTargetPriority
    {
        Nearest,
        LowestHP,
        HighestHP,
        Random,
    }

    public static class CombatAttributeIds
    {
        public const int HP = 1;
        public const int MaxHP = 2;
        public const int Attack = 3;
        public const int Defense = 4;
        public const int MoveSpeed = 5;
        public const int AttackRange = 6;
        public const int AttackInterval = 7;
        public const int CritRate = 8;
        public const int CritDamage = 9;
        public const int DamageReduce = 10;
        public const int DamageReduce1 = 11;
        public const int DamageReduce2 = 12;
        public const int AbsoluteReduce = 13;
        public const int DamageUp1 = 14;
        public const int DamageUp2 = 15;
    }
}
