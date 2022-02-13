namespace Common
{
    public enum Algorithm
    {
        BasicAlgorithm
    }
    public record History(Algorithm Algortihm, string GoalWord, int AttemptCount, List<string> Attempts, DateTimeOffset Created)
    {
    }
}