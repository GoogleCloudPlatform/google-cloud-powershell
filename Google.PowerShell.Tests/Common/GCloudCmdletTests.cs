// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Management.Automation;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    // TODO(chrsmith): Go for the gold and host PowerShell and use
    // real cmdlets? Is it possible to share a reference to a PSCmdlet
    // between the PowerShell environment and its host?
    [Cmdlet("Test", "GCloudCmdlets")]
    internal class FakeGCloudCmdlet : GCloudCmdlet
    {
        internal static string project = "Test-Project";

        /// <summary>
        /// This cmdlet will always report project as the static string "Test-Project".
        /// </summary>
        public override string Project => project;

        public FakeGCloudCmdlet()
        {
            ShouldThrowException = false;
            // Use the fake reporter, regardless of Cloud SDK settings.
            _telemetryReporter = new InMemoryCmdletResultReporter();
        }

        public bool ShouldThrowException { get; set; }

        protected override void ProcessRecord()
        {
            base.ProcessRecord();
            if (ShouldThrowException)
            {
                throw new InvalidOperationException("boom");
            }
        }

        public void SimulateInvocation()
        {
            BeginProcessing();
            ProcessRecord();
            EndProcessing();
        }

        public IReportCmdletResults CmdletResultReporter
        {
            get
            {
                return _telemetryReporter;
            }
        }
    }


    [TestFixture]
    public class GCloudCmdletTests
    {
        [Test]
        public void TestResultReportingSuccess()
        {
            var fakeCmdlet = new FakeGCloudCmdlet();
            var reporter = fakeCmdlet.CmdletResultReporter as InMemoryCmdletResultReporter;
            try
            {
                fakeCmdlet.SimulateInvocation();
            }
            finally
            {
                fakeCmdlet.Dispose();
            }

            Assert.IsTrue(reporter.ContainsEvent("Test-GCloudCmdlets", "Default", FakeGCloudCmdlet.project));
        }

        [Test]
        public void TestResultReportingFailure()
        {
            var fakeCmdlet = new FakeGCloudCmdlet();
            fakeCmdlet.ShouldThrowException = true;

            var reporter = fakeCmdlet.CmdletResultReporter as InMemoryCmdletResultReporter;
            Assert.Throws<InvalidOperationException>(() => fakeCmdlet.SimulateInvocation());
            fakeCmdlet.Dispose();

            Assert.IsTrue(reporter.ContainsEvent("Test-GCloudCmdlets", "Default", FakeGCloudCmdlet.project, 1));
        }
    }
}
