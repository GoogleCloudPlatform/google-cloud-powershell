// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using NUnit.Framework;
using System;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class MeasurementProtocolServiceTests
    {
        private InMemoryCmdletResultReporter _reporter;

        [OneTimeSetUp]
        public void SetUpFixture()
        {
            _reporter = new InMemoryCmdletResultReporter();
        }

        [SetUp]
        public void SetUpTest()
        {
            _reporter.Reset();
        }

        [Test]
        public void InMemoryReporterWorks()
        {
            // Test the basic case: publish events and that they can be validated.
            for (int i = 0; i < 50; i++)
            {
                string parameterSet = $"parameterSet-{i}";
                string projectNumber = $"projectNumber-{i}";
                Assert.IsFalse(_reporter.ContainsEvent("cmdlet", parameterSet, projectNumber));

                // Success case (and confirm MinValue implementation detail).
                _reporter.ReportSuccess("cmdlet", parameterSet, projectNumber);
                Assert.IsTrue(_reporter.ContainsEvent("cmdlet", parameterSet, projectNumber));
                Assert.IsTrue(_reporter.ContainsEvent("cmdlet", parameterSet, projectNumber, Int32.MinValue));

                // Failure case.
                _reporter.ReportFailure("cmdlet", parameterSet, projectNumber, i);
                Assert.IsTrue(_reporter.ContainsEvent("cmdlet", parameterSet, projectNumber, i));

                Assert.IsFalse(_reporter.ContainsEvent("cmdlet", parameterSet, projectNumber, -1));
            }
        }

        [Test]
        public void InMemoryReporterDiscardsOldData()
        {
            _reporter.ReportSuccess("cmdlet", "parameterset-alpha", "project-number");
            Assert.IsTrue(_reporter.ContainsEvent("cmdlet", "parameterset-alpha", "project-number"));

            // Confirm that old data is discarded.
            for (int i = 0; i < 50; i++)
            {
                _reporter.ReportSuccess("cmdlet", "parameterset-beta", "project-number-2");
            }
            Assert.IsFalse(_reporter.ContainsEvent("cmdlet", "parameterset-alpha", "project-number-2"));
        }
    }
}
