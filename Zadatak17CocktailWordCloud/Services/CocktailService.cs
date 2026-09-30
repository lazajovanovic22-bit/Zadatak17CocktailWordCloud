using Akka.Actor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Zadatak17CocktailWordCloud.Messages;
using Zadatak17CocktailWordCloud.Models;
using Zadatak17CocktailWordCloud.Utils;

namespace Zadatak17CocktailWordCloud.Services;

public class CocktailService
{
    private const string BaseUrl = "https://www.thecocktaildb.com/api/json/v1/1/";

    private readonly HttpClient _client = new();
    private readonly IActorRef _ingredientActor;

    public CocktailService(IActorRef ingredientActor)
    {
        _ingredientActor = ingredientActor;
    }

    
    public IObservable<IngredientProcessed> LoadNonAlcoholicIngredients()
    {
        return Observable.FromAsync(GetNonAlcoholicCocktailsAsync)
            .Do(cocktails =>
                Logger.Info($"[RX] Pronađeno koktela: {cocktails.Count}"))
            .SelectMany(cocktails => cocktails) //raspakonja liste u pojedinacne koktele

            // Za svaki koktel pravimo asinhroni Rx poziv za detalje
            .Select(cocktail =>
                Observable.FromAsync(() => GetCocktailDetailsAsync(cocktail)))

            // Najvise 4 paralelna poziva ka API-ju
            .Merge(maxConcurrent: 4)

            .Where(cocktail => cocktail.Ingredients.Count > 0)
            .SelectMany(cocktail => cocktail.Ingredients.Select(ingredient =>
                new IngredientFound(cocktail.Name, ingredient)))
            .Where(message => !string.IsNullOrWhiteSpace(message.Ingredient))
            .Select(message => new IngredientFound(
                message.CocktailName,
                message.Ingredient.Trim()))

            // Rx emituje poruke ka aktoru
            // Ask koristimo da bismo znali da je svaki sastojak stvarno obradjen
            // pre nego sto se Rx tok zavrsi
            .Select(message =>
                Observable.FromAsync(() =>
                    _ingredientActor.Ask<IngredientProcessed>(
                        message,
                        TimeSpan.FromSeconds(10))))
            .Merge(maxConcurrent: 8)

            // Rx Scheduler
            .SubscribeOn(TaskPoolScheduler.Default)
            .ObserveOn(TaskPoolScheduler.Default);
    }

    private async Task<List<CocktailShort>> GetNonAlcoholicCocktailsAsync()
    {
        Logger.Info("[API] Dohvat bezalkoholnih koktela...");

        string json = await GetStringAsync("filter.php?a=Non_Alcoholic");

        var response = JsonConvert.DeserializeObject<CocktailListResponse>(json);
        return response?.Drinks ?? new List<CocktailShort>();
    }

    private async Task<CocktailDetails> GetCocktailDetailsAsync(CocktailShort cocktail)
    {
        Logger.Info($"[API] Detalji: {cocktail.Name} ({cocktail.IdDrink})");

        string json = await GetStringAsync($"lookup.php?i={cocktail.IdDrink}");
        var root = JsonConvert.DeserializeObject<JObject>(json);
        var drink = root?["drinks"]?.FirstOrDefault();

        if (drink == null)
            return new CocktailDetails(cocktail.IdDrink, cocktail.Name, new List<string>());

        var ingredients = Enumerable.Range(1, 15)
            .Select(i => (string?)drink[$"strIngredient{i}"])
            .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient))
            .Select(ingredient => ingredient!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CocktailDetails(cocktail.IdDrink, cocktail.Name, ingredients);
    }

    private async Task<string> GetStringAsync(string relativeUrl)
    {
        using var response = await _client.GetAsync(BaseUrl + relativeUrl);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"TheCocktailDB je vratio status {(int)response.StatusCode} ({response.StatusCode}).");
        }

        return await response.Content.ReadAsStringAsync();
    }
}
