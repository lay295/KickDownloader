using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KickDownloader
{
    public static class KickHelper
    {
        static readonly HttpClient client = new HttpClient();

        public static async Task<KickVideoResponse> FetchKickVideoResponseAsync(string videoId)
        {
            KickVideoResponse result = null;
            try
            {
                HttpResponseMessage response = await client.GetAsync("https://kickvodinfo.twitcharchives.workers.dev/" + videoId);
                response.EnsureSuccessStatusCode();
                result = await response.Content.ReadFromJsonAsync<KickVideoResponse>();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }

            return result;
        }

        public static async Task<List<string>> GetVideoParts(string sourceUrl)
        {
            string[] videoChunks = (await client.GetStringAsync(sourceUrl)).Split('\n');
            string baseUrl = sourceUrl.Substring(0, sourceUrl.LastIndexOf('/'));
            return videoChunks.Where(x => !x.StartsWith('#') && !String.IsNullOrWhiteSpace(x)).Select(x => baseUrl + "/" + x.Trim()).ToList();
        }

        public static async Task<KickClipResponse> FetchKickClipResponseAsync(string clipId)
        {
            KickClipResponse result = null;
            try
            {
                HttpResponseMessage response = await client.GetAsync("https://kickvodinfo.twitcharchives.workers.dev/" + clipId);
                response.EnsureSuccessStatusCode();
                result = await response.Content.ReadFromJsonAsync<KickClipResponse>();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }

            return result;
        }
    }
}
