namespace Zu.ChromeDevTools.Emulation
{
    using Newtonsoft.Json;

    /// <summary>
    /// Used to specify User Agent Cient Hints to emulate. See https://wicg.github.io/ua-client-hints Missing optional values will be filled in by the target with what it would normally use.
    /// </summary>
    public class UserAgentMetadata
    {
        [JsonProperty("brands", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public UserAgentBrandVersion[] Brands
        {
            get; 
            set;
        }
        [JsonProperty("fullVersion", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FullVersion
        {
            get; 
            set;
        }
        [JsonProperty("platform")]
        public string Platform
        {
            get; 
            set;
        }
        [JsonProperty("platformVersion")]
        public string PlatformVersion
        {
            get; 
            set;
        }
        [JsonProperty("architecture")]
        public string Architecture
        {
            get; 
            set;
        }
        [JsonProperty("model")]
        public string Model
        {
            get; 
            set;
        }
        [JsonProperty("mobile")]
        public bool Mobile
        {
            get; 
            set;
        }
    }
}