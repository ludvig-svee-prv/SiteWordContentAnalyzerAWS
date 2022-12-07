using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace SiteContentCategorizer.source.AWS
{
    public class FetchAllProccessedSitesResponse
    {
        public bool Found { get; private set; }
        public List<FetchWebsiteResponse> Sites { get; private set; }

        public FetchAllProccessedSitesResponse(bool found, List<FetchWebsiteResponse> sites)
        {
            Found = found;
            Sites = sites;
        }
    }

    public class FetchWebsiteResponse
    {
        public bool Found { get; private set; }
        public string SiteUrl { get; private set; }
        public List<string> Words { get; private set; }
        public string Category { get; private set; }

        public FetchWebsiteResponse(bool found, string siteUrl, List<string> words, string category)
        {
            Found = found;
            SiteUrl = siteUrl;
            Words = words;
            Category = category;
        }
    }

    public class UploadWordResponse
    {
        public bool Succesful { get; private set; }
        public string Word { get; private set; }

        public UploadWordResponse(bool succesful, string word)
        {
            Succesful = succesful;
            Word = word;
        }
    }

    public class DynamoDBHandler
    {
        public async static Task<bool> UploadSiteToAlreadyProcessedTable(string url, List<string> siteWords, string category, AmazonDynamoDBClient client)
        {
            PutItemRequest request = new()
            {
                TableName = "ProcessedSiteTable",
                Item = new Dictionary<string, AttributeValue>()
                {
                    { "Website", new AttributeValue { S = url }},
                    { "Words", new AttributeValue { SS = siteWords }},
                    { "Category", new AttributeValue { S = category }}
                }
            };

            PutItemResponse response = await client.PutItemAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }

        public async static Task<FetchWebsiteResponse> FetchWebsiteFromAlreadyParsedTable(string url, AmazonDynamoDBClient client)
        {
            GetItemRequest request = new()
            {
                TableName = "ProcessedSiteTable",
                Key = new Dictionary<string, AttributeValue>() { { "Website", new AttributeValue { S = url } } },
            };

            GetItemResponse fetchTask = await client.GetItemAsync(request);
            Dictionary<string, AttributeValue> fetchedItem = fetchTask.Item;
            if (fetchedItem.Count == 0)
            {
                return new(false, "", new(), "");
            }

            List<string> words = new();
            fetchedItem["Words"].SS.ForEach(x => words.Add(x));

            return new(true, fetchedItem["Website"].S, words, fetchedItem["Category"].S);
        }

        public async static Task<FetchAllProccessedSitesResponse> FetchAllProcessedSites(AmazonDynamoDBClient client)
        {
            ScanRequest request = new()
            {
                TableName = "ProcessedSiteTable"
            };

            ScanResponse response = await client.ScanAsync(request);

            List<FetchWebsiteResponse> websites = new();
            foreach (Dictionary<string, AttributeValue> responseDic in response.Items)
            {
                List<string> words = new();
                responseDic["Words"].SS.ForEach(x => words.Add(x));
                websites.Add(new(true, responseDic["Website"].S, words, responseDic["Category"].S));
            }

            return new(true, websites);
        }
    }
}
