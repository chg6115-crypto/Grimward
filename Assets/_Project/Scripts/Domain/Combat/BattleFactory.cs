using System.Collections.Generic;
using Grimward.Domain.Characters;
using Grimward.Domain.Config;
using Grimward.Domain.Core;
namespace Grimward.Domain.Combat
{
    public sealed class BattleFactory
    {
        private readonly BalanceConfig config;
        private readonly IRandomSource random;
        private readonly EnemyFactory enemies;
        private readonly Dictionary<int, IReadOnlyList<Combatant>> nodeEnemies = new Dictionary<int, IReadOnlyList<Combatant>>();
        public BattleFactory(BalanceConfig config, IRandomSource random, EnemyFactory enemies)
        {
            this.config = config;
            this.random = random;
            this.enemies = enemies;
        }
        public BattleState Create(Adventurer player, BattleKind kind, int nodeId, bool night)
        {
            IReadOnlyList<Combatant> group;
            if (kind == BattleKind.Ambush) group = enemies.CreateGoblins();
            else if (!nodeEnemies.TryGetValue(nodeId, out group))
            {
                group = enemies.CreateGoblins();
                nodeEnemies.Add(nodeId, group);
            }
            return new BattleState(player.Fighter, group, kind, night, config, random);
        }
    }
}
