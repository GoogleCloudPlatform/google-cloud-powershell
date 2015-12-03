// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;


namespace Google.PowerShell.Common
{
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

    /// <summary>
    /// Wrapper around the Google Analytics Measurement Protocol service. This class is stateless and
    /// just exposes methods to submit event data. See:
    /// https://developers.google.com/analytics/devguides/collection/protocol/v1/
    /// 
    /// This class is thread hostile. You have been warned.
    /// </summary>
    public class MeasurementProtocolService
    {
        // TODO(chrsmith): After the code has been submitted and had some bake time,
        // change this to the prod Cloud SDK analytics property ID ("UA-36037335-2").
        private const string TestWebPropertyID = "UA-19953206-4";

        // Static constructor initializes the default values.
        static MeasurementProtocolService()
        {
            SetWebPropertyID(TestWebPropertyID);
            // Consumers should set a more appropriate ID once known. e.g. reading the
            // Cloud SDK's settings file.
            SetClientID(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Google Analytics web property ID to associate the data with.
        /// </summary>
        public static string WebPropertyID { get; protected set; }

        /// <summary>
        /// Anonymous client ID for the source of the event.
        /// </summary>
        public static string ClientID { get; protected set; }

        public static void SetWebPropertyID(string analyticsID)
        {
            AssertArgumentNotNullOrEmpty("analyticsID", analyticsID);
            // TODO(chrsmith): We could also assert it matches a regex, etc.
            WebPropertyID = analyticsID;
        }

        public static void SetClientID(string clientID)
        {
            AssertArgumentNotNullOrEmpty("clientID", clientID);
            ClientID = clientID.Trim();
        }

        public static HttpWebRequest GenerateRequest(string category, string action, string label, int? value = null)
        {
            AssertArgumentNotNullOrEmpty("category", category);
            AssertArgumentNotNullOrEmpty("action", action);
            AssertArgumentNotNullOrEmpty("label", label);

            // If you need help debugging the request, see the Validation Server at
            // /debug/collect and then inspect the response.
            var request = (HttpWebRequest)WebRequest.Create("https://www.google-analytics.com/collect");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.KeepAlive = false;

            var payloadData = new Dictionary<string, string> {
                { "v", "1" },
                { "tid", WebPropertyID },
                { "cid", ClientID },
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
            var postDataString =
                payloadData
                .Aggregate("", (acc, val) => string.Format("{0}{1}={2}&", acc, val.Key,
                                                             HttpUtility.UrlEncode(val.Value)))
                .TrimEnd('&');

            var encoding = new UTF8Encoding(false /* no BOM */);
            request.ContentLength = Encoding.UTF8.GetByteCount(postDataString);
            using (var writer = new StreamWriter(request.GetRequestStream(), encoding))
            {
                writer.Write(postDataString);
                writer.Close();
            }

            return request;
        }

        public static void IssueRequest(HttpWebRequest request)
        {
            try
            {
                using (var webResponse = (HttpWebResponse)request.GetResponse())
                {
                    if (webResponse.StatusCode != HttpStatusCode.OK)
                    {
                        throw new WebException("Google Analytics did not return a 200.");
                    }
                    webResponse.Close();
                }
            }
            catch (Exception)
            {
                // Silently ignore it. Even if the request was malformed Google Analytics will
                // return a 200. So I assume this would only happen in the event of some transient
                // network failure, e.g. there is no internet connection.
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
        /// <param name="errorCode">Return the HTTP error code as applicable, otherwise use non-zero.</param>
        void ReportFailure(string cmdletName, string parameterSet, int errorCode);
    }

    /// <summary>
    /// Fake implementation of IReportCmdletResults for unit testing. This will also be used in
    /// production for users who have opted-out of sending analytics data to Google. (Read:
    /// performance matters.)
    /// </summary>
    public class FakeCmdletResultReporter : IReportCmdletResults
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

        public FakeCmdletResultReporter()
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
            _events[_eventsRecorded] = record;
            _eventsRecorded++;
            if (_eventsRecorded >= kMaxEvents)
            {
                _eventsRecorded = 0;
            }
        }

        /// <summary>
        /// Clears all history of events.
        /// </summary>
        public void Reset()
        {
            _eventsRecorded = 0;
            _events = new EventRecord[16];
        }

        /// <summary>
        /// Returns whether or not an event with the given makeup has been recorded.
        /// </summary>
        public bool ContainsEvent(string cmdletName, string parameterSet, int errorCode = Int32.MinValue)
        {
            var expectedRecord = EventRecord.Create(cmdletName, parameterSet, errorCode);

            // Count backwards to 0.
            for (int i = _eventsRecorded - 1; i >= 0; i--)
            {
                if (_events[i].Equals(expectedRecord))
                {
                    return true;
                }
            }

            // Loop backwards and start from kMaxEvents down to _eventsRecorded.
            for (int i = kMaxEvents - 1; i >= _eventsRecorded; i--)
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
            MeasurementProtocolService.SetClientID(clientID);
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
