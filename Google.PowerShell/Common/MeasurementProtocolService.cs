// Copyright 2015 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    /// - "category" is fixed to be "powershell"
    /// - "action" is the name of the cmdlet
    /// - "label" is the name of the parameter set
    /// - "value" will be null if the cmdlet was successful, otherwise non-zero.

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
        /// <param name="errorCode">Return the HTTP error code as applicable, otherwise use 0.</param>
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

            // Loop backwards and start from kMaxEvents down to eventsRecorded_.
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
}
