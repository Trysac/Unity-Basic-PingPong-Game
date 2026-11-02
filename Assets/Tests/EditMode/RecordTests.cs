using NUnit.Framework;

namespace RetroRally.Core.Tests
{
    public class RecordBookTests
    {
        [Test]
        public void WinsBuildAStreakAndTheBestStreakIsKept()
        {
            var store = new MemoryRecordStore();
            var book = new RecordBook(store);

            RecordUpdate first = book.RecordCpuMatch(Difficulty.Normal, won: true, longestRally: 4);
            RecordUpdate second = book.RecordCpuMatch(Difficulty.Hard, won: true, longestRally: 2);

            Assert.That(first.NewBestStreak, Is.True);
            Assert.That(second.NewBestStreak, Is.True);
            Assert.That(book.Streak, Is.EqualTo(2));
            Assert.That(book.BestStreak, Is.EqualTo(2));
            Assert.That(book.Wins(Difficulty.Normal), Is.EqualTo(1));
            Assert.That(book.Wins(Difficulty.Hard), Is.EqualTo(1));
            Assert.That(book.Wins(Difficulty.Easy), Is.EqualTo(0));
            Assert.That(store.SaveCount, Is.EqualTo(2), "every finished match is saved");
        }

        [Test]
        public void ALossEndsTheStreakButNotTheBest()
        {
            var book = new RecordBook(new MemoryRecordStore());
            book.RecordCpuMatch(Difficulty.Easy, true, 0);
            book.RecordCpuMatch(Difficulty.Easy, true, 0);
            RecordUpdate loss = book.RecordCpuMatch(Difficulty.Easy, false, 0);
            RecordUpdate win = book.RecordCpuMatch(Difficulty.Easy, true, 0);

            Assert.That(loss.NewBestStreak, Is.False);
            Assert.That(win.NewBestStreak, Is.False, "a streak of 1 does not beat 2");
            Assert.That(book.Streak, Is.EqualTo(1));
            Assert.That(book.BestStreak, Is.EqualTo(2));
            Assert.That(book.Losses(Difficulty.Easy), Is.EqualTo(1));
            Assert.That(book.Wins(Difficulty.Easy), Is.EqualTo(3));
        }

        [Test]
        public void TheLongestRallyCountsInEveryModeAndOnlyGoesUp()
        {
            var store = new MemoryRecordStore();
            var book = new RecordBook(store);
            Assert.That(book.RecordVersusMatch(12).NewLongestRally, Is.True);
            Assert.That(book.RecordCpuMatch(Difficulty.Hard, false, 7).NewLongestRally, Is.False);
            Assert.That(book.LongestRally, Is.EqualTo(12));
            Assert.That(book.Streak, Is.EqualTo(0));
            Assert.That(book.Wins(Difficulty.Hard) + book.Losses(Difficulty.Normal), Is.EqualTo(0));
            Assert.That(store.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void RecordsSurviveInTheStore()
        {
            var store = new MemoryRecordStore();
            new RecordBook(store).RecordCpuMatch(Difficulty.Hard, true, 9);
            var reloaded = new RecordBook(store);
            Assert.That(reloaded.Wins(Difficulty.Hard), Is.EqualTo(1));
            Assert.That(reloaded.BestStreak, Is.EqualTo(1));
            Assert.That(reloaded.LongestRally, Is.EqualTo(9));
        }
    }

    public class MatchSettingsTests
    {
        [Test]
        public void ChoicesCycleAndWrapAround()
        {
            var settings = new MatchSettings { PointsToWin = 11, Difficulty = Difficulty.Hard, Mode = GameMode.Arcade };
            settings.CyclePoints(1);
            settings.CycleDifficulty(1);
            settings.CycleMode(1);
            Assert.That(settings.PointsToWin, Is.EqualTo(3));
            Assert.That(settings.Difficulty, Is.EqualTo(Difficulty.Easy));
            Assert.That(settings.Mode, Is.EqualTo(GameMode.Classic));

            settings.CyclePoints(-1);
            settings.CycleDifficulty(-1);
            Assert.That(settings.PointsToWin, Is.EqualTo(11));
            Assert.That(settings.Difficulty, Is.EqualTo(Difficulty.Hard));
        }

        [Test]
        public void SavedChoicesLoadBack()
        {
            var store = new MemoryRecordStore();
            new MatchSettings { PointsToWin = 7, Difficulty = Difficulty.Easy, Mode = GameMode.Arcade }.Save(store);
            MatchSettings loaded = MatchSettings.Load(store);
            Assert.That(loaded.PointsToWin, Is.EqualTo(7));
            Assert.That(loaded.Difficulty, Is.EqualTo(Difficulty.Easy));
            Assert.That(loaded.Mode, Is.EqualTo(GameMode.Arcade));
            Assert.That(loaded.Opponent, Is.EqualTo(Opponent.Cpu));
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void BrokenSavedValuesFallBackToTheDefaults()
        {
            var store = new MemoryRecordStore();
            store.SetInt("retrorally.settings.points", 4);
            store.SetInt("retrorally.settings.difficulty", 9);
            store.SetInt("retrorally.settings.mode", -1);
            MatchSettings loaded = MatchSettings.Load(store);
            var defaults = new MatchSettings();
            Assert.That(loaded.PointsToWin, Is.EqualTo(defaults.PointsToWin));
            Assert.That(loaded.Difficulty, Is.EqualTo(defaults.Difficulty));
            Assert.That(loaded.Mode, Is.EqualTo(defaults.Mode));
        }

        [Test]
        public void AMatchUsesACopyOfTheSettings()
        {
            var settings = new MatchSettings { PointsToWin = 3 };
            var sim = new MatchSim(settings, 1);
            settings.PointsToWin = 11;
            Assert.That(sim.Settings.PointsToWin, Is.EqualTo(3));
            Assert.That(sim.Score.PointsToWin, Is.EqualTo(3));
        }
    }
}
