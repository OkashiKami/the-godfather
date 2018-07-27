#region USING_DIRECTIVES
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TheGodfather.Services.Common;
#endregion

namespace TheGodfather.Services
{
    internal class WitAiService : ITheGodfatherService
    {
        private static readonly string _url = $"https://api.wit.ai/message?v=20180727";
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        private static readonly Regex _mentionRegex = new Regex(@"<((@(!|&)?)|#)(?<id>[0-9]+)>", RegexOptions.Compiled);


        public WitAiService(string key)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }


        public async Task<WitAiResponse> ProcessMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message missing!", "message");

            message = _mentionRegex.Replace(message, "%id%");

            string response = await _http.GetStringAsync($"{_url}&q={WebUtility.UrlEncode(message)}").ConfigureAwait(false);
            var data = JsonConvert.DeserializeObject<WitAiResponse>(response);
            return data;
        }

        public string CreateCommandAndArguments(WitAiResponse data, IReadOnlyList<DiscordUser> umentions, 
            IReadOnlyList<DiscordChannel> cmentions, IReadOnlyList<DiscordRole> rmentions)
        {
            if (!data.Entities.ContainsKey("intent") || data.Entities["intent"].FirstOrDefault()?.Confidence < 0.9)
                return null;

            string cmd;
            switch (data.Entities["intent"].FirstOrDefault()?.Value) {
                case "prefix_set":
                    if (!data.Entities.ContainsKey("prefix"))
                        cmd = null;
                    else
                        cmd = $"prefix {data.Entities["prefix"].FirstOrDefault()?.Value}";
                    break;
                case "user_kick":
                    if (umentions.Any())
                        cmd = $"user kick {string.Join(" ", umentions.Select(u => u.Id))}";
                    else
                        cmd = null;
                    break;
                default:
                    cmd = null; break;
            }

            return cmd;
        }
    }
}
