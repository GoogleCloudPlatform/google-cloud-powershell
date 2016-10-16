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

        /// <summary>
        /// Gets or sets the token type as specified in http://tools.ietf.org/html/rfc6749#section-7.1.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("token_type")]
        public string TokenType { get; set; }

        /// <summary>Gets or sets the lifetime in seconds of the access token.</summary>
        [Newtonsoft.Json.JsonPropertyAttribute("expires_in")]
        public Nullable<long> ExpiresInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the refresh token which can be used to obtain a new access token.
        /// For example, the value "3600" denotes that the access token will expire in one hour from the time the 
        /// response was generated.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("refresh_token")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the scope of the access token as specified in http://tools.ietf.org/html/rfc6749#section-3.3.
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("scope")]
        public string Scope { get; set; }

        /// <summary>
        /// Gets or sets the id_token, which is a JSON Web Token (JWT) as specified in http://tools.ietf.org/html/draft-ietf-oauth-json-web-token
        /// </summary>
        [Newtonsoft.Json.JsonPropertyAttribute("id_token")]
        public string IdToken { get; set; }

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
