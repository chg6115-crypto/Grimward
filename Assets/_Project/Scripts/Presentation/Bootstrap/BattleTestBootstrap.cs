using System.Collections.Generic;
using System.Linq;
using Grimward.Domain.Combat;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using Grimward.Domain.Units;
using Grimward.Presentation.Battle;
using Grimward.Presentation.Data;
using UnityEngine;

namespace Grimward.Presentation.Bootstrap
{
    /// <summary>
    /// M1 전투 테스트 씬의 구성 루트. Inspector에서 파티·편성·밤·시드를 골라 실행한다.
    /// 화면(BattleScreen)은 Hierarchy에 만들어 둔 것을 연결해 쓴다. 「새 전투」는 시드 + 1.
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
            [InspectorName("보스방 (무작위)")] ForestBoss,
            [InspectorName("악마 습격")] DemonRaid,
            [InspectorName("보스방: 홉고블린")] BossHobgoblin,
            [InspectorName("보스방: 버그베어")] BossBugbear,
            [InspectorName("얕은 숲: 소매치기 방 (로스터 C)")] ThiefRoom,
            [InspectorName("얕은 숲: 소매치기 무리 (로스터 D)")] ThiefGang
        }

        [SerializeField] private BattleScreen screen;
        [SerializeField] private int seed = 12345;
        [SerializeField] private PartyPreset party = PartyPreset.FourA;
        [SerializeField] private EncounterPreset encounter = EncounterPreset.ForestNormal;
        [SerializeField] private bool night;
        [SerializeField] private int potions = 3;
        [SerializeField] private int herbs = 2;
        [SerializeField] private int keys = 1;
        [SerializeField] private int torches = 2;
        [SerializeField] private int bandages = 2;
        [Header("자동 진행 — 실행 중 A 키로도 켜고 끔")]
        [Tooltip("켜면 파티도 AI가 조작하고, 전투가 끝나면 다음 전투로 넘어간다. 결과는 Console에 누적")]
        [SerializeField] private bool autoPlay;
        [SerializeField] private AutoFocus autoFocus = AutoFocus.LowestHp;
        [Tooltip("자동 진행에서 이 수만큼 전투하면 멈춤 (0 = 무한)")]
        [SerializeField] private int autoBattleLimit = 0;

        public enum AutoFocus
        {
            [InspectorName("HP 낮은 적 먼저")] LowestHp,
            [InspectorName("큰 적(보스) 먼저")] Biggest,
            [InspectorName("주술사 먼저")] SupportFirst,
            [InspectorName("맨 앞 적")] Front
        }

        [Tooltip("켜면 1번 모험가는 얕은 베임, 2번 모험가는 뇌진탕을 가진 채 시작 (부상 확인용)")]
        [SerializeField] private bool startInjured;

        private GameDatabase db;
        private int runIndex;
        private BattleState current;
        private int played, wins, defeats, fled, roundSum, downSum, deathSum;

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
            [EncounterPreset.BossHobgoblin] = "forest_boss_hobgoblin",
            [EncounterPreset.BossBugbear] = "forest_boss_bugbear",
            [EncounterPreset.ThiefRoom] = "forest_roster_c",
            [EncounterPreset.ThiefGang] = "forest_roster_d",
        };

        /// <summary>에디터 UI 빌더 전용.</summary>
        public void Assign(BattleScreen battleScreen) => screen = battleScreen;

        private void Start()
        {
            Application.targetFrameRate = 60;
            if (screen == null)
            {
                Debug.LogError("BattleTestBootstrap: Screen이 비었습니다. 메뉴 Grimward → 전투 테스트 UI 만들기를 실행하세요.");
                return;
            }
            db = GameDataLoader.Load();
            screen.SetAutoPlay(autoPlay, (AutoPartyAI.Focus)autoFocus);
            NewBattle();
        }

        private void NewBattle()
        {
            Record();
            if (screen.AutoPlay && autoBattleLimit > 0 && played >= autoBattleLimit)
            {
                screen.SetAutoPlay(false, (AutoPartyAI.Focus)autoFocus);
                Debug.Log($"[자동 전투] {autoBattleLimit}전 완료 — 자동 진행을 멈췄습니다.");
            }
            var random = new SeededRandom(seed + runIndex++);
            var heroes = Parties[party].Select((id, i) =>
            {
                var def = db.GetClass(id);
                var a = new Adventurer($"{id}_{i}", def.name, def, db.Balance.defaultPerception, 3);
                a.RestoreFullHp(db.Balance);
                if (startInjured && i == 0) a.Injuries.Add(new InjuryState("shallow_cut"));
                if (startInjured && i == 1) a.Injuries.Add(new InjuryState("concussion"));
                return a;
            }).ToList();

            var inventory = new BattleInventory();
            inventory.Add("potion", potions);
            inventory.Add("herb", herbs);
            inventory.Add("key", keys);
            inventory.Add("torch", torches);
            inventory.Add("bandage", bandages);

            var battle = BattleState.Create(db, random, heroes, Encounters[encounter], night, inventory);
            battle.Start();
            current = battle;
            screen.Show(battle, db, NewBattle, SpriteCatalog.Load());
        }

        /// <summary>끝난 전투 결과를 누적해 Console에 한 줄 남긴다.</summary>
        private void Record()
        {
            if (current == null || current.Phase != BattlePhase.Ended) return;
            var b = current; current = null;
            played++; roundSum += b.Round;
            if (b.Outcome == BattleOutcome.Victory) wins++;
            else if (b.Outcome == BattleOutcome.Defeat) defeats++;
            else fled++;
            int down = b.Log.Count(e => e.Kind == BattleEventKind.Died && e.Target != null && e.Target.Side == BattleSide.Party);
            int dead = b.Log.Count(e => e.Kind == BattleEventKind.KnockedDown && e.Message.Contains(" 사망 ("));
            downSum += down; deathSum += dead;
            string outcome = b.Outcome == BattleOutcome.Victory ? "승리" : b.Outcome == BattleOutcome.Defeat ? "전멸" : "도주";
            Debug.Log($"[자동 전투] {played}전 · 이번 {outcome} {b.Round}R 전투불능 {down} 사망 {dead} │ " +
                      $"누적 승 {wins * 100 / played}% (전멸 {defeats} · 도주 {fled}) · 평균 {roundSum / (float)played:0.0}R · " +
                      $"전투불능 {downSum / (float)played:0.00}명 · 사망 {deathSum / (float)played:0.00}명 │ {party} vs {encounter}, 공략 {autoFocus}");
        }
    }
}
