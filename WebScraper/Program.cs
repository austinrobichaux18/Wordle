using Microsoft.Playwright;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

public class Program
{
    private static IPage page;
    private static IKeyboard keyboard;
    public static async Task Main()
    {
        //using var playwright = await Playwright.CreateAsync();
        //await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        //{
        //    Headless = false,
        //});
        //page = await browser.NewPageAsync();
        //keyboard = page.Keyboard;

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Webkit.LaunchAsync();
        var page = await browser.NewPageAsync();

        var urls = new List<string> { "https://www.food.com/recipe/strawberry-rhubarb-dump-cake-408694" };
        var results = new List<Result>();

        //while (true)
        //{
        //}
        await GetResultsAsync(page, urls, results, 0);
    }

    private static async Task GetResultsAsync(IPage page, List<string> urls, List<Result> results, int index)
    {
        try
        {
            await page.GotoAsync(urls[index]);

            var result = new Result();
            result.Url = page.Url;
            result.Title = (await page.TitleAsync()).Replace("  - Food.com","");

            var item = new Item();
            await GetInstructionsAsync(page, item);
            await GetIngredientsAsync(page, item);
            result.Items.Add(item);
            await GetImagesAsync(page, result);
            results.Add(result);

            await GetOtherRecipeUrlsAsync(page, urls);

            if (results.Count > 10)
            {
                var json = JsonConvert.SerializeObject(results);
                File.WriteAllText("C:\\Users\\arobi\\source\\repos\\Wordle\\WebScraper\\Results.json", json);
                return;
            }
            await GetResultsAsync(page, urls, results, index + 1);
        }
        catch (Exception e)
        {
            Console.WriteLine(urls[index]);
            Console.WriteLine(e.Message);
            Console.WriteLine();
        }
    }

    private static async Task GetOtherRecipeUrlsAsync(IPage page, List<string> urls)
    {
        var aLocator = page.Locator("a");
        for (int i = 0; i < await aLocator.CountAsync(); i++)
        {
            var recipeUrl = await aLocator.Nth(i).GetAttributeAsync("href");
            if (recipeUrl.StartsWith("https") && !recipeUrl.Contains("user") && recipeUrl.Contains("recipe") && !urls.Contains(recipeUrl))
            {
                urls.Add(recipeUrl);
            }
        }
    }

    private static async Task GetImagesAsync(IPage page, Result result)
    {
        var imgLocator = page.Locator("img");
        for (int i = 0; i < await imgLocator.CountAsync(); i++)
        {
            var imgUrl = await imgLocator.Nth(i).GetAttributeAsync("src");
            if (imgUrl.Contains("avatar") || imgUrl.Contains("promo") || imgUrl.Contains("yahoo"))
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
    }

    private static async Task GetInstructionsAsync(IPage page, Item item)
    {
        var instructionsElement = await page.WaitForSelectorAsync(".direction-list");
        var instructions = await instructionsElement.InnerTextAsync();
        item.Instructions = instructions.Split("\n").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static async Task GetIngredientsAsync(IPage page, Item item)
    {
        var ingredientElement = await page.WaitForSelectorAsync(".ingredient-list");
        var ingredients = await ingredientElement.InnerTextAsync();
        var split = ingredients.Split("\n").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        for (int i = 0; i < split.Count / 2; i++)
        {
            item.Ingredients.Add(new DoubleString());
        }
        item.Ingredients.Add(new DoubleString());

        for (int i = 0; i < split.Count; i++)
        {
            var temp = item.Ingredients.First(x => string.IsNullOrWhiteSpace(x.One) || string.IsNullOrWhiteSpace(x.Two));
            if (string.IsNullOrWhiteSpace(temp.One))
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
            var temp = item.Ingredients[i].Two.Split(" ").ToList();

            item.Ingredients[i].One += " " + temp[0];
            temp.RemoveAt(0);
            item.Ingredients[i].Two = string.Join(" ", temp);
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

}