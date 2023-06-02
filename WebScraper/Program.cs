using Microsoft.Playwright;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;

public class Program
{
    private static IBrowser browser;

    public static async Task Main()
    {
        //await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        //{
        //    Headless = false,
        //});

        using var playwright = await Playwright.CreateAsync();
        browser = await playwright.Webkit.LaunchAsync();

        var urls = JsonConvert.DeserializeObject<List<string>>(await File.ReadAllTextAsync("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Urls.json"));
        var results = new List<Result>();

        var batchSize = 2;
        for (int i = 0; i < urls.ToList().Count() / batchSize; i++)
        {
            var tasks = new List<Task>();
            for (int j = 0; j < batchSize; j++)
            {
                await GetResultsAsync(null, urls, results, i * batchSize + j);
                //tasks.Add(GetResultsAsync(null, urls, results, i * batchSize + j));
            }
            await Task.WhenAll(tasks);
            if (i > 0 && i % 10 == 0)
            {
                File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json", JsonConvert.SerializeObject(results));
                File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Urls.json", JsonConvert.SerializeObject(urls));
            }
        }
        File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json", JsonConvert.SerializeObject(results));
        File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Urls.json", JsonConvert.SerializeObject(urls));
    }

    private static string GetTime(Time lastTime, double thisTime)
    {
        var thisTimeSeconds = Math.Round(((double)((double)thisTime / (double)1000)), 2);
        if (lastTime.time == null)
        {
            lastTime.time = thisTimeSeconds;
            return $"---This: (0). Total: ({thisTimeSeconds})";
        }
        var result = Math.Round((double)(thisTimeSeconds - lastTime.time), 2);
        lastTime.time = thisTimeSeconds;
        return $" ---This: ({result}). Total: ({thisTimeSeconds})";
    }

    private static async Task GetResultsAsync(IPage page, List<string> urls, List<Result> results, int index)
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine();

            var timer = new Stopwatch();
            timer.Start();
            var time = new Time();

            Console.WriteLine($"Getting Browser ({index})" + GetTime(time, timer.ElapsedMilliseconds));
            page = await browser.NewPageAsync();
            Console.WriteLine($"Got Browser ({index})" + GetTime(time, timer.ElapsedMilliseconds));
            Console.WriteLine();

            Console.WriteLine($"Going to page ({index}) ({urls[index]})" + GetTime(time, timer.ElapsedMilliseconds));
            await page.GotoAsync(urls[index]);
            Console.WriteLine($"Got to page ({index}) ({urls[index]})" + GetTime(time, timer.ElapsedMilliseconds));
            Console.WriteLine();

            var result = new Result();
            result.Url = page.Url;

            Console.WriteLine($"Getting Title ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            var title = (await page.TitleAsync()).Replace("- Food.com", "");
            Console.WriteLine($"Got Title ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            Console.WriteLine();

            result.Title = title.Replace("Recipe", "").Trim();

            var item = new Item();
            await GetInstructionsAsync(page, item, index, time, timer);
            await GetIngredientsAsync(page, item, index, time, timer);
            result.Items.Add(item);
            await GetImagesAsync(page, result, index, time, timer);
            results.Add(result);

            await GetOtherRecipeUrlsAsync(page, urls, index, time, timer);

            //if (results.Count > 5)
            //{
            //    File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json", JsonConvert.SerializeObject(results));
            //    File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Urls.json", JsonConvert.SerializeObject(urls));
            //    return;
            //}
            timer.Stop();
            Console.WriteLine($"Done ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            await page.CloseAsync();
            //await GetResultsAsync(page, urls, results, index + 1);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Url ({index}): " + urls[index]);
            Console.WriteLine(e.Message);
            Console.WriteLine();
        }
    }

    private static async Task GetOtherRecipeUrlsAsync(IPage page, List<string> urls, int index, Time time, Stopwatch timer)
    {
        Console.WriteLine($"Getting RecipeUrls ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        var aLocator = page.Locator("a");
        for (int i = 0; i < await aLocator.CountAsync(); i++)
        {
            var recipeUrl = await aLocator.Nth(i).GetAttributeAsync("href");
            if (recipeUrl.StartsWith("https") && !recipeUrl.Contains("ideas") && !recipeUrl.Contains("user") && recipeUrl.Contains("recipe") && !urls.Contains(recipeUrl))
            {
                urls.Add(recipeUrl);
            }
        }
        Console.WriteLine($"Got RecipeUrls ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        Console.WriteLine();
    }

    private static async Task GetImagesAsync(IPage page, Result result, int index, Time time, Stopwatch timer)
    {
        Console.WriteLine($"Getting Images ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        var imgLocator = page.Locator("img");
        for (int i = 0; i < await imgLocator.CountAsync(); i++)
        {
            var imgUrl = await imgLocator.Nth(i).GetAttributeAsync("src");
            if (imgUrl.Contains("avatar") || imgUrl.Contains("promo") || imgUrl.Contains("yahoo") || imgUrl.Contains("facebook") || imgUrl.Contains("google") || imgUrl.Contains("static"))
            {
                continue;
            }
            if (imgUrl.Contains("ar_"))
            {
                if (!result.Images.Any(x => x.Contains("v1/img") && imgUrl.Contains("v1/img") && x.Substring(x.IndexOf("v1/img")) == imgUrl.Substring(x.IndexOf("v1/img"))))
                {
                    result.Images.Add(imgUrl);
                }
            }
            else
            {
                if (!result.Images.Contains(imgUrl))
                {
                    result.Images.Add(imgUrl);
                }
            }
        }
        Console.WriteLine($"Got Images({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        Console.WriteLine();
    }

    private static async Task GetInstructionsAsync(IPage page, Item item, int index, Time time, Stopwatch timer)
    {
        Console.WriteLine($"Getting Instructions ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        var instructionsElement = await page.WaitForSelectorAsync(".direction-list");
        var instructions = await instructionsElement.InnerTextAsync();
        Console.WriteLine($"Got Instructions ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        Console.WriteLine();
        item.Instructions = instructions.Split("\n").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Replace("\r", "")).ToList();
    }

    private static async Task GetIngredientsAsync(IPage page, Item item, int index, Time time, Stopwatch timer)
    {
        Console.WriteLine($"Getting Ingredients ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        var ingredientElement = await page.WaitForSelectorAsync(".ingredient-list");
        var ingredients = await ingredientElement.InnerTextAsync();
        Console.WriteLine($"Got Ingredients ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        Console.WriteLine();
        var split = ingredients.Split("\n").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        for (int i = 0; i < split.Count / 2; i++)
        {
            item.Ingredients.Add(new DoubleString());
        }
        if (split.Count % 2 == 1)
        {
            item.Ingredients.Add(new DoubleString());
        }

        for (int i = 0; i < split.Count; i++)
        {
            var temp = item.Ingredients.First(x => string.IsNullOrWhiteSpace(x.One) || string.IsNullOrWhiteSpace(x.Two));
            if (string.IsNullOrWhiteSpace(temp.One) && split.Count != i + 1)
            {
                temp.One = split[i];
            }
            else
            {
                temp.Two = split[i];
            }
        }

        for (int i = 0; i < split.Count / 2; i++)
        {
            {
                var innerSplit = item.Ingredients[i].Two.Split(" ").ToList();
                if (innerSplit.Count == 1)
                {
                    continue;
                }

                item.Ingredients[i].One += " " + innerSplit[0];
                innerSplit.RemoveAt(0);
                item.Ingredients[i].Two = string.Join(" ", innerSplit);
            }

            while (item.Ingredients[i].One.Contains("(") && !item.Ingredients[i].One.Contains(")"))
            {
                var innerSplit = item.Ingredients[i].Two.Split(" ").ToList();
                item.Ingredients[i].One += " " + innerSplit[0];
                innerSplit.RemoveAt(0);
                item.Ingredients[i].Two = string.Join(" ", innerSplit);
            }
        }
    }
    public class Result
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public List<Item> Items { get; set; } = new List<Item>();

    }
    public class Item
    {
        public List<string> Instructions { get; set; } = new List<string>();
        public List<DoubleString> Ingredients { get; set; } = new List<DoubleString>();
    }

    public class DoubleString
    {
        public string One { get; set; }
        public string Two { get; set; }
    }
    public class Time
    {
        public double? time { get; set; }
    }
}