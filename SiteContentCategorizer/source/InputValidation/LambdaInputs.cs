using Newtonsoft.Json;
using System;

namespace SiteContentCategorizer.source.InputValidation
{
    internal class StartWebsiteAnalysisInput
    {
        public string SiteUrl = "";

        [JsonConstructor]
        public StartWebsiteAnalysisInput(string siteUrl)
        {
            SiteUrl = siteUrl;
        }
    }

    internal class UploadWebsiteCategoryInput
    {
        public string SiteUrl;
        public string Category;

        [JsonConstructor]
        public UploadWebsiteCategoryInput(string siteUrl, string category)
        {
            SiteUrl = siteUrl;
            Category = category;
        }       
    }
}
