using Google.PowerShell.Compute;
using NUnit.Framework;
using System.Management.Automation;

namespace Google.PowerShell.Tests.Common.Compute
{
    class TestType
    {
        public string Name { get; set; }
    }

    [TestFixture]
    public class PropertyByTypeTransformationAttributeTest
    {
        private static string testString = "testString";

        [Test]
        public void TestStringPassThrough()
        {
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", Type = typeof(TestType) };
            object result = attribute.Transform(null, testString);
            Assert.AreEqual(result, testString);
        }

        [Test]
        public void TestProjectConversion()
        {
            TestType project = new TestType { Name = testString };
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", Type = typeof(TestType) };
            object result = attribute.Transform(null, project);
            Assert.AreEqual(testString,result );
        }

        [Test]
        public void TestPSObjectConversion()
        {
            TestType project = new TestType { Name = testString };
            PSObject obj = new PSObject(project);
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", Type = typeof(TestType) };
            object result = attribute.Transform(null, obj);
            Assert.AreEqual(result, testString);
        }

        [Test]
        public void TestDeepPSObjectConversion()
        {
            TestType project = new TestType { Name = testString };
            PSObject obj = new PSObject(project);
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", Type = typeof(TestType) };
            object result = attribute.Transform(null, new PSObject(obj));
            Assert.AreEqual(result, testString);
        }
    }
}
