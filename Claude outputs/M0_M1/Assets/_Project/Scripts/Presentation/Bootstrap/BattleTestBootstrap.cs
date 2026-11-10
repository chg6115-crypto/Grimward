using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;
using Grimward.Presentation.Battle;
using Grimward.Presentation.Common;
using Grimward.Presentation.Data;
using TMPro;
using UnityEngine;

namespace Grimward.Presentation.Bootstrap
{
    /// <summary>
    /// M1 전투 테스트 씬의 구성 루트. Inspector에서 파티·편성·밤·시드를 골라 실행한다.
    /// 「새 전투」를 누르면 시드 + 1로 다시 만든다.
    /// </summary>
    public sealed class BattleTestBootstrap : MonoBehaviour
    {
        public enum PartyPreset
        {
            [InspectorName("시작 2인 (검사·마법사)")] StartingDuo,
            [InspectorName("4인 A (검사·도적·마법사·사제)")] FourA,
            [InspectorName("4인 B (검사·도적·궁수·사제)")] FourB
        }

        public enum EncounterPreset
        {
            [InspectorName("튜토리얼 고블린 1~2")] TutorialBattle,
            [InspectorName("약초밭 고블린 3")] TutorialHerb,
            [InspectorName("얕은 숲 일반")] ForestNormal,
            [InspectorName("보스방")] ForestBoss,
            [InspectorName("악마 습격")] DemonRaid
        }

        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private int seed = 12345;
        [SerializeField] private PartyPreset party = PartyPreset.FourA;
        [SerializeField] private EncounterPreset encounter = EncounterPreset.ForestNormal;
        [SerializeField] private bool night;
        [SerializeField] private int potions = 3;
        [SerializeField] private int herbs = 2;

        private GameDatabase db;
        private BattleScreen screen;
        private int runIndex;

        private static readonly Dictionary<PartyPreset, string[]> Parties = new Dictionary<PartyPreset, string[]>
        {
            [PartyPreset.StartingDuo] = new[] { "swordsman", "mage" },
            [PartyPreset.FourA] = new[] { "swordsman", "rogue", "mage", "priest" },
            [PartyPreset.FourB] = new[] { "swordsman", "rogue", "archer", "priest" },
        };

        private static readonly Dictionary<EncounterPreset, string> Encounters = new Dictionary<EncounterPreset, string>
        {
            [EncounterPreset.TutorialBattle] = "tutorial_battle",
            [EncounterPreset.TutorialHerb] = "tutorial_herb",
            [EncounterPreset.ForestNormal] = "forest_normal",
            [EncounterPreset.ForestBoss] = "forest_boss",
            [EncounterPreset.DemonRaid] = "demon_raid",
        };

        private void Start()
        {
            Application.targetFrameRate = 60;
            db = GameDataLoader.Load();
            NewBattle();
        }

        private void NewBattle()
        {
            if (screen != null) Destroy(screen.gameObject);

            var random = new SeededRandom(seed + runIndex++);
            var heroes = Parties[party].Select((id, i) =>
            {
                var def = db.GetClass(id);
                var a = new Adventurer($"{id}_{i}", def.name, def, db.Balance.defaultPerception, 3);
                a.RestoreFullHp(db.Balance);
                return a;
            }).ToList();

            var inventory = new BattleInventory();
            inventory.Add("potion", potions);
            inventory.Add("herb", herbs);

            var battle = BattleState.Create(db, random, heroes, Encounters[encounter], night, inventory);
            battle.Start();

            var go = new GameObject("BattleScreen");
            go.transform.SetParent(transform, false);
            screen = go.AddComponent<BattleScreen>();
            screen.Initialize(battle, db, FontProvider.Resolve(uiFont), NewBattle);
        }
    }
}
