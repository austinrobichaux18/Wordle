namespace InternalWordle
{
    public record GuessResult(bool IsValid, char Letter = ' ', Evaluation Evaluation = Evaluation.Absent, int Position = 0, int Iteration = 0)
    {
    }
}