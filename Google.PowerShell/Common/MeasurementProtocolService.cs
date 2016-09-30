// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using GoogleAnalyticsUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Interface for interacting with the Measurement Protocol service. See concrete
    /// implementations for details.
    /// </summary>
    public interface IReportCmdletResults
    {
        /// <summary>
        /// Report a successful cmdlet invocation. "Expected" errors are considered a success.
        /// For example, Test-XXX should report success even if the existance test fails.
        /// </summary>
        void ReportSuccess(string cmdletName, string parameterSet);

        /// <summary>
        /// Report a cmdlet failing. Failure is defined as any abnormal termination, such as
        /// a runtime exception, user-cancelation, etc.
        /// </summary>
        /// <param name="cmdletName">Name of the cmdlet that failed.</param>
        /// <param name="parameterSet">Name of the prameter set the cmdlet was running.</param>
        /// <param name="errorCode">Return the HTTP error code as applicable, otherwise use non-zero.</param>
        void ReportFailure(string cmdletName, string parameterSet, int errorCode);
    }

    /// <summary>
    /// Fake implementation of IReportCmdletResults for unit testing. This will also be used in
    /// production for users who have opted-out of sending analytics data to Google. (Read:
    /// performance matters.)
    /// </summary>
    public class InMemoryCmdletResultReporter : IReportCmdletResults
    {
        /// <summary>
        /// IMPORTANT: We rely on ValueType.Equals for structural equality later. If
        ///  you make this a class you will need to overwrite Equals and GetHashCode.
        /// </summary>
        protected struct EventRecord
        {
            public string cmdletName;
            public string parameterSet;
            // MIN_INT will be used to denote the "success"/null case (instead of using Nullable).
            public int errorCode;

            public static EventRecord Create(string cmdletName, string parameterSet, int errorCode)
            {
                return new EventRecord
                {
                    cmdletName = cmdletName,
                    parameterSet = parameterSet,
                    errorCode = errorCode
                };
            }
        }

        /// <summary>
        /// Keep kMaxEvents stored in memory, for checking later. After kMaxEvents have
        /// been recorded, the oldest events will get overwritten.
        /// </summary>
        private const int kMaxEvents = 16;
        private int _eventsRecorded;
        private EventRecord[] _events;

        public InMemoryCmdletResultReporter()
        {
            Reset();
        }

        public void ReportSuccess(string cmdletName, string parameterSet)
        {
            Report(EventRecord.Create(cmdletName, parameterSet, Int32.MinValue));
        }

        public void ReportFailure(string cmdletName, string parameterSet, int errorCode)
        {
            Report(EventRecord.Create(cmdletName, parameterSet, errorCode));
        }

        protected void Report(EventRecord record)
        {
            // Overwrite older events as if it were a circular buffer.
            _events[_eventsRecorded % kMaxEvents] = record;
            _eventsRecorded++;
        }

        /// <summary>
        /// Clears all history of events.
        /// </summary>
        public void Reset()
        {
            _eventsRecorded = 0;
            _events = new EventRecord[kMaxEvents];
        }

        /// <summary>
        /// Returns whether or not an event with the given makeup has been recorded.
        /// </summary>
        public bool ContainsEvent(string cmdletName, string parameterSet, int errorCode = Int32.MinValue)
        {
            var expectedRecord = EventRecord.Create(cmdletName, parameterSet, errorCode);
            for (int i = 0; i < _events.Length; i++)
            {
                if (_events[i].Equals(expectedRecord))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Type of analytic event.
    /// </summary>
    public enum AnalyticsEventCategory
    {
        CmdletInvocation,
        Provider
    }

    /// <summary>
    /// Reports PowerShell cmdlet results to Google Analytics.
    /// </summary>
    public class GoogleAnalyticsCmdletReporter : IReportCmdletResults
    {
        // Google Analytics Property ID, see di-metrics.
        private const string PropertyId = "UA-36037335-1";
        // Application name to report to GA.
        private const string ApplicationName = "google-cloud-powershell";
        // Version of the Google.PowerShell assembly.
        private static readonly Lazy<string> s_appVersion = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetName().Version.ToString());

        /// <summary>
        /// Analytics reporter object.
        /// </summary>
        private Lazy<AnalyticsReporter> _reporter = null;

        /// <summary>
        /// Category for all analytic events reported through this instance.
        /// </summary>
        private AnalyticsEventCategory _category = AnalyticsEventCategory.CmdletInvocation;

        public GoogleAnalyticsCmdletReporter(string clientID, AnalyticsEventCategory analyticsCategory)
        {
            // We report results to Google Analytics if the user has opted-into Cloud SDK metric reporting. However, we
            // don't want to report data during development. (e.g. when running unit tests.)
            string envVar = Environment.GetEnvironmentVariable("DISABLE_POWERSHELL_ANALYTICS");
            bool debugFlag = (envVar == "TRUE");

            _category = analyticsCategory;
            _reporter = new Lazy<AnalyticsReporter>(() =>
            {
                return new AnalyticsReporter(PropertyId,
                    clientId: clientID,
                    appName: ApplicationName,
                    appVersion: s_appVersion.Value,
                    debug: debugFlag);
            });
        }

        public void ReportSuccess(string cmdletName, string parameterSet)
        {
            Report(_category, cmdletName, parameterSet, null);
        }

        public void ReportFailure(string cmdletName, string parameterSet, int errorCode)
        {
            Report(_category, cmdletName, parameterSet, errorCode);
        }

        private void Report(AnalyticsEventCategory category, string cmdletName, string parameterSet, int? errorCode)
        {
            _reporter.Value?.ReportEvent(
                // e.g. CmdletInvocation, Provider
                category: category.ToString(),
                // e.g. New-GcsBucket, ConvertTo-GcSqlInstance, Get-ChildItem
                action: cmdletName,
                // e.g. FromString, FromFile
                label: parameterSet,
                // null (success), 403, 404
                value: errorCode);
        }
    }
}
