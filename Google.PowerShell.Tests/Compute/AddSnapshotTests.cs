using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.Apis.Http;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.PowerShell.Compute;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Tests.Compute
{
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
            clientServiceMock.Setup(c => c.DeserializeResponse<TResponse>(It.IsAny<HttpResponseMessage>()))
                .Returns(response);
            return requestMock;
        }

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

    public class TestableAddGceSnapshotCmdlet : AddGceSnapshotCmdlet
    {
        public static Mock<ComputeService> ComputeServiceMock { get; } = new Mock<ComputeService>();

        /// <inheritdoc />
        public override ComputeService Service { get; } = ComputeServiceMock.Object;
    }

    public class AddSnapshotTests
    {
        private RunspaceConfiguration _config;
        private Pipeline _pipeline;

        [OneTimeSetUp]
        public void BeforeAll()
        {
            _config = RunspaceConfiguration.Create();
            _config.Cmdlets.Append(
                new CmdletConfigurationEntry("Add-GceSnapshot", typeof(TestableAddGceSnapshotCmdlet), ""));
        }

        [SetUp]
        public void BeforeEach()
        {
            TestableAddGceSnapshotCmdlet.ComputeServiceMock.Reset();
            Runspace rs = RunspaceFactory.CreateRunspace(_config);
            rs.Open();
            _pipeline = rs.CreatePipeline();
        }

        [TearDown]
        public void AfterEach()
        {
            _pipeline.Dispose();
            _pipeline.Runspace.Dispose();
        }

        [Test]
        public void TestCmdlet()
        {
            var operation = new Operation
            {
                Name = "createSnapshotOperation",
                Status = "DONE",
                Id = 0,
                Description = "mock operation"
            };
            Mock<ComputeService> serviceMock = TestableAddGceSnapshotCmdlet.ComputeServiceMock;
            Mock<DisksResource.CreateSnapshotRequest> requestMock = serviceMock.Resource(s => s.Disks).SetupRequest(
                d => d.CreateSnapshot(It.IsAny<Snapshot>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Task.FromResult(operation));
            var snapshotResult = new Snapshot();
            serviceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), Task.FromResult(snapshotResult));

            _pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -Zone someZone -Project someProject");
            Collection<PSObject> results = _pipeline.Invoke();

            Assert.AreEqual(results.Single(), snapshotResult);
            requestMock.VerifySet(r => r.GuestFlush = It.IsAny<bool>(), Times.Never);
        }

        [Test]
        public void TestVss()
        {
            var operation = new Operation
            {
                Name = "createSnapshotOperation",
                Status = "DONE",
                Id = 0,
                Description = "mock operation"
            };
            Mock<ComputeService> serviceMock = TestableAddGceSnapshotCmdlet.ComputeServiceMock;
            Mock<DisksResource.CreateSnapshotRequest> requestMock = serviceMock.Resource(s => s.Disks).SetupRequest(
                d => d.CreateSnapshot(It.IsAny<Snapshot>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Task.FromResult(operation));
            var snapshotResult = new Snapshot();
            serviceMock.Resource(s => s.Snapshots).SetupRequest(
                s => s.Get(It.IsAny<string>(), It.IsAny<string>()), Task.FromResult(snapshotResult));

            _pipeline.Commands.AddScript("Add-GceSnapShot -DiskName diskname -Zone someZone -Project someProject -VSS");
            Collection<PSObject> results = _pipeline.Invoke();

            Assert.AreEqual(results.Single(), snapshotResult);
            requestMock.VerifySet(r => r.GuestFlush = true, Times.Once);
        }
    }
}
