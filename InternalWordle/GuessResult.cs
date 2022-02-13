namespace InternalWordle
{
    public class GuessResult
    {
        public GuessResult(bool IsValid, string Guess, char Letter = ' ', Evaluation Evaluation = Evaluation.Absent, int Position = 0, int Iteration = 0)
        {
            this.IsValid = IsValid;
            this.Guess = Guess;
            this.Letter = Letter;
            this.Evaluation = Evaluation;
            this.Position = Position;
            this.Iteration = Iteration;
        }

        public bool IsValid { get;  }
        public string Guess { get; }
        public char Letter { get; }
        public Evaluation Evaluation { get; set; }
        public int Position { get; }
        public int Iteration { get; }
    }
}