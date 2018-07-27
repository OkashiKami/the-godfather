#region USING_DIRECTIVES
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
#endregion

namespace TheGodfather.Services.Common
{
    public class WitAiResponse
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("_text")]
        public string Text { get; set; }

        [JsonProperty("entities")]
        public Dictionary<string, List<WitAiEntity>> Entities { get; set; }
    }

    public class WitAiEntity
    {
        [JsonProperty("suggested")]
        public bool Suggested { get; set; }

        [JsonProperty("confidence")]
        public double Confidence { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}
