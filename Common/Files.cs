using Newtonsoft.Json;

namespace Common
{
    public static class Files
    {
        public static string AllWords => "C:\\Users\\arobi\\source\\repos\\Wordle\\Common\\AllWords.json";
        public static string AllFiveLetterWords => "C:\\Users\\arobi\\source\\repos\\Wordle\\Common\\AllFiveLetterWords.json";
        public static string AllNotWordleAccepted => "C:\\Users\\arobi\\source\\repos\\Wordle\\Common\\NotWords.json";
        public static string WordsWon => "C:\\Users\\arobi\\source\\repos\\Wordle\\Common\\WordsWon.json";
        public static string History => "C:\\Users\\arobi\\source\\repos\\Wordle\\Common\\History.json";

        public static List<string> FiveLetterWords { get; set; }
        public static List<string> NotWordleAccepted { get; set; }

        public static async Task AddHistoryAsync(Algorithm Algortihm, string GoalWord, int AttemptCount, List<string> Attempts)
        {
            var text = File.ReadAllText(History);
            var values = JsonConvert.DeserializeObject<List<History>>(text) ?? new List<History>();
            values.Add(new Common.History(Algortihm, GoalWord, AttemptCount, Attempts, DateTimeOffset.Now));
            var newJson = JsonConvert.SerializeObject(values);
            await File.WriteAllTextAsync(History, newJson);
        }

        public static List<string> GetNotAcceptedWordleWords() =>
                NotWordleAccepted = NotWordleAccepted
                    ?? JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(AllNotWordleAccepted)).ToList();
        public static List<string> GetFiveLetterWords() =>
        FiveLetterWords = FiveLetterWords
            ?? JsonConvert.DeserializeObject<List<KeyValue>>(File.ReadAllText(AllFiveLetterWords))
                        .Select(x => x.Key).ToList();
        public static List<string> GetWords() =>
                GetFiveLetterWords().Where(x => !GetNotAcceptedWordleWords().Contains(x)).ToList();
    }
}
