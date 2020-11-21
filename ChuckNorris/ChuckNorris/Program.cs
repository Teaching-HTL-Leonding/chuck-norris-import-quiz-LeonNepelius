using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

var factory = new ChuckNorrisJokesContextFactory();
using var context = factory.CreateDbContext();
using var client = new HttpClient();

if (args.Length == 1)
{
    if (args[0] == "clean")
    {
        //Delete every row
        await context.Database.ExecuteSqlRawAsync("DELETE FROM Jokes");
        //Reset the PK
        await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT('Jokes', RESEED, 0)");
        await context.SaveChangesAsync();
    }
    else
    {
        var numberOfJokes = 5;
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            numberOfJokes = Convert.ToInt32(args[0]);
            if (numberOfJokes > 10) throw new Exception("Too many jokes");
            for (int i = 0; i < numberOfJokes; i++)
            {
                var jokeData = await GetRandomJoke();
                await AddJokeToDatabase(jokeData);
            }
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Something bad happened: {ex.Message}");
        }
    }
}
else
{
    Console.Error.WriteLine("Wrong Command-Line Arguments");
}

async System.Threading.Tasks.Task<ChuckNorrisJokeData> GetRandomJoke()
{
    var counter = 0;
    var jokeData = new ChuckNorrisJokeData();
    do
    {
        if (counter == 9) throw new Exception("We got all of the Chuck Norris Jokes in the world >:)))");
        var responseBody = await client.GetStreamAsync("https://api.chucknorris.io/jokes/random");
        jokeData = await JsonSerializer.DeserializeAsync<ChuckNorrisJokeData>(responseBody);
        if (jokeData == null) throw new Exception("Could not deserialize json!");
        if (jokeData.Categories.FirstOrDefault() != "explicit") break;
        counter++;
    } while (!context.Jokes.Select(x => x.ChuckNorrisId).Contains(jokeData.Id));
    return jokeData;
}

async System.Threading.Tasks.Task AddJokeToDatabase(ChuckNorrisJokeData jokeData)
{
    var newJoke = new ChuckNorrisJoke { ChuckNorrisId = jokeData.Id, Url = jokeData.Url, Joke = jokeData.Value };
    await context.Jokes.AddAsync(newJoke);
    await context.SaveChangesAsync();
}

class ChuckNorrisJokeData
{
    [JsonPropertyName("categories")]
    public string[] Categories { get; set; } = Array.Empty<string>();

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

}


class ChuckNorrisJoke
{
    public int Id { get; set; }

    [MaxLength(40)]
    public string ChuckNorrisId { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string Url { get; set; } = string.Empty;

    public string Joke { get; set; } = string.Empty;
}

class ChuckNorrisJokesContext : DbContext
{
    public DbSet<ChuckNorrisJoke> Jokes { get; set; }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ChuckNorrisJokesContext(DbContextOptions<ChuckNorrisJokesContext> options) : base(options) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}

class ChuckNorrisJokesContextFactory : IDesignTimeDbContextFactory<ChuckNorrisJokesContext>
{
    public ChuckNorrisJokesContext CreateDbContext(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var optionsBuilder = new DbContextOptionsBuilder<ChuckNorrisJokesContext>();
        optionsBuilder
            .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .UseSqlServer(configuration["ConnectionStrings:DefaultConnection"]);

        return new ChuckNorrisJokesContext(optionsBuilder.Options);
    }
}