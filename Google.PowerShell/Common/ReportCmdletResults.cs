// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Reflection;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Interface for reporting cmdlet invocations and results to analytic services. See concrete
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
    /// performance matters, it can't just store everything in memory.)
    /// </summary>
    public class InMemoryCmdletResultReporter : IReportCmdletResults
    {
        /// <summary>
        /// IMPORTANT: We rely on ValueType.Equals for structural equality later. If
        /// you make this a class you will need to overwrite Equals and GetHashCode.
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
        ProviderInvocation
    }

    /// <summary>
    /// Reports PowerShell cmdlet results to Google Analytics.
    /// </summary>
    public class GoogleAnalyticsCmdletReporter : IReportCmdletResults
    {
        // Google Analytics Property ID.
        private const string PropertyId = "UA-36037335-1";
        // Application name to report to GA.
        private const string ApplicationName = "google-cloud-powershell";
        // Version of the Google.PowerShell assembly.
        private static readonly Lazy<string> s_appVersion =
            new Lazy<string>(() => typeof(GoogleAnalyticsCmdletReporter).GetTypeInfo().Assembly.GetName().Version.ToString());

        /// <summary>
        /// Singleton instance of the event reporter object, wrapping the actual calls to Google Analytics.
        /// </summary>
        private static Lazy<EventsReporter> s_reporter = null;

        /// <summary>
        /// Category for all analytic events reported through this instance.
        /// </summary>
        private AnalyticsEventCategory _category = AnalyticsEventCategory.CmdletInvocation;

        public GoogleAnalyticsCmdletReporter(string clientID, AnalyticsEventCategory analyticsCategory)
        {
            // We report results to Google Analytics if the user has opted-into Cloud SDK metric reporting. However, we
            // don't want to report data during development. (e.g. when running unit tests.) The data is still sent, but
            // only to a debug server.
            string envVar = Environment.GetEnvironmentVariable("DISABLE_POWERSHELL_ANALYTICS");
            bool disableReporting = !String.IsNullOrWhiteSpace(envVar);

            _category = analyticsCategory;
            if (s_reporter == null)
            {
                // BUG: We only forward the clientID when the AnalyticsReporter is constructed the first time.
                // If the user opts-out and then opts-into analytics reporting, their client ID will change,
                // but we will keep using the same cached version. Will persist until the GoogleCloud PowerShell
                // module is unloaded, i.e. until the PowerShell session terminates.
                s_reporter = new Lazy<EventsReporter>(() =>
                {
                    var analyticsReporter = new AnalyticsReporter(
                        PropertyId,
                        clientId: clientID,
                        appName: ApplicationName,
                        appVersion: s_appVersion.Value,
                        debug: disableReporting);

                    return new EventsReporter(analyticsReporter);
                });
            }
        }

        public void ReportSuccess(string cmdletName, string parameterSet)
        {
            Report(cmdletName, parameterSet, null);
        }

        public void ReportFailure(string cmdletName, string parameterSet, int errorCode)
        {
            Report(cmdletName, parameterSet, errorCode);
        }

        private void Report(string cmdletName, string parameterSet, int? errorCode)
        {
            // The event type will be CmdletInvocation or ProviderInvocation. The specific cmdlet and parameter set
            // used are encoded in the event's metadata.
            AnalyticsEvent eventData = new AnalyticsEvent(
                _category.ToString(),
                "cmdletName", cmdletName,
                "parameterSet", (parameterSet ?? "null"),
                "errorCode", (errorCode.HasValue ? errorCode.ToString() : "null"));

            s_reporter.Value.ReportEvent(
                source: "virtual.powershell",
                eventType: "powershell",
                eventName: eventData.Name,
                userLoggedIn: true,
                projectNumber: null,
                metadata: eventData.Metadata);
        }
    }
}
