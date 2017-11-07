using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Tests.Common;
using Moq;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Compute
{
    [TestFixture]
    public class GceCmdletTestBase : PsCmdletTestBase
    {
        protected readonly Operation DoneOperation = new Operation
        {
            Name = "doneOperation",
            Status = "DONE",
            Id = 0,
            Description = "mock operation"
        };

        public static Mock<ComputeService> ServiceMock { get; } = new Mock<ComputeService>();

        [SetUp]
        public new void BeforeEach()
        {
            ServiceMock.Reset();
        }
    }
}