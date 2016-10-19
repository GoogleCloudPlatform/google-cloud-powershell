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
        /// The user that this token corresponds to.
        /// </summary>
        public string User { get; private set; }

        /// <summary>The access token issued by the authorization server.</summary>
        public string AccessToken { get; private set; }

        /// <summary>
        /// The date and time that this token was issued.
        /// This is set in UTC time.
        /// It should be set by the CLIENT after the token was received from the server.
        /// </summary>
        public DateTime ExpiredTime { get; internal set; }

        /// <summary>
        /// Returns <c>true</c> if the token is expired or it's going to be expired in the next minute.
        /// </summary>
        public bool IsExpired
        {
            get
            {
                if (AccessToken == null)
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
        /// Construct a new token by parsing userCredentialJson.
        /// </summary>
        public ActiveUserToken(string userCredentialJson, string user)
        {
            User = user;
            JToken parsedCredentialJson = JObject.Parse(userCredentialJson);
            JToken accessTokenJson = parsedCredentialJson.SelectToken("access_token");

            if (accessTokenJson == null || accessTokenJson.Type != JTokenType.String)
            {
                throw new InvalidDataException("Credential JSON should contain access token key.");
            }

            AccessToken = accessTokenJson.Value<string>();

            JToken tokenExpiryJson = parsedCredentialJson.SelectToken("token_expiry");

            // Service account credentials do not expire.
            if (tokenExpiryJson == null || tokenExpiryJson.Type == JTokenType.Null)
            {
                ExpiredTime = DateTime.MaxValue;
            }
            else
            {
                TokenExpiry tokenExpiry = tokenExpiryJson.ToObject<TokenExpiry>();

                if (tokenExpiry == null)
                {
                    throw new InvalidDataException("Credential JSON contains an invalid token_expiry.");
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
        /// Represents a token expiry object returned from gcloud auth print-access-token.
        /// </summary>
        private class TokenExpiry
        {
            [JsonProperty("microsecond")]  internal int MicroSecond { get; set; }
            [JsonProperty("second")]       internal int Second { get; set; }
            [JsonProperty("minute")]       internal int Minute { get; set; }
            [JsonProperty("hour")]         internal int Hour { get; set; }
            [JsonProperty("day")]          internal int Day { get; set; }
            [JsonProperty("month")]        internal int Month { get; set; }
            [JsonProperty("year")]         internal int Year { get; set; }
        }
    }
}

