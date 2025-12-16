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
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

// Main json payload class
public class BestWorkerModePayload
{
    public Job Job { get; set; } = new();
    public Worker Worker { get; set; } = new();
    public List<Selector> Selectors { get; set; } = new();
}

// Job class
public class Job
{
    public bool HighPriority { get; set; }
    public List<string> Licensure { get; set; } = new();
    public string Jurisdiction { get; set; } = "";
}

// Selector class
public class Selector
{
    public string Key { get; set; } = "";
    public string Operator { get; set; } = "";
    public int Value { get; set; }
    public int? ExpiresAfterSeconds { get; set; }
}

// Worker class
public class Worker
{
    public bool HighPriority { get; set; }
    public List<string> Licensure { get; set; } = new();
    public List<string> Jurisdiction { get; set; } = new();
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

        BestWorkerModePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BestWorkerModePayload>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
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
        return ok;
    }
}

// Best worker mode function class
public class BestWorkerMode
{
    public static int WorkerScore(BestWorkerModePayload payload)
    {
        var score = 0;
        var scoreRequired = 0;

        var jobLicensures = payload.Job.Licensure;
        var jobJurisdiction = payload.Job.Jurisdiction;

        var workerLicensures = payload.Worker.Licensure;
        var workerJurisdiction = payload.Worker.Jurisdiction;

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