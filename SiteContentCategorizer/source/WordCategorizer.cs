using System;
using System.Text;
using Amazon.DynamoDBv2;
using SiteContentCategorizer.source.AWS;

namespace SiteContentCategorizer.source
{
    internal class SortedCategory
    {
        public string CategoryName { get; private set; }
        public double CategoryPercentage { get; private set; } // Percentage of words that had this category out of all words on site

        public SortedCategory(string categoryName, double categoryPercentage)
        {
            CategoryName = categoryName;
            CategoryPercentage = categoryPercentage;
        }
    }

    internal class CompletedAnalysis
    {
        public string Url { get; private set; }
        public int TotalUniqueWords { get; private set; }
        public SortedCategory[] Categories { get; private set; }

        public CompletedAnalysis(string url, int totalUniqueWords, SortedCategory[] categories)
        {
            Url = url;
            TotalUniqueWords = totalUniqueWords;
            Categories = categories;
        }
    }

    internal class WordCategorizer
    {
        private const string NotPreviouslyAnalyzedCategory = "NotPreviouslyAdded";

        private static void IncreaseCategoryCountInDictionaryByOne(Dictionary<string, int> dict, string id)
        {
            if (!dict.ContainsKey(id))
            {
                dict.Add(id, 0);
            }
            dict[id]++;
        }

        private static Dictionary<string, HashSet<string>> ConstructWordCategoriesDictionary(List<FetchWebsiteResponse> websites)
        {
            Dictionary<string, HashSet<string>> Categories = new();

            foreach(FetchWebsiteResponse fetchWebsite in websites)
            {
                string category = fetchWebsite.Category.ToLower();
                if (!Categories.ContainsKey(category))
                { 
                    Categories.Add(category, new HashSet<string>());
                }

                foreach(string word in fetchWebsite.Words)
                {
                    string lowWord = word.ToLower();

                    if (!Categories[category].Contains(lowWord))
                    {
                        Categories[category].Add(lowWord);
                    }
                }
            }

            return Categories;
        }

        private static bool WordIsInCategory(Dictionary<string, HashSet<string>> dict, string word, string category)
        {
            if (!dict.ContainsKey(category))
            {
                StringBuilder sb = new();
                sb.Append("Missing category in CategoryDictionary. Category: ");
                sb.Append(category);
                sb.Append(". This should never happen!");
                Console.WriteLine(sb.ToString());
                return false;
            }

            return dict[category].Contains(word);
        }

        /// <summary>
        /// Creates a CompletedAnalysis object based on the given words.
        /// </summary>
        /// <returns> Returns a CompletedAnalysis object containing the result of the analysis.</returns>
        public static CompletedAnalysis GetCompletedAnalysis(string url, List<string> siteWords, List<FetchWebsiteResponse> websites)
        {
            Dictionary<string, HashSet<string>> foundCategories = ConstructWordCategoriesDictionary(websites);
            Dictionary<string, int> categoryAmounts = new();

            foreach(string word in siteWords)
            {
                bool foundAny = false;
                foreach(string category in foundCategories.Keys)
                {
                    if (WordIsInCategory(foundCategories, word, category))
                    {
                        foundAny = true;
                        IncreaseCategoryCountInDictionaryByOne(categoryAmounts, category);
                    }
                }

                if (!foundAny)
                {
                    IncreaseCategoryCountInDictionaryByOne(categoryAmounts, NotPreviouslyAnalyzedCategory);
                }
            }

            List<SortedCategory> categories = new();
            foreach (string key in categoryAmounts.Keys)
            {
                double categoryPercentage = categoryAmounts[key] / (double)siteWords.Count * 100; // Set percentage to be between 0 - 100 instead of 0 - 1.
                SortedCategory category = new(key, categoryPercentage);
                categories.Add(category);
            }

            SortedCategory[] sorted = categories.OrderByDescending(c => c.CategoryPercentage).ToArray();

            return new(url, siteWords.Count, sorted);
        }
    }
}
