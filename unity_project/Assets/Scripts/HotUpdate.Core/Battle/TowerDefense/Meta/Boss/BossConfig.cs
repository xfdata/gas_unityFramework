using System;
using GAS;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Boss闃舵瑙﹀彂鏉′欢
    /// </summary>
    public enum EBossPhaseTrigger
    {
        /// <summary>HP浣庝簬鎸囧畾鐧惧垎姣旀椂瑙﹀彂</summary>
        HPThreshold,
        /// <summary>缁忚繃鎸囧畾鏃堕棿鍚庤Е鍙?/summary>
        TimeElapsed,
        /// <summary>琚嚮鏉€鍚庤Е鍙戯紙澶嶆椿闃舵锛?/summary>
        OnDeath,
    }

    /// <summary>
    /// Boss閰嶇疆 ScriptableObject 鈥?瀹氫箟Boss鐨勫闃舵銆佷笓灞炴妧鑳藉拰鐗规畩鏈哄埗銆?
    /// 
    /// Boss 澶嶇敤 TDEnemyActor 浣滀负鍩虹鏁屼汉锛岄€氳繃 BossPhaseComponent 鎵╁睍銆?
    /// 鎶€鑳介€氳繃 GAS (GameplayAbilityDefinition) 椹卞姩銆?
    /// 
    /// 璁捐绾︽潫锛圥hase 7锛夛細
    /// - Boss 蹇呴』澶嶇敤 BattleSkill 绯荤粺
    /// - 涓嶅厑璁稿崟鐙啓 Boss 鎴樻枟妗嗘灦
    /// - Phase 蹇呴』鍙厤缃?
    /// </summary>
    [CreateAssetMenu(fileName = "BossConfig", menuName = "TowerDefense/Boss Config", order = 230)]
    public class BossConfig : ScriptableObject
    {
        [Header("Identity")]
        public string BossId;
        public string DisplayName;

        [Header("Base Config")]
        [Tooltip("Boss 鐨勫熀纭€鏁屼汉閰嶇疆锛堢户鎵?TDEnemyConfig 鐨勫睘鎬э級")]
        public TDEnemyConfig BaseEnemyConfig;

        [Tooltip("Boss鐨凥P鍊嶇巼锛堜箻鍒?BaseEnemyConfig 鐨勬湁鏁圚P涓婏級")]
        public float HpMultiplier = 5f;

        [Header("Phases")]
        [Tooltip("Boss闃舵鍒楄〃锛堟寜椤哄簭鎵ц锛?")]
        public BossPhase[] Phases = Array.Empty<BossPhase>();

        [Header("Special Skills")]
        [Tooltip("Boss鍏ㄥ眬鎶€鑳斤紙鎵€鏈夐樁娈靛彲鐢級")]
        public GameplayAbilityDefinition[] GlobalAbilities = Array.Empty<GameplayAbilityDefinition>();

        [Header("Reward")]
        [Tooltip("鍑绘潃閲戝竵濂栧姳锛堣鐩朆aseEnemyConfig鐨勫嚮鏉€閲戝竵锛?")]
        public int KillGold = 500;

        [Tooltip("鍑绘潃澶╄祴鐐瑰鍔?")]
        public int TalentPointReward = 5;

        /// <summary>鍗曚釜Boss闃舵閰嶇疆</summary>
        [Serializable]
        public class BossPhase
        {
            [Tooltip("闃舵鍚嶇О")]
            public string PhaseName;

            [Tooltip("瑙﹀彂鏉′欢")]
            public EBossPhaseTrigger Trigger;

            [Tooltip("瑙﹀彂鍊硷紙HP% = 0.3 琛ㄧず HP < 30% 鏃惰Е鍙戯級")]
            [Range(0f, 1f)]
            public float TriggerValue = 0.5f;

            [Tooltip("杩涘叆姝ら樁娈垫椂鏂藉姞鐨?GameplayEffect")]
            public GameplayEffectDefinition PhaseEnterEffect;

            [Tooltip("姝ら樁娈垫縺娲荤殑鎶€鑳?")]
            public GameplayAbilityDefinition[] PhaseAbilities = Array.Empty<GameplayAbilityDefinition>();

            [Tooltip("姝ら樁娈电Щ閫熷€嶇巼")]
            public float SpeedMultiplier = 1f;

            [Tooltip("姝ら樁娈典激瀹虫姷鎶?")]
            [Range(0f, 0.9f)]
            public float DamageResist = 0f;

            [Tooltip("姝ら樁娈垫槸鍚﹀厤鐤噺閫?")]
            public bool ImmuneSlow;
        }
    }
}
