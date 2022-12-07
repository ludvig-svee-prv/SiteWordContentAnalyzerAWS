using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SiteContentCategorizer.source.SiteParser
{
    internal class WebsiteCrawler
    {
        private PageFetcher pageFetcher;

        public string BaseUrl { get; private set; }

        public WebsiteCrawler(string baseUrl, string[]? forbiddenPaths)
        {
            pageFetcher = new(forbiddenPaths);
            BaseUrl = baseUrl;
        }

        /// <summary>
        /// Function that fetches links from the baseUrl for a set amount of pages. Max Amount is 30 pages to prevent timeout risk.
        /// </summary>
        /// <returns> Returns the id of the current website analysis to be used when fetching results when successful. Otherwise returns reason it was unable to generate. </returns>
        public FetchHtmlsFromWebsiteResponse FetchHtmlsFromWebsite()
        {
            List<HtmlDocument> finishedPages = new();
            List<Task<ProcessUrlResponse>> activeFetchHtmlTasks = new();

            if (!pageFetcher.UrlCanBeProcessed(BaseUrl))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Failed to process base url. The website - ");
                sb.Append(BaseUrl);
                sb.Append(" - can for this reason NOT be processed.");
                string message = sb.ToString();

                Console.WriteLine(message);
                return new(message);
            }
            activeFetchHtmlTasks.Add(pageFetcher.ProcessUrl(BaseUrl, BaseUrl));

            while(activeFetchHtmlTasks.Count > 0)
            {
                Task<ProcessUrlResponse>[] activeTasks = activeFetchHtmlTasks.ToArray();
                int finishedTask = Task.WaitAny(activeTasks);
                activeFetchHtmlTasks.Remove(activeTasks[finishedTask]);

                ProcessUrlResponse response = activeTasks[finishedTask].Result;
                if (!response.Successful)
                {
                    continue;
                }

                if (response.Document != null) // This should always be true.
                {
                    finishedPages.Add(response.Document);
                }
                else
                {
                    Console.WriteLine("Incorrect behaviour. Document is null despite successful processing.");
                }

                if (response.Urls != null) // This should always be true.
                {
                    foreach(string u in response.Urls)
                    {
                        if (pageFetcher.UrlCanBeProcessed(u))
                        {
                            activeFetchHtmlTasks.Add(pageFetcher.ProcessUrl(u, BaseUrl));
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Incorrect behaviour. Urls are null despite successful processing.");
                }
            }

            return new(finishedPages);
        }
    }

    internal class FetchHtmlsFromWebsiteResponse
    {
        public bool Successful { get; private set; }
        public string ErrorMessage { get; private set; }
        public List<HtmlDocument> Documents { get; private set; }

        public FetchHtmlsFromWebsiteResponse(string errorMessage)
        {
            Successful = false;
            ErrorMessage = errorMessage;
            Documents = new();
        }

        public FetchHtmlsFromWebsiteResponse(List<HtmlDocument> documents)
        {
            Successful = true;
            ErrorMessage = "";
            Documents = documents;
        }
    }
}
