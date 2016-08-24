// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Management.Automation;
using Google.PowerShell.Common;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Common.ComputeEngine
{
    internal class TestType
    {
        public string Name { get; set; }
    }

    [TestFixture]
    public class PropertyByTypeTransformationAttributeTest
    {
        private static string s_testString = "testString";

        [Test]
        public void TestStringPassThrough()
        {
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", TypeToTransform = typeof(TestType) };
            object result = attribute.Transform(null, s_testString);
            Assert.AreEqual(result, s_testString);
        }

        [Test]
        public void TestProjectConversion()
        {
            TestType project = new TestType { Name = s_testString };
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", TypeToTransform = typeof(TestType) };
            object result = attribute.Transform(null, project);
            Assert.AreEqual(s_testString, result);
        }

        [Test]
        public void TestPSObjectConversion()
        {
            TestType project = new TestType { Name = s_testString };
            PSObject obj = new PSObject(project);
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", TypeToTransform = typeof(TestType) };
            object result = attribute.Transform(null, obj);
            Assert.AreEqual(result, s_testString);
        }

        [Test]
        public void TestDeepPSObjectConversion()
        {
            TestType project = new TestType { Name = s_testString };
            PSObject obj = new PSObject(project);
            var attribute = new PropertyByTypeTransformationAttribute { Property = "name", TypeToTransform = typeof(TestType) };
            object result = attribute.Transform(null, new PSObject(obj));
            Assert.AreEqual(result, s_testString);
        }
    }
}
