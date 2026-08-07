using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BattleCommon.Tests
{
    [TestFixture]
    public sealed class BattleGameplayBoundaryTests
    {
        private static readonly string[] ForbiddenBusinessGasTypes =
        {
            "GameplayAbilitySpec",
            "GameplayEffectSpec",
            "GameplayEffectContext",
            "TargetData",
            "ActiveGameplayEffect",
            "GameplayAbilitySpecHandle",
            "ActiveGameplayEffectHandle",
            "GameplayEffectRuntime",
            "GameplayAbilitySystem",
            "GameplayTagContainer",
        };

        [Test]
        public void ApplicationGameplayCode_DoesNotUseRawGasRuntimeTypes()
        {
            var violations = new List<string>();

            foreach (var root in EnumerateApplicationGameplayRoots())
            {
                if (!Directory.Exists(root))
                    continue;

                var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                    CollectViolations(files[i], violations);
            }

            Assert.IsEmpty(
                violations,
                "普通业务必须通过 BattleGameplayFacade 调用 GAS，而不是直接使用底层运行时类型：\n" +
                string.Join("\n", violations));
        }

        private static IEnumerable<string> EnumerateApplicationGameplayRoots()
        {
            var scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            yield return Path.Combine(scriptsRoot, "HotUpdate.Core", "Gameplay");
            yield return Path.Combine(scriptsRoot, "HotUpdate.Game");
        }

        private static void CollectViolations(string filePath, List<string> violations)
        {
            var source = File.ReadAllText(filePath);
            for (int i = 0; i < ForbiddenBusinessGasTypes.Length; i++)
            {
                var typeName = ForbiddenBusinessGasTypes[i];
                if (source.IndexOf(typeName, StringComparison.Ordinal) >= 0)
                    violations.Add($"{filePath}: {typeName}");
            }
        }
    }
}
