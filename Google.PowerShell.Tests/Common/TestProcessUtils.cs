// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using NUnit.Framework;
using System.Collections.Generic;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class TestProcessUtils
    {
        /// <summary>
        /// Test that GetCommandOutput returns the $env:TMP path correctly.
        /// </summary>
        [Test]
        public void TestGetCommandOutput()
        {
            ProcessOutput commandOutput = ProcessUtils.GetCommandOutput("cmd.exe", "/c \"echo %TMP%\"").Result;

            Assert.IsTrue(commandOutput.Succeeded, "echo %TMP% command should have run successfully.");
            Assert.That(commandOutput.StandardError, Is.Not.Null.And.Not.Empty, "There should be no error.");
            Assert.IsTrue(
                Equals(commandOutput.StandardOutput.Trim(), System.Environment.GetEnvironmentVariable("TMP")),
                "GetCommandOutput for echo %TMP% should return $env:TMP path.");
        }

        /// <summary>
        /// Test that GetCommandOutput understands the environment variable passed in.
        /// </summary>
        [Test]
        public void TestGetCommandOutputWithEnvironmentVariable()
        {
            string testVariable = "TEST";
            Dictionary<string, string> environmentDictionary = new Dictionary<string, string>
                {
                    { "TESTVARIABLE", testVariable }
                };

            ProcessOutput commandOutput =
                ProcessUtils.GetCommandOutput("cmd.exe", "/c \"echo %TESTVARIABLE%\"", environmentDictionary).Result;

            Assert.IsTrue(commandOutput.Succeeded, "echo %TESTVARIABLE% command should have run successfully.");
            Assert.That(commandOutput.StandardError, Is.Not.Null.And.Not.Empty, "There should be no error.");
            Assert.IsTrue(
                Equals(commandOutput.StandardOutput.Trim(), testVariable),
                "GetCommandOutput for echo %TESTVARIABLE% should return $env:TESTVARIABLE.");
        }
    }
}
