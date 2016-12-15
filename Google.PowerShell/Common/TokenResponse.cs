// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// OAuth 2.0 model for a successful access token response as specified in 
    /// http://tools.ietf.org/html/rfc6749#section-5.1.
    /// </summary>
    public class TokenResponse
    {
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
        /// Construct a new token by parsing activeConfigJson and get the credential.
        /// </summary>
        public TokenResponse(JToken userCredentialJson)
        {
            JToken accessTokenJson = userCredentialJson.SelectToken("access_token");
            JToken tokenExpiryJson = userCredentialJson.SelectToken("token_expiry");

            if (accessTokenJson == null || accessTokenJson.Type != JTokenType.String)
            {
                throw new InvalidDataException("Credential JSON should contain access token key.");
            }

            AccessToken = accessTokenJson.Value<string>();

            // Service account credentials do not expire.
            if (tokenExpiryJson == null || tokenExpiryJson.Type == JTokenType.Null)
            {
                ExpiredTime = DateTime.MaxValue;
            }
            else
            {
                if (tokenExpiryJson.Type != JTokenType.Date)
                {
                    throw new InvalidDataException("Credential JSON contains an invalid token_expiry.");
                }
                ExpiredTime = DateTime.SpecifyKind(tokenExpiryJson.Value<DateTime>(), DateTimeKind.Utc);
            }
        }
    }
}
