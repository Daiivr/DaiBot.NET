using Newtonsoft.Json;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysBot.Pokemon.WinForms
{
    public class UpdateChecker
    {
        private const string RepositoryOwner = "Daiivr";
        private const string RepositoryName = "DaiBot.NET";

        public static async Task<(bool UpdateAvailable, bool UpdateRequired, string NewVersion)> CheckForUpdatesAsync(bool showUpdateForm = false)
        {
            ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();

            bool updateAvailable = latestRelease != null && latestRelease.TagName != TradeBot.Version;
#pragma warning disable CS8604 // Possible null reference argument.
            bool updateRequired = latestRelease?.Prerelease == false && IsUpdateRequired(latestRelease.Body);
#pragma warning restore CS8604 // Possible null reference argument.
            string? newVersion = latestRelease?.TagName;

            if (updateAvailable && showUpdateForm)
            {
#pragma warning disable CS8604 // Possible null reference argument.
                UpdateForm updateForm = new(updateRequired, newVersion);
#pragma warning restore CS8604 // Possible null reference argument.
                updateForm.ShowDialog();
            }

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            return (updateAvailable, updateRequired, newVersion);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
        }


        public static async Task<string> FetchChangelogAsync()
        {
            ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();

            if (latestRelease == null)
                return "No se pudo recuperar la información de la versión más reciente.";

#pragma warning disable CS8603 // Possible null reference return.
            return latestRelease.Body;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public static async Task<string?> FetchDownloadUrlAsync()
        {
            ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();

            if (latestRelease == null)
                return null;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.
            string? downloadUrl = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return downloadUrl;
        }

        private static async Task<ReleaseInfo?> FetchLatestReleaseAsync()
        {
            using var client = new HttpClient();
            try
            {
                // Add a custom header to identify the request
                client.DefaultRequestHeaders.Add("User-Agent", "DaiBot.NET");

                string releasesUrl = $"http://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
                HttpResponseMessage response = await client.GetAsync(releasesUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                ReleaseInfo? release = JsonConvert.DeserializeObject<ReleaseInfo>(jsonContent);

                return release;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsUpdateRequired(string changelogBody)
        {
            return !string.IsNullOrWhiteSpace(changelogBody) &&
                   changelogBody.Contains("Required = Yes", StringComparison.OrdinalIgnoreCase);
        }

        private class ReleaseInfo
        {
            [JsonProperty("tag_name")]
            public string? TagName { get; set; }

            [JsonProperty("prerelease")]
            public bool Prerelease { get; set; }

            [JsonProperty("assets")]
            public List<AssetInfo>? Assets { get; set; }

            [JsonProperty("body")]
            public string? Body { get; set; }
        }

        private class AssetInfo
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}
