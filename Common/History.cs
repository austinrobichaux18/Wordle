namespace Common
{
    public enum Algorithm
    {
        MatchOnlyCorrectLetterPosition
    }
    public record History(Algorithm Algortihm, string GoalWord, int AttemptCount, List<string> Attempts, DateTimeOffset Created)
    {
    }
}