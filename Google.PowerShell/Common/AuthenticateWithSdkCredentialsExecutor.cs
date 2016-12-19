/*
Copyright 2015-2016 Google Inc. All Rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// OAuth 2.0 credential for accessing protected resources using an access token.
    /// This class will get the access token from "gcloud auth print-access-token".
    /// </summary>
    public class AuthenticateWithSdkCredentialsExecutor : ICredential, IHttpExecuteInterceptor, IHttpUnsuccessfulResponseHandler
    {
        /// <summary>
        /// Returns the authorization code flow. 
        /// This is used to revoke token (in RevokeTokenAsync)
        /// and intercept request with the access token (in InterceptAsync).
        /// </summary>
        private IAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer()
            {
                ClientSecrets = new ClientSecrets()
                {
                    ClientId = "clientId",
                    ClientSecret = "clientSecrets"
                }
            });

        #region IHttpExecuteInterceptor

        /// <summary>
        /// Default implementation is to try to refresh the access token if there is no access token or if we are 1 
        /// minute away from expiration. If token server is unavailable, it will try to use the access token even if 
        /// has expired. If successful, it will call <see cref="IAccessMethod.Intercept"/>.
        /// </summary>
        public async Task InterceptAsync(HttpRequestMessage request, CancellationToken taskCancellationToken)
        {
            Task<string> getAccessTokenTask = GetAccessTokenForRequestAsync(request.RequestUri.ToString(), taskCancellationToken);
            string accessToken = await getAccessTokenTask.ConfigureAwait(false);
            flow.AccessMethod.Intercept(request, accessToken);
        }

        #endregion

        #region IHttpUnsuccessfulResponseHandler

        /// <summary>
        /// Handles an abnormal response when sending a HTTP request.
        /// A simple rule must be followed, if you modify the request object in a way that the abnormal response can
        /// be resolved, you must return <c>true</c>.
        /// </summary>
        public async Task<bool> HandleResponseAsync(HandleUnsuccessfulResponseArgs args)
        {
            if (args.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return await RefreshTokenAsync(args.CancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        #endregion

        #region IConfigurableHttpClientInitializer

        public void Initialize(ConfigurableHttpClient httpClient)
        {
            httpClient.MessageHandler.AddExecuteInterceptor(this);
            httpClient.MessageHandler.AddUnsuccessfulResponseHandler(this);
        }

        #endregion

        #region ITokenAccess implementation

        public virtual async Task<string> GetAccessTokenForRequestAsync(string authUri = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            TokenResponse token = await ActiveUserConfig.GetActiveUserToken(cancellationToken);
            return token.AccessToken;
        }

        #endregion

        /// <summary>
        /// Refreshes the token by calling to ActiveUserConfig.GetActiveUserToken
        /// </summary>
        public async Task<bool> RefreshTokenAsync(CancellationToken taskCancellationToken)
        {
            TokenResponse userToken = await ActiveUserConfig.GetActiveUserToken(taskCancellationToken, refresh: true);
            if (userToken != null && userToken.AccessToken != null)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asynchronously revokes the token by calling
        /// <see cref="Google.Apis.Auth.OAuth2.Flows.IAuthorizationCodeFlow.RevokeTokenAsync"/>.
        /// </summary>
        /// <param name="taskCancellationToken">Cancellation token to cancel an operation.</param>
        /// <returns><c>true</c> if the token was revoked successfully.</returns>
        public async Task<bool> RevokeTokenAsync(CancellationToken taskCancellationToken)
        {
            TokenResponse userToken = await ActiveUserConfig.GetActiveUserToken(taskCancellationToken);

            await flow.RevokeTokenAsync("userId", userToken.AccessToken, taskCancellationToken).ConfigureAwait(false);
            // We don't set the token to null, cause we want that the next request (without reauthorizing) will fail).
            return true;
        }
    }
}
