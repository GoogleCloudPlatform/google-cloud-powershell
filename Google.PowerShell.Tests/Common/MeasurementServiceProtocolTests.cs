// Copyright 2015 Google Inc. All Rights Reserved.
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
        private FakeCmdletResultReporter _fakeReporter;

        [TestFixtureSetUp]
        public void SetUpFixture()
        {
            _fakeReporter = new FakeCmdletResultReporter();
        }

        [SetUp]
        public void SetUpTest()
        {
            _fakeReporter.Reset();
        }

        [Test]
        public void TheFakeWorks()
        {
            // Test the basic case: publish events and that they can be validated.
            for (int i = 0; i < 50; i++)
            {
                string parameterSet = String.Format("parameterSet-{0}", i);
                Assert.IsFalse(_fakeReporter.ContainsEvent("cmdlet", parameterSet));

                // Success case (and confirm MinValue implementation detail).
                _fakeReporter.ReportSuccess("cmdlet", parameterSet);
                Assert.IsTrue(_fakeReporter.ContainsEvent("cmdlet", parameterSet));
                Assert.IsTrue(_fakeReporter.ContainsEvent("cmdlet", parameterSet, Int32.MinValue));

                // Failure case.
                _fakeReporter.ReportFailure("cmdlet", parameterSet, i);
                Assert.IsTrue(_fakeReporter.ContainsEvent("cmdlet", parameterSet, i));

                Assert.IsFalse(_fakeReporter.ContainsEvent("cmdlet", parameterSet, -1));
            }
        }

        [Test]
        public void TheFakeDiscardsOldData()
        {
            _fakeReporter.ReportSuccess("cmdlet", "parameterset-alpha");
            Assert.IsTrue(_fakeReporter.ContainsEvent("cmdlet", "parameterset-alpha"));

            // Confirm that old data is discarded.
            for (int i = 0; i < 50; i++)
            {
                _fakeReporter.ReportSuccess("cmdlet", "parameterset-beta");
            }
            Assert.IsFalse(_fakeReporter.ContainsEvent("cmdlet", "parameterset-alpha"));
        }
    }
}