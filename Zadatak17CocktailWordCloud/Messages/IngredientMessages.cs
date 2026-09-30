namespace Zadatak17CocktailWordCloud.Messages;

// Poruka koju Rx salje aktoru za svaki pronadjeni sastojak
public sealed class IngredientFound
{
    public IngredientFound(string cocktailName, string ingredient)
    {
        CocktailName = cocktailName;
        Ingredient = ingredient;
    }

    public string CocktailName { get; }
    public string Ingredient { get; }
}

// Potvrda da je aktor obradio sastojak
public sealed class IngredientProcessed
{
    public IngredientProcessed(string ingredient, int frequency)
    {
        Ingredient = ingredient;
        Frequency = frequency;
    }

    public string Ingredient { get; }
    public int Frequency { get; }
}

// Zahtev za trenutno stanje Word Cloud-a
public sealed class GetWordCloud
{
    public static GetWordCloud Instance { get; } = new();
    private GetWordCloud() { }
}

// Odgovor aktora sa kopijom trenutnog stanja
public sealed class WordCloudResult
{
    public WordCloudResult(Dictionary<string, int> frequencies)
    {
        Frequencies = frequencies;
    }

    public Dictionary<string, int> Frequencies { get; }
}

// Poruka za brisanje prethodnog stanja
public sealed class ResetIngredients
{
    public static ResetIngredients Instance { get; } = new();
    private ResetIngredients() { }
}

public sealed class ResetCompleted
{
    public static ResetCompleted Instance { get; } = new();
    private ResetCompleted() { }
}
