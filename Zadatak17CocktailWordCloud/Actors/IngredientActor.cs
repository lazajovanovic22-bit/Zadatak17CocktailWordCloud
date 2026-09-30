using Akka.Actor;
using Zadatak17CocktailWordCloud.Messages;
using Zadatak17CocktailWordCloud.Utils;

namespace Zadatak17CocktailWordCloud.Actors;

public class IngredientActor : UntypedActor
{
    
    private readonly Dictionary<string, int> _frequencies =
        new(StringComparer.OrdinalIgnoreCase);

    protected override void OnReceive(object message)
    {
        switch (message)
        {
            case IngredientFound ingredientFound:
                ProcessIngredient(ingredientFound);
                break;

            case GetWordCloud:
                
                Sender.Tell(new WordCloudResult(
                    new Dictionary<string, int>(_frequencies, StringComparer.OrdinalIgnoreCase)));
                break;

            case ResetIngredients:
                _frequencies.Clear();
                Logger.Info("[AKTOR] Word Cloud stanje je obrisano.");
                Sender.Tell(ResetCompleted.Instance);
                break;
        }
    }

    private void ProcessIngredient(IngredientFound message)
    {
        if (_frequencies.ContainsKey(message.Ingredient))
            _frequencies[message.Ingredient]++;
        else
            _frequencies[message.Ingredient] = 1;

        int currentFrequency = _frequencies[message.Ingredient];

        Logger.Info(
            $"[AKTOR] {message.CocktailName} -> {message.Ingredient} (ukupno: {currentFrequency})");

        Sender.Tell(new IngredientProcessed(message.Ingredient, currentFrequency));
    }

    public static Props Props()
        => Akka.Actor.Props.Create<IngredientActor>();
}
