using Common;
using InternalWordle;

public class Program
{
    public static string GoalWord { get; private set; }
    public static async Task Main()
    {
        var gamesPlayed = 0;
        var iterationsToSolve = new List<int>();
        while (true)
        {
            GoalWord = GetGoalWord();
            Console.WriteLine($"Game: {gamesPlayed}. GOAL WORD: " + GoalWord);
            var attempts = new List<string>();
            var isPlaying = true;
            var iteration = 0;
            var algorithm = Algorithm.BasicAlgorithm;
            var allResults = new List<GuessResult>();
            while (isPlaying)
            {
                var guessWord = iteration == 0 ? "house" : GetBestWord(allResults, algorithm);
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
            //await Files.AddHistoryAsync(algorithm, GoalWord, iteration, attempts);
            Console.WriteLine($"WON in {iteration} iterations. Goal word: {GoalWord}");
            iterationsToSolve.Add(iteration);
            gamesPlayed++;
            if (gamesPlayed > 1000)
            {
                var total = 0;
                foreach (var item in iterationsToSolve)
                {
                    total += item;
                }
                var avg = total / gamesPlayed;
            }
        }
    }

    private static void UpdateInformation(List<GuessResult> allResults, List<GuessResult> result) => allResults.AddRange(result);

    private static List<GuessResult> Guess(string guessWord, int iteration)
    {
        var results = new List<GuessResult>();
        if (guessWord == GoalWord)
        {
            results.Add(new GuessResult(true, null));
            return results;
        }
        results.Add(new GuessResult(false, guessWord));
        for (int i = 0; i < guessWord.Length; i++)
        {
            var eval = GetEvaluation(GoalWord, guessWord, i, results);
            results.Add(new GuessResult(false, null, guessWord[i], eval, i, iteration));
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
              && (goalWord.Count(x => x == guessWord[i]) >
                    results.Count(x => x.Letter == guessWord[i] && x.Evaluation != Evaluation.Absent)))
        {
            return Evaluation.Present;
        }
        return Evaluation.Absent;
    }
    private static string GetBestWord(List<GuessResult> allResults, Algorithm algorithm)
    {
        if (algorithm == Algorithm.BasicAlgorithm)
        {
            return FirstAlgorithmGetBestWord(allResults);
        }
        throw new NotImplementedException();
    }

    private static string FirstAlgorithmGetBestWord(List<GuessResult> allResults)
    {
        var words = Files.GetWords();
        var badGuesses = allResults.Where(y => !string.IsNullOrWhiteSpace(y.Guess)).Select(x => x.Guess).ToList();
        words = words.Where(x => !badGuesses.Contains(x)).ToList();

        var corrects = allResults.Where(result => result.Evaluation == Evaluation.Correct).ToList();
        if (corrects.Any())
        {
            words = words.Where(x => corrects.All(result => x[result.Position] == result.Letter)).ToList();
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

        var partialAbsents = absents.Where(x => notAbsents.Any(y => y.Letter == x.Letter));
        //foreach (var item in absents.Where(x => notAbsents.Any(y => y.Letter == x.Letter)))
        //{
        //    if (!partialAbsents.Any(x=> x.Letter == item.Letter && x.Iteration == item.Iteration))
        //    {
        //        partialAbsents.Add(item);
        //    }
        //}
        //TODO: partial absents are fucked 
        if (partialAbsents.Any())
        {
            foreach (var info in partialAbsents)
            {
                var count = allResults.Count(x => x.Letter == info.Letter);
                words = words.Where(word => word.Count(x => x == info.Letter) < count).ToList();
            }
            //foreach (var partialAbsent in partialAbsents)
            //{
            //    var partialLetter = partialAbsent.Letter;
            //    //var allowedAppearanceCount = notAbsents.Where(x => x.Letter == partialLetter )
            //    //                .GroupBy(x => x.Iteration)
            //    //                .OrderByDescending(x => x.Count())
            //    //                .First().Count();
            //    var temp = absents.Where(x => x.Letter == partialLetter);
            //    var temp2 = temp.GroupBy(x => x.Iteration);
            //    var temp3 = temp2.OrderByDescending(x => x.Count());
            //    var temp4 = temp3.First();
            //    var temp5 = temp4.Count();

            //    words = words.Where(x => x.Count(y => y == partialLetter) <= temp5).ToList();
            //}
        }
        return words.First();
    }

    private static string GetGoalWord()
    {
        var words = Files.GetWords();
        //return words[2];
        var index = new Random().Next(words.Count());
        return words[index];
    }
}
