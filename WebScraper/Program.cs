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
        var ideas = new List<FoodIdea>();
        var topics = new List<FoodTopic>();

        var oldResults = JsonConvert.DeserializeObject<List<Result>>(await File.ReadAllTextAsync("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json"));
        topics = JsonConvert.DeserializeObject<List<FoodTopic>>(await File.ReadAllTextAsync("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Topics.json"));
        ideas = JsonConvert.DeserializeObject<List<FoodIdea>>(await File.ReadAllTextAsync("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Ideas.json"));
        urls = ideas.SelectMany(x => x.RecipeUrls).ToList();
        foreach (var item in oldResults.Select(x=> x.Url))
        {
            if(urls.Contains(item))
            {
                urls.Remove(item);
            }
        }

        var batchSize = 4;
        for (int i = 0; i < urls.ToList().Count() / batchSize; i++)
        {
            var tasks = new List<Task>();
            for (int j = 0; j < batchSize; j++)
            {
                //await GetResultsAsync(null, urls, results, ideas, topics, i * batchSize + j);
                tasks.Add(GetResultsAsync(null, urls, results, ideas, topics, i * batchSize + j));
            }
            await Task.WhenAll(tasks);
            if (i > 0 && i % 5 == 0)
            {
                if (results.Count > 0)
                {
                    //TODO NEXT RESTART THIS NEEDS TO PULL EXISTING FILE AND THEN UPDATE IT INSTEAD OF OVERWRITING
                    File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json", JsonConvert.SerializeObject(results));
                }
                //if (topics.Count > 0)
                //{
                //     File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Topics.json", JsonConvert.SerializeObject(topics));
                //}
                //if (ideas.Count > 0)
                //{
                //    File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Ideas.json", JsonConvert.SerializeObject(ideas));
                //}
            }
        }
        if (results.Count > 0)
        {
            //TODO NEXT RESTART THIS NEEDS TO PULL EXISTING FILE AND THEN UPDATE IT INSTEAD OF OVERWRITING
            File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json", JsonConvert.SerializeObject(results));
        }
        //if (topics.Count > 0)
        //{
        //    File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Topics.json", JsonConvert.SerializeObject(topics));
        //}
        //if (ideas.Count > 0)
        //{
        //    File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Ideas.json", JsonConvert.SerializeObject(ideas));
        //}
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

    private static async Task GetResultsAsync(IPage page, List<string> urls, List<Result> results, List<FoodIdea> ideas, List<FoodTopic> topics, int index)
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

            var idea = new FoodIdea();
            var result = new Result();
            var topic = new FoodTopic();
            result.Url = page.Url;

            Console.WriteLine($"Getting Title ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            var title = (await page.TitleAsync()).Replace("- Food.com", "").Trim();
            Console.WriteLine($"Got Title ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            Console.WriteLine();

            if (result.Url.Contains("/ideas/"))
            {
                Console.WriteLine($"Idea Page. ({index}) ({urls[index]})" + GetTime(time, timer.ElapsedMilliseconds));

                idea.Title = title;
                idea.IdeaUrl = result.Url;
                idea.Idea = result.Url.Split("/").Last();
                await GetParentCollectionAsync(page, idea, index, time, timer);
                await GetOtherRecipeUrlsAsync(page, null, idea.RecipeUrls, index, time, timer);
                ideas.Add(idea);
            }
            else if (result.Url.Contains("/topic/"))
            {
                Console.WriteLine($"Topic Page. ({index}) ({urls[index]})" + GetTime(time, timer.ElapsedMilliseconds));

                topic.Title = title;
                topic.TopicUrl = result.Url;
                topic.Topic = result.Url.Split("/").Last();
                await GetOtherRecipeUrlsAsync(page, null, topic.IdeaUrls, index, time, timer);
                topics.Add(topic);
            }
            else if (result.Url.Contains("/recipe/"))
            {
                Console.WriteLine($"Recipe Page. ({index}) ({urls[index]})" + GetTime(time, timer.ElapsedMilliseconds));
                result.Title = title.Replace("Recipe", "").Trim();

                var item = new Item();
                await GetInstructionsAsync(page, item, index, time, timer);
                await GetIngredientsAsync(page, item, index, time, timer);
                result.Items.Add(item);
                await GetImagesAsync(page, result, index, time, timer);
                await GetFactsAsync(page, result, index, time, timer);

                //await GetOtherRecipeUrlsAsync(page, result, urls, index, time, timer);
                results.Add(result);
            }

            timer.Stop();
            Console.WriteLine($"Done ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        }
        catch (Exception e)
        {
            Console.WriteLine($"Url ({index}): " + urls[index]);
            Console.WriteLine(e.Message);
            Console.WriteLine();
        }
        await page.CloseAsync();
    }
    private static async Task GetNutritionInformationAsync(IPage page, List<string> urls, int index, Time time, Stopwatch timer)
    {

    }
    private static async Task GetOtherRecipeUrlsAsync(IPage page, Result result, List<string> urls, int index, Time time, Stopwatch timer)
    {
        Console.WriteLine($"Getting RecipeUrls ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        var aLocator = page.Locator("a");
        var topicIndex = 0;

        var count = await aLocator.CountAsync();
        Console.WriteLine($"Getting RecipeUrls ({index}). Total count ({count}): " + GetTime(time, timer.ElapsedMilliseconds));
        for (int i = 0; i < count; i++)
        {
            var recipeUrl = await aLocator.Nth(i).GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(recipeUrl))
            {
                continue;
            }
            if (recipeUrl.StartsWith("https") && !recipeUrl.Contains("user") && recipeUrl.Contains("recipe") && !urls.Contains(recipeUrl))
            {
                urls.Add(recipeUrl);
            }
            else if (recipeUrl == "/recipes")
            {
                topicIndex = i + 1;
            }
            else if (i == topicIndex)
            {
                if (result != null)
                {
                    result.TopicUrl = recipeUrl;
                    result.Topic = recipeUrl.Split("/").Last();
                }
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

    private static async Task GetParentCollectionAsync(IPage page, FoodIdea idea, int index, Time time, Stopwatch timer)
    {
        try
        {
            Console.WriteLine($"Getting Parent Collection ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            var element = await page.WaitForSelectorAsync(".parent-collection");
            var attribute = await element.GetAttributeAsync("href");
            Console.WriteLine($"Got Parent Collection ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
            Console.WriteLine();
            idea.ParentIdeaUrl = attribute;
            idea.ParentIdea = attribute.Split("/").Last();
        }
        catch
        {

        }
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

    private static async Task GetFactsAsync(IPage page, Result result, int index, Time time, Stopwatch timer)
    {
        Console.WriteLine($"Getting Facts ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        var labelLocator = page.Locator(".facts__label");
        var valueLocator = page.Locator(".facts__value");
        for (int i = 0; i < await valueLocator.CountAsync(); i++)
        {
            var label = await labelLocator.Nth(i).InnerTextAsync();
            var value = (await valueLocator.Nth(i).InnerTextAsync()).Replace("\n", "");
            if (label == "Serves:")
            {
                if (value.Contains("-"))
                {
                    //todo fix to do avg instead of min
                    value = value.Split("-")[0];
                }
                if (int.TryParse(value, out var intResult))
                {
                    result.Servings = intResult;
                }
            }
            if (label == "Ready In:")
            {
                var split = value.Split(" ");
                int number = GetInt(split[0]);
                if (split.Length > 1)
                {
                    number = GetInt(split[0]) * 60 + GetInt(split[1]);
                }
                else if (value.Trim().EndsWith("hr"))
                {
                    number = GetInt(value) * 60;
                }
                result.CookTimeMinutes = number;
            }
        }
        Console.WriteLine($"Got Facts ({index}): " + GetTime(time, timer.ElapsedMilliseconds));
        Console.WriteLine();

    }
    private static int GetInt(string str)
    {
        return int.Parse(string.Join("", str.Where(x => char.IsNumber(x))));
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
        public int Servings { get; set; }
        public int CookTimeMinutes { get; set; }
        public string Title { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public List<Item> Items { get; set; } = new List<Item>();
        public string TopicUrl { get; set; }
        public string Topic { get; set; }
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
    public class FoodIdea
    {
        public string ParentIdeaUrl { get; set; }
        public string ParentIdea { get; set; }
        public string IdeaUrl { get; set; }
        public string Idea { get; set; }
        public string Title { get; set; }
        public List<string> RecipeUrls { get; set; } = new List<string>();
    }
    public class FoodTopic
    {
        public string TopicUrl { get; set; }
        public string Topic { get; set; }
        public string Title { get; set; }
        public List<string> IdeaUrls { get; set; } = new List<string>();
    }
}