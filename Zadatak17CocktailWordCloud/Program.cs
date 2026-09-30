using Akka.Actor;
using Akka.Configuration;
using Zadatak17CocktailWordCloud.Actors;
using Zadatak17CocktailWordCloud.Services;
using Zadatak17CocktailWordCloud.Utils;

namespace Zadatak17CocktailWordCloud;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        //Akka Dispatcher
        var config = ConfigurationFactory.ParseString(@"
            cocktail-dispatcher {
                type = Dispatcher
                executor = ""thread-pool-executor""
                throughput = 10
            }
        ");

        using var actorSystem = ActorSystem.Create("cocktail-system", config);

        var ingredientActor = actorSystem.ActorOf(
            IngredientActor.Props().WithDispatcher("cocktail-dispatcher"),
            "ingredient-actor");

        var cocktailService = new CocktailService(ingredientActor);
        var webServer = new WebServer(cocktailService, ingredientActor, "http://localhost:8080/");

        webServer.Start();

        Console.WriteLine();
        Console.WriteLine("Server je pokrenut.");
        Console.WriteLine("Otvori u browseru: http://localhost:8080/");
        Console.WriteLine("Pritisni ENTER za kraj rada servera.");
        Console.WriteLine();

        Console.ReadLine();

        webServer.Stop();
        await actorSystem.Terminate();

        Logger.Info("Aplikacija je završila sa radom.");
    }
}
