// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Client for the the Google Analytics Measurement Protocol service, which makes
    /// HTTP requests to publish data to a Google Analytics account. This is used to track
    /// usage of PowerShell cmdlets.
    /// 
    /// For more information, see:
    /// https://developers.google.com/analytics/devguides/collection/protocol/v1/
    ///
    /// The following convention is assumed to be used:
    /// - "category" is fixed to be "PowerShell"
    /// - "action" is the name of the cmdlet
    /// - "label" is the name of the parameter set
    /// - "value" will be null if the cmdlet was successful, otherwise non-zero.
    ///
    /// This class is thread hostile. You have been warned.
    /// </summary>
    public class MeasurementProtocolService
    {
        // TODO(chrsmith): After the code has been submitted and had some bake time,
        // change this to the prod Cloud SDK analytics property ID ("UA-36037335-2").
        private const string TestWebPropertyId = "UA-80810157-1";

        // Static constructor initializes the default values.
        static MeasurementProtocolService()
        {
            // Consumers should set a more appropriate ID once known. e.g. reading the
            // Cloud SDK's settings file.
            SetClientId(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Google Analytics web property ID to associate the data with.
        /// </summary>
        public static string WebPropertyId { get; } = TestWebPropertyId;

        /// <summary>
        /// Anonymous client ID for the source of the event.
        /// </summary>
        public static string ClientId { get; protected set; }

        /// <summary>
        /// Sets the client ID used when reporting telemetry.
        /// </summary>
        public static void SetClientId(string clientId)
        {
            AssertArgumentNotNullOrEmpty(nameof(clientId), clientId);
            ClientId = clientId.Trim();
        }

        /// <summary>
        /// Generates the HTTP request object used for sending telemetry data.
        /// </summary>
        public static HttpWebRequest GenerateRequest(string category, string action, string label, int? value = null)
        {
            AssertArgumentNotNullOrEmpty(nameof(category), category);
            AssertArgumentNotNullOrEmpty(nameof(action), action);
            AssertArgumentNotNullOrEmpty(nameof(label), label);

            // If you need help debugging the request, see the Validation Server at
            // /debug/collect and then inspect the response.
            var request = (HttpWebRequest)WebRequest.Create("https://www.google-analytics.com/collect");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.KeepAlive = false;

            // Data we will send along with the web request. Later baked into the HTTP
            // request's payload.
            var payloadData = new Dictionary<string, string> {
                { "v", "1" },
                { "tid", WebPropertyId },
                { "cid", ClientId },
                { "t", "event" },
                { "ec", category },
                { "ea", action },
                { "el", label },
            };
            if (value.HasValue)
            {
                payloadData.Add("ev", value.ToString());
            }

            // Convert the URL parameters into a single payload.
            string postDataString = String.Join(
                "&",
                payloadData.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

            var encoding = new UTF8Encoding(false /* no BOM */);
            request.ContentLength = Encoding.UTF8.GetByteCount(postDataString);
            using (var writer = new StreamWriter(request.GetRequestStream(), encoding))
            {
                writer.Write(postDataString);
            }

            return request;
        }

        /// <summary>
        /// Sends the web request, effectively sending the telemetry data to Google Analytics.
        /// </summary>
        public static void IssueRequest(HttpWebRequest request)
        {
            try
            {
                using (var webResponse = (HttpWebResponse)request.GetResponse())
                {
                    // If usage data seems too low, consider debugging this line
                    // and checking webResponse.StatusCode.
                }
            }
            catch (Exception ex)
            {
                // Silently ignore it. Even if the request was malformed Google Analytics will
                // return a 200. So I assume this would only happen in the event of some transient
                // network failure, e.g. there is no internet connection.
                Debug.WriteLine("Error issuing Analytics request: {0}", ex.Message);
            }
        }

        private static void AssertArgumentNotNullOrEmpty(string argumentName, string argumentValue)
        {
            if (string.IsNullOrEmpty(argumentValue))
            {
                throw new ArgumentNullException(argumentName);
            }
        }
    }

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
    /// Reports PowerShell cmdlet results to Google Analytics.
    /// </summary>
    public class GoogleAnalyticsCmdletReporter : IReportCmdletResults
    {
        public GoogleAnalyticsCmdletReporter(string clientID)
        {
            MeasurementProtocolService.SetClientId(clientID);
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
            var request = MeasurementProtocolService.GenerateRequest("PowerShell", cmdletName, parameterSet, errorCode);
            MeasurementProtocolService.IssueRequest(request);
        }
    }
}
