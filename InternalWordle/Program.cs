﻿using Common;
using InternalWordle;
using Newtonsoft.Json;

public class Program
{
    public static string GoalWord { get; private set; }
    public static async Task Main()
    {
        var gamesPlayed = 0;
        var iterationsToSolve = new List<int>();
        while (true)
        {
            //GoalWord = "pilar";
            GoalWord = GetGoalWord();

            Console.WriteLine($"Game: {gamesPlayed}. GOAL WORD: " + GoalWord);
            var attempts = new List<string>();
            var isPlaying = true;
            var iteration = 0;
            var algorithm = Algorithm.BasicAlgorithm;
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
            iterationsToSolve.Add(iteration);
            gamesPlayed++;
            if (gamesPlayed > 1000)
            {
                var total = 0;
                foreach (var item in iterationsToSolve)
                {
                    total += item;
                }
                var avg = (double)total / (double)gamesPlayed;
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
        for (int position = 0; position < guessWord.Length; position++)
        {
            var eval = GetEvaluation(GoalWord, guessWord, position, results);
            results.Add(new GuessResult(false, null, guessWord[position], eval, position, iteration));
        }
        foreach (var result in results.Skip(1))
        {
            var letterOccurances = GoalWord.Count(x => x == result.Letter);
            var corrects = results.Count(x => x.Evaluation == Evaluation.Correct && x.Letter == result.Letter);
            var presents = results.Count(x => x.Evaluation == Evaluation.Present && x.Letter == result.Letter);
            var presentsToOverride = (corrects + presents) - letterOccurances;
            if (presentsToOverride > 0)
            {
                for (int i = 0; i < presentsToOverride; i++)
                {
                    var last = results.Last(x => x.Evaluation == Evaluation.Present && x.Letter == result.Letter);
                    last.Evaluation = Evaluation.Absent;
                }
            }
        }
        return results;
    }

    private static Evaluation GetEvaluation(string goalWord, string guessWord, int position, List<GuessResult> results)
    {
        var letterOccurances = goalWord.Count(x => x == guessWord[position]);
        var resultsNotAbsentLetterOccurances = results.Count(x => x.Letter == guessWord[position] && x.Evaluation != Evaluation.Absent);
        if (goalWord[position] == guessWord[position])
        {
            return Evaluation.Correct;
        }
        else if (goalWord.Any(x => guessWord[position] == x)
              && letterOccurances > resultsNotAbsentLetterOccurances)
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
        foreach (var present in presents.ToList())
        {
            //this is when you have 2 of the same letter, both in the wrong spot. One marked as Present and one Absent. 
            //This will send both to presents to ensure it doesnt check the 2 known bad spots already
            var absentOccurances = allResults.Where(x => x.Iteration == present.Iteration && x.Letter == present.Letter && x.Evaluation == Evaluation.Absent);
            foreach (var item in absentOccurances)
            {
                presents.Add(item);
            }
        }

        if (presents.Any())
        {
            words = words.Where(x => presents.All(result => x.Contains(result.Letter) && x[result.Position] != result.Letter)).ToList();
        }

        var notAbsents = allResults.Where(result => result.Evaluation != Evaluation.Absent).ToList();
        var absents = allResults.Where(result => result.Evaluation == Evaluation.Absent).ToList();

        var actuallyAbsent = absents.Where(x => !notAbsents.Any(y => y.Letter == x.Letter)).ToList();
        if (actuallyAbsent.Any())
        {
            words = words.Where(x => actuallyAbsent.All(result => !x.Contains(result.Letter))).ToList();
        }

        var partialAbsents = absents.Where(x => notAbsents.Any(y => y.Letter == x.Letter));
        if (partialAbsents.Any())
        {
            foreach (var info in partialAbsents)
            {
                var count = allResults.Count(x => x.Letter == info.Letter);
                words = words.Where(word => word.Count(x => x == info.Letter) < count).ToList();
            }
        }
        //Can now return words for all acceptable words

        //Order applicable words based on distinct letters to avoid redundant checks
        return words.Select(x => new { score = ScoreWord(x), word = x }).OrderByDescending(x => x.score).Select(x => x.word).First();

    }
    private static double ScoreWord(string word)
    {
        //frequencies go from 1-57 ish. 
        var frequencies = Files.GetLetterFrequency();
        //distinct letters get 100 points
        double score = word.Distinct().Count() * 100;
        foreach (var x in word)
        {
            var temp = frequencies.First(y => y.Key == x.ToString().ToUpper());
            score += double.Parse(temp.Value);
        }
        return score;
    }
    private static string GetGoalWord()
    {
        var words = Files.GetWords();
        var index = new Random().Next(words.Count());
        return words[index];
    }
}
