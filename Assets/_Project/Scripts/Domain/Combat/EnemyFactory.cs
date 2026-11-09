using System.Collections.Generic;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
namespace Grimward.Domain.Combat
{
    public sealed class EnemyFactory
    {
        private readonly BalanceConfig config;
        private readonly IRandomSource random;
        public EnemyFactory(BalanceConfig config, IRandomSource random)
        {
            this.config = config;
            this.random = random;
        }
        public IReadOnlyList<Combatant> CreateGoblins()
        {
            int count = random.NextInt(config.GoblinGroupMin, config.GoblinGroupMax);
            var enemies = new List<Combatant>();
            for (int i = 0; i < count; i++)
                enemies.Add(new Combatant(i, $"고블린 {i + 1}", false, config.GoblinHp,
                    config.GoblinAttackMin, config.GoblinAttackMax, config.GoblinAccuracy,
                    config.GoblinEvasion, config.GoblinSpeed, config.GoblinCritChance, config.GoblinArmor));
            return enemies.AsReadOnly();
        }
    }
}
