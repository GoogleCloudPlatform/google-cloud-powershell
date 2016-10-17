using Google.Apis.Util;
using System;

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
        private string activeUser = CloudSdkSettings.GetSettingsValue("account");

        /// <summary>Gets or sets the access token issued by the authorization server.</summary>
        [Newtonsoft.Json.JsonPropertyAttribute("access_token")]
        public string AccessToken { get; set; }

        /// <summary>Gets or sets the lifetime in seconds of the access token.</summary>
        [Newtonsoft.Json.JsonPropertyAttribute("expires_in")]
        public Nullable<long> ExpiresInSeconds { get; set; }

        /// <summary>The date and time that this token was issued. <remarks>
        /// It should be set by the CLIENT after the token was received from the server.
        /// </remarks> 
        /// </summary>
        public DateTime Issued { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the token is expired or it's going to be expired in the next minute
        /// or if the active user has changed.
        /// </summary>
        public bool IsExpired(IClock clock)
        {
            if (AccessToken == null || !ExpiresInSeconds.HasValue)
            {
                return true;
            }

            string currentAccount = CloudSdkSettings.GetSettingsValue("account");

            return Issued.AddSeconds(ExpiresInSeconds.Value - 60) <= clock.Now
                || !string.Equals(currentAccount, activeUser);
        }
    }
}
