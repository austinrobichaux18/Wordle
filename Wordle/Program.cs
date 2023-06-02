using Common;
using Microsoft.Playwright;
using Newtonsoft.Json;
using System.Threading.Tasks;

class Program
{
    //todo improve find best word 
    //get ordered list of high freq words

    private static IPage page;
    private static IKeyboard keyboard;
    public static IEnumerable<string> Words { get; set; }
    public static List<string> NotWords { get; set; }
    public static List<string> GoodWords => Words.Except(NotWords).ToList();
    public static List<string> BestWords { get; set; }
    private static List<string> GetBestWords()
    {
        var words = GoodWords;
        var ordered0 = GetOrderedMostCommonLetterPosition(words, 0);
        var ordered1 = GetOrderedMostCommonLetterPosition(words, 1);
        var ordered2 = GetOrderedMostCommonLetterPosition(words, 2);
        var ordered3 = GetOrderedMostCommonLetterPosition(words, 3);
        var ordered4 = GetOrderedMostCommonLetterPosition(words, 4);

        //sares
        var mostCommonWord = ordered0.First().ToString() + ordered1.First().ToString() + ordered2.First().ToString() + ordered3.First().ToString() + ordered4.First().ToString();

        var bestWords = words.ToList();
        var letterCount = ordered0.Count();
        for (int wordPosition = 0; wordPosition < 4; wordPosition++)
        {
            var filteredWords = bestWords.ToList();
            var successIndex = -1;
            var letterIndexCache = 0;
            for (var letterIndex = letterIndexCache; letterIndex < letterCount; letterIndex++)
            {
                var newFilteredWords = new List<string>();
                var orderedList = new List<char>();
                if (wordPosition == 0)
                {
                    orderedList = ordered0.ToList();
                }
                else if (wordPosition == 1)
                {
                    orderedList = ordered1.ToList();
                }
                else if (wordPosition == 2)
                {
                    orderedList = ordered2.ToList();
                }
                else if (wordPosition == 3)
                {
                    orderedList = ordered3.ToList();
                }
                else if (wordPosition == 4)
                {
                    orderedList = ordered4.ToList();
                }

                newFilteredWords = filteredWords.Where(x => x[wordPosition] == orderedList[letterIndex]).ToList();
                letterIndexCache++;
                if (newFilteredWords.Any())
                {
                    filteredWords = newFilteredWords.ToList();
                    successIndex = letterIndex;
                    break;
                }
                if (!newFilteredWords.Any() && letterIndex == letterIndexCache)
                {
                    letterIndex = successIndex + 1;
                    wordPosition--;
                    wordPosition--;
                    break;
                }
            }
            bestWords = filteredWords.ToList();
        }


        return bestWords;
    }
    private static async Task<IEnumerable<string>> GetAcceptableWordsAsync()
    {
        var informations = await ReadAndGetInformationAsync();

        var absents = informations.Where(x => x.IsAbsent && !informations.Any(y => !y.IsAbsent && y.letter == x.letter));
        var absentsAndPresents = informations.Where(x => x.IsAbsent && informations.Any(y => !y.IsAbsent && y.letter == x.letter));

        var words = GoodWords.Where(word =>
                        !word.Any(letter => absents.Select(x => x.letter).Contains(letter))).ToList();

        foreach (var info in absentsAndPresents)
        {
            var count = informations.Count(x => x.letter == info.letter);
            words = words.Where(word => word.Count(x => x == info.letter) < count).ToList();
        }

        words = words.Where(word =>
                    informations.Where(y => y.isThisPosition)
                       .All(infoLetter => word.Contains(infoLetter.letter)
                                       && word[infoLetter.position] == infoLetter.letter)).ToList();

        words = words.Where(word =>
                    informations.Where(y => !y.isThisPosition && !y.IsAbsent)
                      .All(infoLetter => word.Contains(infoLetter.letter)
                                       && word[infoLetter.position] != infoLetter.letter)).ToList();
        return words;
    }


    private static List<char> GetOrderedMostCommonLetterPosition(List<string> words, int n)
    {
        var nthLetters = words.Select(x => x[n]);
        var nthLetterGroup = nthLetters.GroupBy(x => x);
        return nthLetterGroup.OrderByDescending(x => x.Count()).Select(x => x.Key).ToList();
    }

    private static List<Result> Results { get; set; } = new List<Result>();
    //to access class abc -> .abc
    //to access div id=abc -> #abc
    public static async Task Main()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
        });
        page = await browser.NewPageAsync();
        await InitializeAsync();

        //var nextWord = GetBestWords().First();
        var nextWord = "arose";
        while (true)
        {
            await TypeWord(nextWord);
            await ReadAndCacheResultsAsync();
            await RemoveWordIfInvalid();

            var acceptableWords = await GetAcceptableWordsAsync();
            nextWord = acceptableWords.First();
        }
    }

    private static async Task RemoveWordIfInvalid()
    {
        var lastIndex = Results.Select(x => x.Letter).ToList().IndexOf(' ');
        var lastLetter = Results.Where(x => x.Letter != ' ').Last().Letter;

        await keyboard.PressAsync("Backspace");
        await ReadAndCacheResultsAsync();
        var newLastIndex = Results.Select(x => x.Letter).ToList().IndexOf(' ');

        if (lastIndex != newLastIndex)
        {
            var results = Results.Where(x => x.Letter != ' ');
            var count = results.Count();
            var letters = results.Skip(count - 4).Select(x => x.Letter).ToList();
            letters.Add(lastLetter);
            var word = string.Join("", letters);

            if (!NotWords.Any(x => x == word))
            {
                NotWords.Add(word);
            }
            var newJson = JsonConvert.SerializeObject(NotWords);
            await File.WriteAllTextAsync(Files.AllNotWordleAccepted, newJson);
            InitLists();

            await keyboard.PressAsync("Backspace");
            await keyboard.PressAsync("Backspace");
            await keyboard.PressAsync("Backspace");
            await keyboard.PressAsync("Backspace");
            await ReadAndCacheResultsAsync();
        }
    }
    private static async Task<List<Guess>> ReadAndGetInformationAsync()
    {
        var informations = new List<Guess>();
        foreach (var result in Results.Where(x => x.Evaluation == Evaluation.Correct))
        {
            if (!informations.Any(x => x.position % 5 == result.Position % 5))
            {
                informations.Add(new Guess(result.Position % 5, true, result.Letter));
            }
        }
        await CheckIfWinAsync(informations);
        foreach (var result in Results.Where(x => x.Evaluation == Evaluation.Present))
        {
            if (!informations.Any(x => x.position % 5 == result.Position % 5 && x.isThisPosition))
            {
                informations.Add(new Guess(result.Position % 5, false, result.Letter));
            }
        }
        foreach (var result in Results.Where(x => x.Evaluation == Evaluation.Absent))
        {
            if (!informations.Any(x => x.position == -1 && x.letter == result.Letter))
            {
                informations.Add(new Guess(-1, false, result.Letter));
            }
        }

        return informations;
    }


    private static async Task CheckIfWinAsync(List<Guess> nextGuests)
    {
        if (nextGuests.Count() == 5)
        {
            var text = File.ReadAllText(Files.WordsWon);
            var values = JsonConvert.DeserializeObject<List<string>>(text);
            var wonWord = string.Join("", nextGuests.OrderBy(x => x.position).Select(x => x.letter).ToList());
            if (!values.Any(x => x == wonWord))
            {
                values.Add(wonWord);
            }
            var newJson = JsonConvert.SerializeObject(values);
            await File.WriteAllTextAsync(Files.WordsWon, newJson);
        }
    }

    private static void CleanJson()
    {
        var temp = File.ReadAllText(@"AllWords.json");
        var test = JsonConvert.DeserializeObject<List<KeyValue>>(temp);

        var filtered = test.Where(x => x.Key.Length == 5);
        var jsonFiltered = JsonConvert.SerializeObject(filtered);
    }

    private static async Task InitializeAsync()
    {
        keyboard = page.Keyboard;
        await page.GotoAsync("https://www.nytimes.com/games/wordle/index.html");
        await page.GetByTestId("Play").ClickAsync();
        await page.GetByTestId("icon-close").ClickAsync();

        InitLists();
    }

    private static void InitLists()
    {
        var words = File.ReadAllText(Files.AllFiveLetterWords);
        Words = JsonConvert.DeserializeObject<List<KeyValue>>(words).Select(x => x.Key);

        var notWords = File.ReadAllText(Files.AllNotWordleAccepted);
        NotWords = JsonConvert.DeserializeObject<List<string>>(notWords);

        BestWords = GetBestWords();
    }

    private static async Task ReadAndCacheResultsAsync()
    {
        var rows = page.Locator("game-row");
        var row = rows.Locator(".row");
        var tiles = row.Locator("game-tile");

        var gameTilesCount = await tiles.CountAsync();
        for (int j = 0; j < gameTilesCount; j++)
        {
            var gameTile = tiles.Nth(j);
            var letter = await gameTile.GetAttributeAsync("letter");
            var eval = await gameTile.GetAttributeAsync("evaluation");
            var res = Results.FirstOrDefault(x => x.Position == j);
            if (res != null)
            {
                Results.Remove(res);
                Results.Add(new Result(j, letter?.ToCharArray().FirstOrDefault() ?? ' ', eval));
            }
            else
            {
                Results.Add(new Result(j, letter?.ToCharArray().FirstOrDefault() ?? ' ', eval));
            }
        }

    }

    private static async Task TypeWord(string word)
    {
        await page.TypeAsync("#board", word);
        await keyboard.PressAsync("Enter");
    }
}
public record struct Guess(int position, bool isThisPosition, char letter)
{
    public bool IsAbsent => position == -1;
}
public record struct KeyValue(string Key, string Value)
{
}

public class Result
{
    public Result(int i, char letter, string eval)
    {
        Position = i;
        Letter = letter;
        if (string.Equals(eval, nameof(Evaluation.Absent), StringComparison.OrdinalIgnoreCase))
        {
            Evaluation = Evaluation.Absent;
        }
        else if (string.Equals(eval, nameof(Evaluation.Present), StringComparison.OrdinalIgnoreCase))
        {
            Evaluation = Evaluation.Present;
        }
        else if (string.Equals(eval, nameof(Evaluation.Correct), StringComparison.OrdinalIgnoreCase))
        {
            Evaluation = Evaluation.Correct;
        }
    }

    public int Position { get; set; }
    public char Letter { get; set; }
    public Evaluation Evaluation { get; set; }
}
public enum Evaluation
{
    Absent,
    Present,
    Correct
}