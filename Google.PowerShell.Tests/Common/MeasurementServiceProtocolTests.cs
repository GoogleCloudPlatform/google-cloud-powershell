// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class MeasurementProtocolServiceTests
    {
        private InMemoryCmdletResultReporter _reporter;

        [TestFixtureSetUp]
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
                string parameterSet = String.Format("parameterSet-{0}", i);
                Assert.IsFalse(_reporter.ContainsEvent("cmdlet", parameterSet));

                // Success case (and confirm MinValue implementation detail).
                _reporter.ReportSuccess("cmdlet", parameterSet);
                Assert.IsTrue(_reporter.ContainsEvent("cmdlet", parameterSet));
                Assert.IsTrue(_reporter.ContainsEvent("cmdlet", parameterSet, Int32.MinValue));

                // Failure case.
                _reporter.ReportFailure("cmdlet", parameterSet, i);
                Assert.IsTrue(_reporter.ContainsEvent("cmdlet", parameterSet, i));

                Assert.IsFalse(_reporter.ContainsEvent("cmdlet", parameterSet, -1));
            }
        }

        [Test]
        public void InMemoryReporterDiscardsOldData()
        {
            _reporter.ReportSuccess("cmdlet", "parameterset-alpha");
            Assert.IsTrue(_reporter.ContainsEvent("cmdlet", "parameterset-alpha"));

            // Confirm that old data is discarded.
            for (int i = 0; i < 50; i++)
            {
                _reporter.ReportSuccess("cmdlet", "parameterset-beta");
            }
            Assert.IsFalse(_reporter.ContainsEvent("cmdlet", "parameterset-alpha"));
        }
    }
}
