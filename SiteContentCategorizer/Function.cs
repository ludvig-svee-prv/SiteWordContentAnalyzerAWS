using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using SiteContentCategorizer.source.InputValidation;
using SiteContentCategorizer.source.SiteParser;
using SiteContentCategorizer.source.AWS;
using System.Text;
using Amazon.DynamoDBv2.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
namespace SiteContentCategorizer;

public class Functions
{
    /// <summary>
    /// Constructor that Lambda will invoke.
    /// </summary>
    public Functions()
    {

    }

    /// <summary>
    /// Fetches all word content from a website and uploads it to DynamoDB and tags them with given content type. These words are then used as information for the Website analyser. Respect Robots.txt to ensure ethical use.
    /// </summary>
    /// <returns> Returns 200 if the content is uploaded correctly. </returns>
    public static APIGatewayProxyResponse UploadWebsiteContentCategory(APIGatewayProxyRequest request, ILambdaContext context)
    {
        UploadWebsiteCategoryInput? input;

        try
        {
            input = JsonSerializer.Deserialize<UploadWebsiteCategoryInput>(request.Body, new JsonSerializerOptions() { IncludeFields = true});
        }
        catch (Exception ex)
        {
            APIGatewayProxyResponse failParseResponse = new()
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "Input json was incorrect. Error: " + JsonSerializer.Serialize(ex.Message),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return failParseResponse;
        }

        if (input == null)
        {
            APIGatewayProxyResponse failParseResponse = new()
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = "Input json was not correctly parsed. Contanct Server Admin.",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return failParseResponse;
        }

        Console.WriteLine("Started upload of site: " + input.SiteUrl);
        AmazonDynamoDBClient client = new(Amazon.RegionEndpoint.EUNorth1); // Using the Default set profile with Stockholm as datacenter (eu-north-1)

        Task<FetchWebsiteResponse> alreadyParsedTask = DynamoDBHandler.FetchWebsiteFromAlreadyParsedTable(input.SiteUrl, client);
        alreadyParsedTask.Wait();
        FetchWebsiteResponse alreadyParsedResponse = alreadyParsedTask.Result;

        if (alreadyParsedResponse.Found)
        {
            return new()
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "The website has already been processed and don't need to be uploaded.",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }

        Task<GetAllUniqueWordsFromWebsiteResponse> websiteTask = SiteWordFetcher.GetAllUniqueWordsFromWebsite(input.SiteUrl);
        websiteTask.Wait();
        GetAllUniqueWordsFromWebsiteResponse response = websiteTask.Result;

        if (!response.Successful)
        {
            return new()
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "The website was unable to be processed. Reason: " + response.ErrorMessage,
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }
        Task<bool> uploadWebsite = DynamoDBHandler.UploadSiteToAlreadyProcessedTable(input.SiteUrl, response.FoundWords, input.Category, client);


        string message;
        int statusCode;
        StringBuilder sb = new();
        if (!uploadWebsite.Result)
        {
            sb.Append("Unable to upload website ");
            sb.Append(input.SiteUrl);
            sb.Append(" to DynamoDB - AlreadyProcessedTable.");
            statusCode = (int)HttpStatusCode.InternalServerError;
        }
        else
        {
            sb.Append("Succesfully uploaded website: ");
            sb.Append(input.SiteUrl);
            statusCode = (int)HttpStatusCode.OK;
        }
        message = sb.ToString();
        Console.WriteLine(message);

        return new()
        {
            StatusCode = statusCode,
            Body = message,
            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
        };
    }

    /// <summary>
    /// Analyse a websites word content to get the statistical change for different website categories. Respect Robots.txt to ensure ethical use.
    /// </summary>
    /// <returns> Returns the id of the current website analysis to be used when fetching results when successful. Otherwise returns reason it was unable to generate. </returns>
    public static APIGatewayProxyResponse AnalyseWebsite(APIGatewayProxyRequest request, ILambdaContext context)
    {
        StartWebsiteAnalysisInput? input;

        try
        {
            input = JsonSerializer.Deserialize<StartWebsiteAnalysisInput>(request.Body, new JsonSerializerOptions() { IncludeFields = true });
        }
        catch (Exception ex)
        {
            APIGatewayProxyResponse failParseResponse = new()
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Body = "Input json was incorrect. Error: " + JsonSerializer.Serialize(ex.Message),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return failParseResponse;
        }

        if (input == null)
        {
            APIGatewayProxyResponse failParseResponse = new()
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = "Input json was not correctly parsed. Contanct Server Admin.",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return failParseResponse;
        }

        AmazonDynamoDBClient client = new(Amazon.RegionEndpoint.EUNorth1); // Using the Default set profile with Stockholm as datacenter (eu-north-1)

        Task<FetchWebsiteResponse> alreadyParsedTask = DynamoDBHandler.FetchWebsiteFromAlreadyParsedTable(input.SiteUrl, client);
        alreadyParsedTask.Wait();
        FetchWebsiteResponse alreadyParsedResponse = alreadyParsedTask.Result;

        List<string> pageWords;
        if (!alreadyParsedResponse.Found)
        {
            Task<GetAllUniqueWordsFromWebsiteResponse> websiteTask = SiteWordFetcher.GetAllUniqueWordsFromWebsite(input.SiteUrl);
            websiteTask.Wait();
            GetAllUniqueWordsFromWebsiteResponse response = websiteTask.Result;

            if (!response.Successful)
            {
                return new()
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = "The website was unable to be processed. Reason: " + response.ErrorMessage,
                    Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                };
            }
            pageWords = response.FoundWords;
        }
        else
        {
            pageWords = alreadyParsedResponse.Words;
        }

        // Fetch all websites that have been uploaded and check for each website it they have a word
        Task<FetchAllProccessedSitesResponse> allSites = DynamoDBHandler.FetchAllProcessedSites(client);
        allSites.Wait();

        if (!allSites.Result.Found)
        {
            return new()
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = "Processed websites were unable to be fetched from DB. Contact system admin.",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
        }

        source.CompletedAnalysis ca = source.WordCategorizer.GetCompletedAnalysis(input.SiteUrl, pageWords, allSites.Result.Sites);
        string bodyResponse = JsonSerializer.Serialize(ca, new JsonSerializerOptions() { IncludeFields = true });

        return new()
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = bodyResponse,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}