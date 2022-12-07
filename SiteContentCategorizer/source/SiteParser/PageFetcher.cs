using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SiteContentCategorizer.source.SiteParser
{
    internal class ProcessUrlResponse
    {
        public bool Successful { get; private set; }
        public HtmlDocument? Document { get; private set; }
        public string[]? Urls { get; private set; }

        public ProcessUrlResponse(bool successful, HtmlDocument? document, string[]? urls)
        {
            Successful = successful;
            Document = document;
            Urls = urls;
        }
    }

    internal class PageFetcher
    {
        private const int MaxPossiblePages = 10;

        private readonly string[]? forbiddenPaths;
        private readonly Mutex fetcherMutex = new();

        private readonly HashSet<string> pagesAlreadyProcessed = new();

        public PageFetcher(string[]? forbiddenPaths)
        {
            this.forbiddenPaths = forbiddenPaths;
        }

        private bool CheckUrl(string url)
        {
            if (pagesAlreadyProcessed.Count >= MaxPossiblePages)
            {
                return false;
            }

            if (pagesAlreadyProcessed.Contains(url))
            {
                return false;
            }

            if (forbiddenPaths != null)
            {
                foreach (string s in forbiddenPaths)
                {
                    Match match = Regex.Match(url, s);
                    if (match.Success)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool UrlCanBeProcessed(string url)
        {
            if (!fetcherMutex.WaitOne(2000))
            {
                Console.WriteLine("Failed to fetch lock for 'UrlCanBeProcessed'. Failing automatically...");
                return false;
            }

            bool status = CheckUrl(url);
            fetcherMutex.ReleaseMutex();

            return status;
        }

        public async Task<ProcessUrlResponse> ProcessUrl(string url, string baseUrl)
        {
            if (!fetcherMutex.WaitOne(2000))
            {
                Console.WriteLine("Failed to fetch lock for 'ProcessesUrl'. Failing automatically...");
                ProcessUrlResponse response = new(false, null, null);
                return response;
            }

            if (pagesAlreadyProcessed.Contains(url))
            {
                Console.WriteLine("Url has already been processed. This should never happen. Failing automatically...");
                ProcessUrlResponse response = new(false, null, null);
                return response;
            }

            pagesAlreadyProcessed.Add(url);
            fetcherMutex.ReleaseMutex();

            HtmlWeb webBrowser = new();
            HtmlDocument htmlDocument = await webBrowser.LoadFromWebAsync(url);
            string[] urls = GetLinksFromHtmlDocument(htmlDocument, baseUrl);

            return new(true, htmlDocument, urls);
        }

        /// <summary>
        /// Processess link to ensure it's relevant and correct for word search.
        /// </summary>
        /// <returns> Returns either a correct link with baseUrl included in it, or an empty string if not relevant.</returns>
        private static string ProcessHtmlLink(HtmlAttribute link, string baseUrl)
        {
            string value = link.Value.ToLower();
            if (value.Contains("mailto")) // Don't want links to mailboxes
            {
                return "";
            }

            
            if(value.Contains('#'))
            {
                return "";
            }
            else if (value.Contains("http") || value.Contains("www."))
            {
                if (!Regex.Match(value, baseUrl.ToLower()).Success)
                {
                    return "";
                }
            }
            else
            {
                return baseUrl + value;
            }

            return value;
        }

        private static string[] GetLinksFromHtmlDocument(HtmlDocument doc, string baseUrl)
        {
            List<string> foundUrls = new();
            foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                HtmlAttribute a = link.Attributes["href"];
                string result = ProcessHtmlLink(a, baseUrl);

                if(result != "")
                {
                    foundUrls.Add(result);
                }
            }
            return foundUrls.ToArray();
        }
    }
}
