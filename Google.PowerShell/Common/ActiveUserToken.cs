using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// OAuth 2.0 model for a successful access token response as specified in 
    /// http://tools.ietf.org/html/rfc6749#section-5.1.
    /// </summary>
    public class ActiveUserToken
    {
        /// <summary>
        /// The current active user that corresponds to this token.
        /// </summary>
        private string activeUser;

        /// <summary>
        /// This function will return the current active user.
        /// </summary>
        internal Func<string> getActiveUser;

        /// <summary>The access token issued by the authorization server.</summary>
        public string AccessToken { get; internal set; }

        /// <summary>
        /// The date and time that this token was issued.
        /// This is set in UTC time.
        /// It should be set by the CLIENT after the token was received from the server.
        /// </summary>
        public DateTime ExpiredTime { get; internal set; }

        /// <summary>
        /// Constructs a new token by parsing the userTokenJson.
        /// GetActiveUser will be using CloudSdkSettings.GetSettingsValue("account").
        /// </summary>
        public ActiveUserToken(string userTokenJson) : this(userTokenJson, () => CloudSdkSettings.GetSettingsValue("account"))
        {
        }

        /// <summary>
        /// Construct a new token by parsing the userTokenJson.
        /// The activeUser will be computed by calling getActiveUser.
        /// </summary>
        public ActiveUserToken(string userCredentialJson, Func<string> getActiveUser)
        {
            this.getActiveUser = getActiveUser;
            activeUser = this.getActiveUser();

            JToken parsedCredentialJson = JObject.Parse(userCredentialJson);
            JToken accessTokenJson = parsedCredentialJson.SelectToken("access_token");

            if (accessTokenJson == null || accessTokenJson.Type != JTokenType.String)
            {
                throw new InvalidDataException("Credential Json should contain access token key.");
            }

            AccessToken = accessTokenJson.Value<string>();

            JToken tokenExpiryJson = parsedCredentialJson.SelectToken("token_expiry");

            // Token from GCE does not have expiry.
            if (tokenExpiryJson == null || tokenExpiryJson.Type == JTokenType.Null)
            {
                ExpiredTime = DateTime.MaxValue;
            }
            else
            {
                TokenExpiry tokenExpiry = tokenExpiryJson.ToObject<TokenExpiry>();

                if (tokenExpiry == null)
                {
                    throw new InvalidDataException("Credential Json contains an invalid token_expiry.");
                }

                ExpiredTime = new DateTime(
                    tokenExpiry.Year,
                    tokenExpiry.Month,
                    tokenExpiry.Day,
                    tokenExpiry.Hour,
                    tokenExpiry.Minute,
                    tokenExpiry.Second,
                    tokenExpiry.MicroSecond/1000,
                    DateTimeKind.Utc);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the token is expired or it's going to be expired in the next minute
        /// or if the active user has changed.
        /// </summary>
        public bool IsExpiredOrInvalid()
        {
            if (AccessToken == null)
            {
                return true;
            }

            string currentAccount = getActiveUser();
            bool activeUserChanged = !string.Equals(currentAccount, activeUser);

            if (activeUserChanged)
            {
                return true;
            }

            if (ExpiredTime == DateTime.MaxValue)
            {
                return false;
            }

            return ExpiredTime.AddSeconds(-60) <= DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Represents a token expiry object returned from gcloud auth print-access-token.
    /// </summary>
    internal class TokenExpiry
    {
        [JsonProperty("microsecond")]
        internal int MicroSecond { get; set; }

        [JsonProperty("second")]
        internal int Second { get; set; }

        [JsonProperty("minute")]
        internal int Minute { get; set; }

        [JsonProperty("hour")]
        internal int Hour { get; set; }

        [JsonProperty("day")]
        internal int Day { get; set; }

        [JsonProperty("month")]
        internal int Month { get; set; }

        [JsonProperty("year")]
        internal int Year { get; set; }
    }
}

