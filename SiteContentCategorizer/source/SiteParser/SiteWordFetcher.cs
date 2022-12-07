using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SiteContentCategorizer.source.SiteParser
{
    internal class GetAllUniqueWordsFromWebsiteResponse
    {
        public bool Successful { get; private set; }
        public List<string> FoundWords { get; private set; }
        public string ErrorMessage { get; private set; }

        public GetAllUniqueWordsFromWebsiteResponse(List<string> foundWords)
        {
            Successful = true;
            FoundWords = foundWords;
            ErrorMessage = "";
        }

        public GetAllUniqueWordsFromWebsiteResponse(string errorMessage)
        {
            Successful = false;
            FoundWords = new();
            ErrorMessage = errorMessage;
        }
    }

    internal class SiteWordFetcher
    {
        /// <summary>
        /// Fetches and proccess the Robots.txt file to get disallowed regex paths for generic webcrawlers
        /// </summary>
        /// <returns> Returns the forbidden paths. If no file found or nothing is disallowed, returns empty array.</returns>
        private async static Task<string[]> FetchForbiddenPathsFromRobotsFile(string site)
        {
            string file = "invalid";
            string url = site.EndsWith('/') ? site.Remove(0, site.Length - 2) : site;
            string robotsUrl = url + "/robots.txt";

            try
            {
                HttpClient webClient = new();
                Stream fileStream = await webClient.GetStreamAsync(robotsUrl);
                using (StreamReader sr = new(fileStream))
                {
                    file = sr.ReadToEnd();
                }
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
                return Array.Empty<string>();
            }

            List<string> disallowedPaths = new();
            string[] lines = file.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            bool relevantUser = false;
            foreach (string line in lines)
            {
                if (line.ToLower().Contains("user-agent"))
                {
                    relevantUser = line.ToLower().Equals("user-agent: *");
                    continue;
                }
                else if (!relevantUser)
                {
                    continue;
                }

                if (line.ToLower().Contains("disallow: "))
                {
                    string disAllow = line.Split(" ")[1];

                    StringBuilder sb = new();
                    sb.Append(url);
                    sb.Append(disAllow.Replace("*", "").Replace("?", ""));

                    disallowedPaths.Add(sb.ToString());
                }
            }

            return disallowedPaths.ToArray();
        }

        /// <summary>
        /// Fetches HTML documents from the given website and proccess the documents to find unique words to analyze. 
        /// </summary>
        /// <returns> Returns all distinct words found on the website. Returns error message if no words were possible to be found. </returns>
        public async static Task<GetAllUniqueWordsFromWebsiteResponse> GetAllUniqueWordsFromWebsite(string siteUrl)
        {
            string[] fetchTask = await FetchForbiddenPathsFromRobotsFile(siteUrl);
            WebsiteCrawler wc = new(siteUrl, fetchTask);
            FetchHtmlsFromWebsiteResponse response = wc.FetchHtmlsFromWebsite();

            if (!response.Successful)
            {
                return new(response.ErrorMessage);
            }

            List<string> words = new();
            response.Documents.ForEach(doc =>
            {
                
                foreach (HtmlNode node in doc.DocumentNode.DescendantsAndSelf())
                {
                    if (node.InnerText != "" && node.NodeType == HtmlNodeType.Text)
                    {
                        string text = node.InnerText;
                        string[] splitString = text.Replace("\n", "").Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        foreach(string word in splitString)
                        {
                            string lowWord = word.ToLower();

                            // Remove strings that have numbers, special symbols, not contains any letters or is too long (This to remove script code from page)
                            if (Regex.Matches(lowWord, @"[0-9]").Count > 0 || Regex.Matches(lowWord, @"[_#*]").Count > 0 || Regex.Matches(lowWord, @"[a-zåäö]").Count == 0 || lowWord.Length > 20)
                            {
                                continue;
                            }
                            string fixedWord = Regex.Replace(lowWord, "[^a-zåäö]+", "", RegexOptions.Compiled);
                            words.Add(fixedWord);
                        }
                    }
                }
            });

            if(words.Count == 0)
            {
                StringBuilder sb = new();
                sb.Append("No words were found for website: ");
                sb.Append(wc.BaseUrl);
                sb.Append(". This can be because robots.txt forbids all relevant paths or the URl might be incorrect. Try another site or check if url is damaged.");
                string message = sb.ToString();

                Console.WriteLine(message);
                return new(message);
            }

            return new(words.Distinct().ToList());
        }
    }
}
