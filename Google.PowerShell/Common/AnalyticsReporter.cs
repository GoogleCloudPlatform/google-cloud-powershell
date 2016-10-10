// Copyright 2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

// Forked from https://github.com/GoogleCloudPlatform/google-cloud-visualstudio
// At c1d57d0
// Original files:
// AnalyticsReporter.cs, IAnalyticsReporter.cs, IHitSender.cs, HitSender.cs
// Local changes:
// - Changed namespaces
// - Merged contents from multiple files into one.
// - Removed calls to DebugPrintAnalyticsOutput

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// This interface defines the what a class capable of sending hits to an analytics
    /// services should implement.
    /// </summary>
    public interface IHitSender
    {
        void SendHitData(Dictionary<string, string> hitData);
    }

    /// <summary>
    /// Class used to send Google Analytic hits using the Google Analytics measurement protocol.
    /// 
    /// For more information, see:
    /// https://developers.google.com/analytics/devguides/collection/protocol/v1/
    /// </summary>
    internal class HitSender : IHitSender
    {
        private const string ProductionServerUrl = "https://www.google-analytics.com/internal/collect";
        private const string DebugServerUrl = "https://ssl.google-analytics.com/debug/collect";

        private readonly Lazy<HttpClient> _httpClient;
        private readonly string _serverUrl;
        private readonly string _userAgent;

        public HitSender(bool debug, string userAgent)
        {
            _serverUrl = debug ? DebugServerUrl : ProductionServerUrl;
            _userAgent = userAgent;
            _httpClient = new Lazy<HttpClient>(CreateHttpClient);
        }

        /// <summary>
        /// Sends the hit data to the server.
        /// </summary>
        /// <param name="hitData">The hit data to be sent.</param>
        public async void SendHitData(Dictionary<string, string> hitData)
        {
            var client = _httpClient.Value;
            using (var form = new FormUrlEncodedContent(hitData))
            using (var response = await client.PostAsync(_serverUrl, form).ConfigureAwait(false))
            {
                // See comment above DebugPrintAnalyticsOutput.
            }
        }

#if false
        // Removed. In the source code, the body of the double-using blog contained a call to
        // emit diagnostic information in DEBUG builds. This doesn't translate well to the
        // PowerShell codebase, where we currently build and ship our DEBUG bits.
        DebugPrintAnalyticsOutput(response.Content.ReadAsStringAsync(), hitData);

        /// <summary>
        /// Debugging utility that will print out to the output window the result of the hit request.
        /// </summary>
        /// <param name="resultTask">The task resulting from the request.</param>
        [Conditional("DEBUG")]
        private async void DebugPrintAnalyticsOutput(Task<string> resultTask, Dictionary<string, string> hitData)
        {
            using (var form = new FormUrlEncodedContent(hitData))
            {
                var result = await resultTask.ConfigureAwait(false);
                var formData = await form.ReadAsStringAsync().ConfigureAwait(false);
                Debug.WriteLine($"Request: POST {_serverUrl} Data: {formData}");
                Debug.WriteLine($"Output of analytics: {result}");
            }
        }
#endif
        private HttpClient CreateHttpClient()
        {
            var result = new HttpClient();
            if (_userAgent != null)
            {
                result.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
            }
            return result;
        }
    }

    /// <summary>
    /// This interface abstracts away a class to send analytics data.
    /// </summary>
    public interface IAnalyticsReporter
    {
        /// <summary>
        /// Reports a single event as defined by Google Analytics.
        /// </summary>
        /// <param name="category">The cateogry of the event.</param>
        /// <param name="action">The action taken.</param>
        /// <param name="label">The label for the event, optional.</param>
        /// <param name="value">The value for the event, optional.</param>
        void ReportEvent(string category, string action, string label = null, int? value = null);

        /// <summary>
        /// Reports a page view.
        /// </summary>
        /// <param name="page">The URL of the page.</param>
        /// <param name="title">The title of the page.</param>
        /// <param name="host">The host name for the page.</param>
        /// <param name="customDimensions">Custom values to report using the custom dimensions.</param>
        void ReportPageView(string page, string title, string host, Dictionary<int, string> customDimensions = null);
    }

    /// <summary>
    /// <para>
    /// Client for the the Google Analytics Measurement Protocol service, which makes
    /// HTTP requests to publish data to a Google Analytics account
    /// </para>
    /// 
    /// <para>
    /// </para>
    /// </summary>
    public class AnalyticsReporter : IAnalyticsReporter
    {
        private const string HitTypeParam = "t";
        private const string VersionParam = "v";
        private const string EventCategoryParam = "ec";
        private const string EventActionParam = "ea";
        private const string EventLabelParam = "el";
        private const string EventValueParam = "ev";
        private const string SessionControlParam = "sc";
        private const string PropertyIdParam = "tid";
        private const string ClientIdParam = "cid";
        private const string AppNameParam = "an";
        private const string AppVersionParam = "av";
        private const string ScreenNameParam = "cd";
        private const string DocumentTitleParam = "dt";
        private const string DocumentPathParam = "dp";
        private const string DocumentHostNameParam = "dh";

        private const string VersionValue = "1";
        private const string EventTypeValue = "event";
        private const string PageViewValue = "pageView";
        private const string SessionStartValue = "start";
        private const string SessionEndValue = "end";
        private const string ScreenViewValue = "screenview";

        private readonly Dictionary<string, string> _baseHitData;
        private readonly IHitSender _hitSender;

        /// <summary>
        /// The name of the application to use when reporting data.
        /// </summary>
        public string ApplicationName { get; }

        /// <summary>
        /// The version to use when reporting data.
        /// </summary>
        public string ApplicationVersion { get; }

        /// <summary>
        /// The property ID being used by this reporter.
        /// </summary>
        public string PropertyId { get; }

        /// <summary>
        /// The client ID being used by this reporter.
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="propertyId">The property ID to use, string with the format US-XXXX. Must not be null.</param>
        /// <param name="appName">The name of the app for which this reporter is reporting. Must not be null.</param>
        /// <param name="clientId">The client id to use when reporting, if null a new random Guid will be generated.</param>
        /// <param name="appVersion">Optional, the app version. Defaults to null.</param>
        /// <param name="debug">Optional, whether this reporter is in debug mode. Defaults to false.</param>
        /// <param name="userAgent">Optiona, the user agent to use for all HTTP requests.</param>
        /// <param name="sender">The instance of <seealso cref="IHitSender"/> to use to send the this.</param>
        public AnalyticsReporter(
            string propertyId,
            string appName,
            string clientId = null,
            string appVersion = null,
            bool debug = false,
            string userAgent = null,
            IHitSender sender = null)
        {
            PropertyId = Preconditions.CheckNotNull(propertyId, nameof(propertyId));
            ApplicationName = Preconditions.CheckNotNull(appName, nameof(appName));
            ClientId = clientId ?? Guid.NewGuid().ToString();
            ApplicationVersion = appVersion;

            _baseHitData = MakeBaseHitData();
            _hitSender = sender ?? new HitSender(debug, userAgent);
        }

        /// <summary>
        /// Convenience method to report a single event to Google Analytics.
        /// </summary>
        /// <param name="category">The category for the event.</param>
        /// <param name="action">The action that took place.</param>
        /// <param name="label">The label affected by the event.</param>
        /// <param name="value">The new value.</param>
        public void ReportEvent(string category, string action, string label = null, int? value = null)
        {
            Preconditions.CheckNotNull(category, nameof(category));
            Preconditions.CheckNotNull(action, nameof(action));

            // Data we will send along with the web request. Later baked into the HTTP
            // request's payload.
            var hitData = new Dictionary<string, string>(_baseHitData)
            {
                { HitTypeParam, EventTypeValue },
                { EventCategoryParam, category },
                { EventActionParam, action },
            };
            if (label != null)
            {
                hitData[EventLabelParam] = label;
            }
            if (value != null)
            {
                hitData[EventValueParam] = value.ToString();
            }
            _hitSender.SendHitData(hitData);
        }

        /// <summary>
        /// Reports a page view hit to analytics.
        /// </summary>
        /// <param name="page">The URL to the page.</param>
        /// <param name="title">The page title.</param>
        /// <param name="host">The page host name.</param>
        /// <param name="customDimensions">Custom dimensions to add to the hit.</param>
        public void ReportPageView(
            string page,
            string title,
            string host,
            Dictionary<int, string> customDimensions = null)
        {
            Preconditions.CheckNotNull(page, nameof(page));

            var hitData = new Dictionary<string, string>(_baseHitData)
            {
                { HitTypeParam, PageViewValue },
                { DocumentPathParam, page },
            };

            if (title != null)
            {
                hitData[DocumentTitleParam] = title;
            }
            if (host != null)
            {
                hitData[DocumentHostNameParam] = host;
            }
            if (customDimensions != null)
            {
                foreach (var entry in customDimensions)
                {
                    hitData[GetCustomDimension(entry.Key)] = entry.Value;
                }
            }

            _hitSender.SendHitData(hitData);
        }

        /// <summary>
        /// Reports a window view.
        /// </summary>
        /// <param name="name">The name of the window. Must not be null.</param>
        public void ReportScreen(string name)
        {
            Preconditions.CheckNotNull(name, nameof(name));

            var hitData = new Dictionary<string, string>(_baseHitData)
            {
                { HitTypeParam, ScreenViewValue },
                { ScreenNameParam, name },
            };
            _hitSender.SendHitData(hitData);
        }

        /// <summary>
        /// Reports that the session is starting.
        /// </summary>
        public void ReportStartSession()
        {
            var hitData = new Dictionary<string, string>(_baseHitData)
            {
                { HitTypeParam,EventTypeValue },
                { SessionControlParam, SessionStartValue }
            };
            _hitSender.SendHitData(hitData);
        }

        /// <summary>
        /// Reports that the session is ending.
        /// </summary>
        public void ReportEndSession()
        {
            var hitData = new Dictionary<string, string>(_baseHitData)
            {
                { HitTypeParam, EventTypeValue },
                { SessionControlParam, SessionEndValue }
            };
            _hitSender.SendHitData(hitData);
        }

        /// <summary>
        /// Constructs the dictionary with the common parameters that all requests must
        /// have.
        /// </summary>
        /// <returns>Dictionary with the parameters for the report request.</returns>
        private Dictionary<string, string> MakeBaseHitData()
        {
            var result = new Dictionary<string, string>
            {
                { VersionParam, VersionValue },
                { PropertyIdParam, PropertyId },
                { ClientIdParam, ClientId },
                { AppNameParam, ApplicationName },
            };
            if (ApplicationVersion != null)
            {
                result.Add(AppVersionParam, ApplicationVersion);
            }
            return result;
        }

        private static string GetCustomDimension(int index) => $"cd{index}";
    }
}
