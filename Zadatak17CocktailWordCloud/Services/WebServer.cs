using Akka.Actor;
using System.Diagnostics;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using Zadatak17CocktailWordCloud.Messages;
using Zadatak17CocktailWordCloud.Utils;

namespace Zadatak17CocktailWordCloud.Services;

public class WebServer
{
    private readonly HttpListener _listener = new();
    private readonly CocktailService _cocktailService;
    private readonly IActorRef _ingredientActor;

    public WebServer(
        CocktailService cocktailService,
        IActorRef ingredientActor,
        string prefix)
    {
        _cocktailService = cocktailService;
        _ingredientActor = ingredientActor;
        _listener.Prefixes.Add(prefix);
    }

    public void Start()
    {
        _listener.Start();
        Logger.Info("Web server sluša na http://localhost:8080/");

        
        _ = Task.Run(ListenLoopAsync);
    }

    public void Stop()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
            _listener.Close();
            Logger.Info("Web server je zaustavljen.");
        }
    }

    private async Task ListenLoopAsync()
    {
        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                //svaki zahtev nezavisno
                _ = Task.Run(() => HandleRequestAsync(context));
            }
            catch (HttpListenerException)
            {
                
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Greška u serverskoj petlji: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        string method = context.Request.HttpMethod;
        string path = context.Request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

        Logger.Info($"HTTP {method} {path} - zahtev primljen.");

        try
        {
            if (method != "GET")
            {
                await WriteHtmlAsync(
                    context,
                    Page("Metoda nije podržana", "<p>Dozvoljen je samo GET zahtev.</p>"),
                    405);
                return;
            }

            switch (path)
            {
                case "/":
                    await WriteHtmlAsync(context, HomePage());
                    break;

                case "/load":
                    await LoadCocktailsAndShowResultAsync(context);
                    break;

                case "/wordcloud":
                    await ShowWordCloudAsync(context);
                    break;

                case "/reset":
                    await ResetWordCloudAsync(context);
                    break;

                default:
                    await WriteHtmlAsync(
                        context,
                        Page("404", "<p>Tražena stranica ne postoji.</p><p><a href='/'>Početna</a></p>"),
                        404);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Obrada zahteva nije uspela: {ex.Message}");

            if (context.Response.OutputStream.CanWrite)
            {
                await WriteHtmlAsync(
                    context,
                    Page(
                        "Greška",
                        $"<p>Došlo je do greške: {WebUtility.HtmlEncode(ex.Message)}</p>" +
                        "<p><a href='/'>Početna</a></p>"),
                    500);
            }
        }
        finally
        {
            stopwatch.Stop();
            int statusCode = context.Response.StatusCode;
            string result = statusCode < 400 ? "USPEŠNO" : "GREŠKA";

            Logger.Info(
                $"HTTP {method} {path} - {result}, status {statusCode}, " +
                $"vreme {stopwatch.ElapsedMilliseconds} ms.");
        }
    }

    private async Task LoadCocktailsAndShowResultAsync(HttpListenerContext context)
    {
        Logger.Info("Pokrenuto novo prikupljanje podataka.");

        // Svaki /load počinje od praznog Word Cloud-a.
        await _ingredientActor.Ask<ResetCompleted>(
            ResetIngredients.Instance,
            TimeSpan.FromSeconds(5));

        int processedIngredients = 0;
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = _cocktailService
            .LoadNonAlcoholicIngredients()
            .Subscribe(
                onNext: _ => Interlocked.Increment(ref processedIngredients),
                onError: error => completion.TrySetException(error),
                onCompleted: () => completion.TrySetResult(true));

        try
        {
            await completion.Task;
        }
        finally
        {
            subscription.Dispose();
        }

        Logger.Info($"Rx obrada završena. Obrađeno sastojaka: {processedIngredients}.");

        var result = await _ingredientActor.Ask<WordCloudResult>(
            GetWordCloud.Instance,
            TimeSpan.FromSeconds(5));

        string info =
            $"<p><strong>Obrađeno pojavljivanja sastojaka:</strong> {processedIngredients}</p>" +
            $"<p><strong>Različitih sastojaka:</strong> {result.Frequencies.Count}</p>";

        await WriteHtmlAsync(
            context,
            Page("Word Cloud - rezultat", info + BuildWordCloud(result) + Navigation()));
    }

    private async Task ShowWordCloudAsync(HttpListenerContext context)
    {
        var result = await _ingredientActor.Ask<WordCloudResult>(
            GetWordCloud.Instance,
            TimeSpan.FromSeconds(5));

        await WriteHtmlAsync(
            context,
            Page("Trenutni Word Cloud", BuildWordCloud(result) + Navigation()));
    }

    private async Task ResetWordCloudAsync(HttpListenerContext context)
    {
        await _ingredientActor.Ask<ResetCompleted>(
            ResetIngredients.Instance,
            TimeSpan.FromSeconds(5));

        await WriteHtmlAsync(
            context,
            Page("Reset", "<p>Word Cloud je obrisan.</p>" + Navigation()));
    }

    private static string BuildWordCloud(WordCloudResult result)
    {
        if (result.Frequencies.Count == 0)
        {
            return "<p>Nema podataka. Klikni na <strong>Učitaj podatke</strong>.</p>";
        }

        int maxFrequency = result.Frequencies.Values.Max();
        int total = result.Frequencies.Values.Sum();

        var cloud = new StringBuilder();
        cloud.Append("<div class='cloud'>");

        foreach (var item in result.Frequencies
                     .OrderByDescending(x => x.Value)
                     .ThenBy(x => x.Key))
        {
            int fontSize = 16 + (int)(44.0 * item.Value / maxFrequency);
            string ingredient = WebUtility.HtmlEncode(item.Key);

            cloud.Append(
                $"<span style='font-size:{fontSize}px' title='{ingredient}: {item.Value}'>" +
                $"{ingredient}<small> ({item.Value})</small></span>");
        }

        cloud.Append("</div>");
        cloud.Append($"<p>Ukupan broj pojavljivanja: <strong>{total}</strong></p>");

        cloud.Append("<table><tr><th>Sastojak</th><th>Frekvencija</th></tr>");
        foreach (var item in result.Frequencies
                     .OrderByDescending(x => x.Value)
                     .ThenBy(x => x.Key))
        {
            cloud.Append(
                $"<tr><td>{WebUtility.HtmlEncode(item.Key)}</td><td>{item.Value}</td></tr>");
        }
        cloud.Append("</table>");

        return cloud.ToString();
    }

    private static string HomePage()
    {
        string body = @"
            <p>Projekat koristi <strong>Rx.NET</strong>, <strong>Akka.NET</strong> i TheCocktailDB API.</p>
            <p>Rx dohvata bezalkoholne koktele, zatim njihove detalje i sastojke.</p>
            <p>Svaki sastojak se šalje kao poruka Akka aktoru koji čuva frekvencije u svom internom stanju.</p>
            <div class='actions'>
                <a class='button' href='/load'>Učitaj podatke i napravi Word Cloud</a>
                <a class='button secondary' href='/wordcloud'>Prikaži trenutno stanje</a>
                <a class='button danger' href='/reset'>Obriši stanje</a>
            </div>";

        return Page("Zadatak 17 - Non-Alcoholic Cocktail Word Cloud", body);
    }

    private static string Navigation()
    {
        return @"
            <div class='actions'>
                <a class='button' href='/load'>Učitaj ponovo</a>
                <a class='button secondary' href='/wordcloud'>Trenutno stanje</a>
                <a class='button secondary' href='/'>Početna</a>
                <a class='button danger' href='/reset'>Reset</a>
            </div>";
    }

    private static string Page(string title, string body)
    {
        return $@"<!DOCTYPE html>
<html lang='sr'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <title>{WebUtility.HtmlEncode(title)}</title>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 1100px; margin: 40px auto; padding: 0 20px; color: #222; }}
        h1 {{ margin-bottom: 20px; }}
        .cloud {{ border: 1px solid #ddd; border-radius: 10px; padding: 25px; line-height: 1.8; margin: 25px 0; }}
        .cloud span {{ display: inline-block; margin: 6px 10px; }}
        .cloud small {{ font-size: 11px; color: #666; }}
        .actions {{ margin: 25px 0; display: flex; gap: 10px; flex-wrap: wrap; }}
        .button {{ display: inline-block; padding: 10px 14px; text-decoration: none; border: 1px solid #333; border-radius: 6px; color: #111; }}
        .secondary {{ border-color: #777; }}
        .danger {{ border-color: #a33; }}
        table {{ border-collapse: collapse; width: 100%; margin-top: 25px; }}
        th, td {{ border-bottom: 1px solid #ddd; padding: 8px; text-align: left; }}
    </style>
</head>
<body>
    <h1>{WebUtility.HtmlEncode(title)}</h1>
    {body}
</body>
</html>";
    }

    private static async Task WriteHtmlAsync(
        HttpListenerContext context,
        string html,
        int statusCode = 200)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(html);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;

        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.OutputStream.Close();
    }
}
