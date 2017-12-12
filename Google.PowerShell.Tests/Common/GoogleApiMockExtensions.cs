// Copyright 2017 Google Inc. All Rights Reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Apis.Http;
using Google.Apis.Requests;
using Google.Apis.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Tests.Common
{
    /// <summary>
    /// A set of extention methods for to help when mocking google api services.
    /// </summary>
    public static class GoogleApiMockExtensions
    {
        /// <summary>
        /// Takes a mock for either a  <see cref="BaseClientService"/> or a resource and creates a mock of a child resource.
        /// </summary>
        /// <typeparam name="T">The mocked type to get a mocked child resource from.</typeparam>
        /// <typeparam name="TResource">A resource type in the mocked type.</typeparam>
        /// <param name="mock">The mock to setup the resource as a child of.</param>
        /// <param name="resourceExpression">The express that gets the resource.
        /// </param>
        public static Mock<TResource> Resource<T, TResource>(
            this Mock<T> mock,
            Expression<Func<T, TResource>> resourceExpression)
            where T : class
            where TResource : class
        {
            var resourceMock = new Mock<TResource>(Mock.Of<IClientService>());
            mock.Setup(resourceExpression).Returns(resourceMock.Object);
            return resourceMock;
        }

        /// <summary>
        /// Sets up a reqest.
        /// </summary>
        /// <typeparam name="TResource">The type of resource creating the request.</typeparam>
        /// <typeparam name="TRequest">The type of the request to make.</typeparam>
        /// <typeparam name="TResponse">The type of the responce.</typeparam>
        /// <param name="resourceMock">The mock of the resource that makes the request.</param>
        /// <param name="requestExpression">The expression of the request. Uses Moq.It functions for wildcards.</param>
        /// <param name="response">The responce the request should receive.</param>
        /// <returns>The mock of the request object. Useful for verification.</returns>
        public static Mock<TRequest> SetupRequest<TResource, TRequest, TResponse>(
            this Mock<TResource> resourceMock,
            Expression<Func<TResource, TRequest>> requestExpression,
            Task<TResponse> response)
            where TRequest : ClientServiceRequest<TResponse>
            where TResource : class
        {
            Mock<IClientService> clientServiceMock = GetClientServiceMock();
            Mock<TRequest> requestMock =
                GetRequestMock<TRequest, TResponse>(requestExpression, clientServiceMock.Object);
            resourceMock.Setup(requestExpression).Returns(requestMock.Object);
            clientServiceMock.Setup(c => c.DeserializeResponse<TResponse>(It.IsAny<HttpResponseMessage>())).Returns(response);
            return requestMock;
        }

        /// <summary>
        /// Sets up a reqest.
        /// </summary>
        /// <typeparam name="TResource">The type of resource creating the request.</typeparam>
        /// <typeparam name="TRequest">The type of the request to make.</typeparam>
        /// <typeparam name="TResponse">The type of the responce.</typeparam>
        /// <param name="resourceMock">The mock of the resource that makes the request.</param>
        /// <param name="requestExpression">The expression of the request. Uses Moq.It functions for wildcards.</param>
        /// <param name="response">The responce the request should receive.</param>
        /// <returns>The mock of the request object. Useful for verification.</returns>
        public static Mock<TRequest> SetupRequest<TResource, TRequest, TResponse>(
            this Mock<TResource> resourceMock,
            Expression<Func<TResource, TRequest>> requestExpression,
            TResponse response)
            where TRequest : ClientServiceRequest<TResponse>
            where TResource : class
        {
            return resourceMock.SetupRequest(requestExpression, Task.FromResult(response));
        }

        /// <summary>
        /// Sets up a reqest.
        /// </summary>
        /// <typeparam name="TResource">The type of resource creating the request.</typeparam>
        /// <typeparam name="TRequest">The type of the request to make.</typeparam>
        /// <typeparam name="TResponse">The type of the responce.</typeparam>
        /// <param name="resourceMock">The mock of the resource that makes the request.</param>
        /// <param name="requestExpression">The expression of the request. Uses Moq.It functions for wildcards.</param>
        /// <param name="response">A function returning responce the request should receive.</param>
        /// <returns>The mock of the request object. Useful for verification.</returns>

        public static Mock<TRequest> SetupRequest<TResource, TRequest, TResponse>(
            this Mock<TResource> resourceMock,
            Expression<Func<TResource, TRequest>> requestExpression,
            Func<Task<TResponse>> response)
            where TRequest : ClientServiceRequest<TResponse>
            where TResource : class
        {
            Mock<IClientService> clientServiceMock = GetClientServiceMock();
            Mock<TRequest> requestMock =
                GetRequestMock<TRequest, TResponse>(requestExpression, clientServiceMock.Object);
            resourceMock.Setup(requestExpression).Returns(requestMock.Object);
            clientServiceMock.Setup(c => c.DeserializeResponse<TResponse>(It.IsAny<HttpResponseMessage>()))
                .Returns(response);
            return requestMock;
        }

        private static Mock<TRequest> GetRequestMock<TRequest, TResponse>(
            LambdaExpression requestExpression,
            IClientService clientService) where TRequest : ClientServiceRequest<TResponse>
        {
            var requestMethod = requestExpression.Body as MethodCallExpression;
            if (requestMethod == null)
            {
                throw new ArgumentException(
                    $"{nameof(requestExpression)}.{nameof(requestExpression.Body)} " +
                    $"must be of type {nameof(MethodCallExpression)} " +
                    $"but was {requestExpression.Body.GetType()}");
            }

            IEnumerable<object> methodArgs =
                requestMethod.Arguments.Select(a => a.Type)
                    .Select(Expression.Default)
                    .Select(e => Expression.Convert(e, typeof(object)))
                    .Select(e => Expression.Lambda<Func<object>>(e).Compile()());
            object[] constructorArgs = new[] { clientService }.Concat(methodArgs).ToArray();
            var requestMock = new Mock<TRequest>(constructorArgs)
            {
                CallBase = true
            };

            requestMock.Setup(r => r.RestPath).Returns("/");
            requestMock.Object.RequestParameters.Clear();
            return requestMock;
        }

        private static Mock<IClientService> GetClientServiceMock()
        {
            var clientServiceMock = new Mock<IClientService>();
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            // Use MockBehavior.Strict to ensure we make no acutal http requests.
            var configurableHandlerMock =
                new Mock<ConfigurableMessageHandler>(MockBehavior.Strict, handlerMock.Object);
            var clientMock = new Mock<ConfigurableHttpClient>(MockBehavior.Strict, configurableHandlerMock.Object);
            clientMock.Setup(c => c.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new HttpResponseMessage()));

            clientServiceMock.Setup(c => c.BaseUri).Returns("https://mock.uri");
            clientServiceMock.Setup(c => c.HttpClient).Returns(clientMock.Object);
            clientServiceMock.Setup(c => c.Serializer.Format).Returns("json");
            clientServiceMock.Setup(c => c.SerializeObject(It.IsAny<object>())).Returns("{}");

            return clientServiceMock;
        }
    }
}