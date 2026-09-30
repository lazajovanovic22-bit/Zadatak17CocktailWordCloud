using Newtonsoft.Json;

namespace Zadatak17CocktailWordCloud.Models;

public class CocktailListResponse
{
    [JsonProperty("drinks")]
    public List<CocktailShort>? Drinks { get; set; }
}

public class CocktailShort
{
    [JsonProperty("idDrink")]
    public string IdDrink { get; set; } = string.Empty;

    [JsonProperty("strDrink")]
    public string Name { get; set; } = string.Empty;
}

public class CocktailDetails
{
    public CocktailDetails(string id, string name, List<string> ingredients)
    {
        Id = id;
        Name = name;
        Ingredients = ingredients;
    }

    public string Id { get; }
    public string Name { get; }
    public List<string> Ingredients { get; }
}
