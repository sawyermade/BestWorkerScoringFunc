// using Microsoft.Azure.Functions.Worker;
// using Microsoft.Extensions.Logging;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;

// namespace BestWorkerScoringFunc;

// public class ScoreWorker
// {
//     private readonly ILogger<ScoreWorker> _logger;

//     public ScoreWorker(ILogger<ScoreWorker> logger)
//     {
//         _logger = logger;
//     }

//     [Function("ScoreWorker")]
//     public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
//     {
//         _logger.LogInformation("C# HTTP trigger function processed a request.");
//         return new OkObjectResult("Welcome to Azure Functions!");
//     }
// }

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace BestWorkerScoringFunc;

// Main json payload class
// public class BestWorkerModePayload
// {
//     public Job Job { get; set; } = new();
//     public Worker Worker { get; set; } = new();
//     public List<Selector> Selectors { get; set; } = new();
// }

// // Job class
// public class Job
// {
//     public bool HighPriority { get; set; }
//     public List<string> Licensure { get; set; } = new();
//     public string Jurisdiction { get; set; } = "";
// }

// // Selector class
// public class Selector
// {
//     public string Key { get; set; } = "";
//     public string Operator { get; set; } = "";
//     public int Value { get; set; }
//     public int? ExpiresAfterSeconds { get; set; }
// }

// // Worker class
// public class Worker
// {
//     public bool HighPriority { get; set; }
//     public List<string> Licensure { get; set; } = new();
//     public List<string> Jurisdiction { get; set; } = new();
// }

public class BestWorkerModePayload
{
    [JsonPropertyName("job")]
    public Job Job { get; set; } = new();

    [JsonPropertyName("selectors")]
    public List<Selectors> Selectors { get; set; } = new();

    [JsonPropertyName("worker")]
    public Worker Worker { get; set; } = new();
}

public class Job
{
    [JsonPropertyName("certificationId")]
    public string CertificationId { get; set; } = "";

    // JSON uses "JurisdictionId" (capital J)
    [JsonPropertyName("JurisdictionId")]
    public string JurisdictionId { get; set; } = "";
}

public class Worker
{
    // JSON uses "Id" (capital I)
    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    // Note: in your JSON this is a single comma-separated string, not an array
    [JsonPropertyName("certificationIds")]
    public string CertificationIds { get; set; } = "";

    // JSON uses "JurisdictionIds" (capital J), and it's an empty string in the sample
    [JsonPropertyName("JurisdictionIds")]
    public string JurisdictionIds { get; set; } = "";
}

public class Selectors
{
    // Your sample has selectors: [] so shape is unknown.
    // These common fields make it flexible; extra JSON fields will be ignored by default.
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "";

    [JsonPropertyName("value")]
    public int Value { get; set; } = 0;

    [JsonPropertyName("expiresAfterSeconds")]
    public int ExpiresAfterSeconds { get; set; } = 0;
}

// HTTP Trigger Function
public class ScoreWorker
{
    private readonly ILogger _log;

    public ScoreWorker(ILoggerFactory loggerFactory)
    {
        _log = loggerFactory.CreateLogger<ScoreWorker>();
    }

    [Function("ScoreWorker")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        Console.WriteLine($"Request Body: {body}");

        BestWorkerModePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BestWorkerModePayload>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            Console.WriteLine($"Payload Json: {JsonSerializer.Serialize(payload)}");
            Console.WriteLine($"Payload: {JsonSerializer.Serialize(payload)}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invalid JSON payload");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON payload");
            return bad;
        }

        if (payload?.Job == null || payload.Worker == null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Payload must include job + worker");
            return bad;
        }

        var score = BestWorkerMode.WorkerScore(payload);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        // IMPORTANT: return a raw number (string is fine) like "0" or "100"
        await ok.WriteStringAsync(score.ToString());
        Console.WriteLine($"Score: {score}");
        if (score >= 100)
        {
            return ok;
        }
        else
        {
            return ok;
        }
    }
}

// Best worker mode function class
public class BestWorkerMode
{
    public static int WorkerScore(BestWorkerModePayload payload)
    {
        var score = 0;
        var scoreRequired = 0;

        var jobLicensures = payload.Job.CertificationId.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        var jobJurisdiction = payload.Job.JurisdictionId;

        var workerLicensures = payload.Worker.CertificationIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        var workerJurisdiction = payload.Worker.JurisdictionIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var selectors = payload.Selectors;

        if (selectors.Count > 0)
        {
            scoreRequired = selectors.Sum(i => i.Value);

            foreach (var selector in selectors)
            {
                switch (selector.Key.ToLower())
                {
                    case "licensure":
                    {
                        int licensureValue = selector.Value;
                        string licensureOperator = selector.Operator.ToLower();

                        int licensureScore = LicensureMatchScoreString(jobLicensures, workerLicensures, licensureValue);

                        if (licensureOperator == "greaterthanequal" && licensureScore >= licensureValue)
                            score += licensureScore;
                        else if (licensureOperator == "equals" && licensureScore == licensureValue)
                            score += licensureScore;

                        break;
                    }

                    case "jurisdiction":
                    {
                        int jurisdictionValue = selector.Value;
                        string jurisdictionOperator = selector.Operator.ToLower();

                        bool has = workerJurisdiction.Contains(jobJurisdiction);

                        int jurisdictionScore =
                            jurisdictionOperator == "equals"    ? (has ? jurisdictionValue : 0) :
                            jurisdictionOperator == "notequals" ? (has ? 0 : jurisdictionValue) :
                            0;

                        score += jurisdictionScore;
                        break;
                    }
                }
            }
        }
        else
        {
            // Base scoring when no selectors
            scoreRequired = 2;
            score += LicensureMatchScoreString(jobLicensures, workerLicensures, 1);
            score += workerJurisdiction.Contains(jobJurisdiction) ? 0 : 1;
        }

        return score >= scoreRequired ? 100 : 0;
    }

    protected static int LicensureMatchScoreString(List<string> jobLicensures, List<string> workerLicensures, int licensureValue)
    {
        return jobLicensures.Any(workerLicensures.Contains)
            ? licensureValue
            : 0;
    }
}