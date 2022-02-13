using Common;
using InternalWordle;

public class Program
{
    public static string GoalWord { get; private set; }
    public static async Task Main()
    {
        var gamesPlayed = 0;
        while (true)
        {
            GoalWord = GetGoalWord();
            Console.WriteLine("GOAL WORD: " + GoalWord);
            var attempts = new List<string>();
            var isPlaying = true;
            var iteration = 0;
            var algorithm = Algorithm.MatchOnlyCorrectLetterPosition;
            var allResults = new List<GuessResult>();
            while (isPlaying)
            {
                var guessWord = GetBestWord(allResults, algorithm);
                Console.WriteLine($"Iteration: {iteration}. Guess: " + guessWord);
                attempts.Add(guessWord);
                var result = Guess(guessWord, iteration);
                UpdateInformation(allResults, result);
                if (result.First().IsValid)
                {
                    break;
                }
                iteration++;
            }
            await Files.AddHistoryAsync(algorithm, GoalWord, iteration, attempts);
            Console.WriteLine($"WON in {iteration} iterations. Goal word: {GoalWord}");
            gamesPlayed++;
            if (gamesPlayed > 10)
            {

            }
        }
    }

    private static void UpdateInformation(List<GuessResult> allResults, List<GuessResult> result) => allResults.AddRange(result);

    private static List<GuessResult> Guess(string guessWord, int iteration)
    {
        var results = new List<GuessResult>();
        if (guessWord == GoalWord)
        {
            results.Add(new GuessResult(true));
            return results;
        }
        for (int i = 0; i < guessWord.Length; i++)
        {
            var eval = GetEvaluation(GoalWord, guessWord, i, results);
            results.Add(new GuessResult(false, guessWord[i], eval, i, iteration));
        }
        return results;
    }

    private static Evaluation GetEvaluation(string goalWord, string guessWord, int i, List<GuessResult> results)
    {
        if (goalWord[i] == guessWord[i])
        {
            return Evaluation.Correct;
        }
        else if (goalWord.Any(x => guessWord[i] == x)
              && goalWord.Count(x => x == guessWord[i]) <
                    results.Count(x => x.Letter == guessWord[i] && x.Evaluation != Evaluation.Absent))
        {
            return Evaluation.Present;
        }
        return Evaluation.Absent;
    }
    private static string GetBestWord(List<GuessResult> allResults, Algorithm algorithm)
    {
        if (algorithm == Algorithm.MatchOnlyCorrectLetterPosition)
        {
            return FirstAlgorithmGetBestWord(allResults);
        }
        throw new NotImplementedException();
    }

    private static string FirstAlgorithmGetBestWord(List<GuessResult> allResults)
    {
        var words = Files.GetWords();
        var corrects = allResults.Where(result => result.Evaluation == Evaluation.Correct).ToList();
        if (corrects.Any())
        {
            words = words.Where(x => corrects.Any(result => x[result.Position] == result.Letter)).ToList();
        }
        var presents = allResults.Where(result => result.Evaluation == Evaluation.Present).ToList();
        if (presents.Any())
        {
            words = words.Where(x => presents.Any(result => x[result.Position] != result.Letter)).ToList();
        }
        var notAbsents = allResults.Where(result => result.Evaluation != Evaluation.Absent).ToList();
        var absents = allResults.Where(result => result.Evaluation == Evaluation.Absent).ToList();
        var actuallyAbsent = absents.Where(x => !notAbsents.Any(y => y.Letter == x.Letter)).ToList();
        if (actuallyAbsent.Any())
        {
            words = words.Where(x => actuallyAbsent.All(result => !x.Contains(result.Letter))).ToList();
        }
        var partialAbsents = new List<GuessResult>();
        foreach (var item in absents.Where(x => notAbsents.Any(y => y.Letter == x.Letter)))
        {
            if (!partialAbsents.Any(x=> x.Letter == item.Letter && x.Iteration == item.Iteration))
            {
                partialAbsents.Add(item);
            }
        }
        //TODO: partial absents are fucked 
        if (partialAbsents.Any())
        {
            foreach (var partialAbsent in partialAbsents)
            {
                var partialLetter = partialAbsent.Letter;
                //var allowedAppearanceCount = notAbsents.Where(x => x.Letter == partialLetter )
                //                .GroupBy(x => x.Iteration)
                //                .OrderByDescending(x => x.Count())
                //                .First().Count();
                var temp = absents.Where(x => x.Letter == partialLetter);
                var temp2 = temp.GroupBy(x => x.Iteration);
                var temp3 = temp2.OrderByDescending(x => x.Count());
                var temp4 = temp3.First();
                var temp5 = temp4.Count();

                words = words.Where(x => x.Count(y => y == partialLetter) <= temp5).ToList();
            }
        }
        return words.First();
    }

    private static string GetGoalWord()
    {
        var words = Files.GetWords();
        return words[2];
        //var index = new Random().Next(words.Count());
        //return words[index];
    }
}
