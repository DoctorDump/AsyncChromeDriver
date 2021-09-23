namespace Zu.ChromeDevTools.Emulation
{
    using Newtonsoft.Json;

    /// <summary>
    /// Used to specify User Agent Cient Hints to emulate. See https://wicg.github.io/ua-client-hints
    /// </summary>
    public class UserAgentBrandVersion
    {
        [JsonProperty("brand", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Brand
        {
            get;
            set;
        }
        [JsonProperty("version", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Version
        {
            get;
            set;
        }
    }
}