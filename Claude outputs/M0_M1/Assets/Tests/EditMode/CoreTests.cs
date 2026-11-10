using System.Linq;
using Grimward.Domain.Core;
using Grimward.Domain.Data;
using NUnit.Framework;

namespace Grimward.Tests
{
    public class CoreTests
    {
        [Test]
        public void SeededRandom_SameSeed_SameSequence()
        {
            var a = new SeededRandom(12345);
            var b = new SeededRandom(12345);
            var sa = Enumerable.Range(0, 50).Select(_ => a.NextInt(1, 8)).ToList();
            var sb = Enumerable.Range(0, 50).Select(_ => b.NextInt(1, 8)).ToList();
            CollectionAssert.AreEqual(sa, sb);
        }

        [Test]
        public void SeededRandom_NextInt_InclusiveRange()
        {
            var r = new SeededRandom(7);
            var values = Enumerable.Range(0, 2000).Select(_ => r.NextInt(1, 8)).ToList();
            Assert.That(values.Min(), Is.EqualTo(1));
            Assert.That(values.Max(), Is.EqualTo(8));
        }

        [Test]
        public void SeededRandom_StateRestore_ContinuesSequence()
        {
            var r = new SeededRandom(99);
            r.NextDouble();
            ulong saved = r.State;
            double expected = r.NextDouble();
            var restored = new SeededRandom(1) { State = saved };
            Assert.That(restored.NextDouble(), Is.EqualTo(expected));
        }

        [Test]
        public void GameTime_RestGoesToNextMorning()
        {
            var config = TestData.Balance();
            var t = new GameTime(1, 21 * 60);
            Assert.That(t.IsNight(config), Is.True);
            t.RestUntilNextMorning(config);
            Assert.That(t.Day, Is.EqualTo(2));
            Assert.That(t.MinuteOfDay, Is.EqualTo(360));
            Assert.That(t.IsDay(config), Is.True);
        }

        [Test]
        public void GameTime_AddMinutes_RollsDay()
        {
            var t = new GameTime(1, 23 * 60 + 30);
            t.AddMinutes(60);
            Assert.That(t.Day, Is.EqualTo(2));
            Assert.That(t.MinuteOfDay, Is.EqualTo(30));
        }

        [Test]
        public void GameDatabase_Validate_ReportsMissingSkill()
        {
            var file = TestData.File();
            file.classes[0].skills = new[] { "hit", "nope" };
            var errors = new GameDatabase(file).Validate();
            Assert.That(errors.Any(e => e.Contains("nope")), Is.True);
        }

        [Test]
        public void GameDatabase_Validate_TestDataIsClean()
        {
            CollectionAssert.IsEmpty(TestData.Db().Validate());
        }
    }
}
