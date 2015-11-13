using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// Services wrapping the Google Analytics Measurement Protocol service, which makes
    /// HTTP requests to publish data to a Google Analytics account. This is used to track
    /// usage of PowerShell cmdlets.
    /// 
    /// For more information, see:
    /// https://developers.google.com/analytics/devguides/collection/protocol/v1/
    /// 
    /// There are several implementations of the IMeasurementProtocolService. All are
    /// specific to the PowerShell use-case, and not the generalized API.

    /// <summary>
    /// Interface for interacting with the Measurement Protocol service. See concrete
    /// implementations for details.
    /// </summary>
    public interface IMeasurementProtocolService
    {
        // TODO(chrsmith): Should we be reporting runtime errors too? e.g. Get-GcsBucket
        // returning a 403 or 404?
        /// <summary>
        /// Publish an event to Google Analytics. By convention, the category is
        /// "PowerShell" and the action is the name of the cmdlet. Label should
        /// either be the parameter set (or "default").
        /// </summary>
        /// <remarks>
        /// The Measurement Protocol has an optional "value" (int) parameter which is
        /// not exposed in this library.
        /// </remarks>
        void PublishEvent(string category, string action, string label);
    }

    /// <summary>
    /// Fake implementation of IMeasurementProtocolService for unit testing. This will
    /// also be used in production for users who have opted-out of sending analytics
    /// data to Google. (Read: performance matters.)
    /// </summary>
    public class FakeMeasurementProtocolService : IMeasurementProtocolService
    {
        /// <summary>
        /// IMPORTANT: We rely on ValueType.Equals for structural equality later. If
        ///  you make this a class you will need to overwrite Equals and GetHashCode.
        /// </summary>
        private struct EventRecord
        {
            public string category;
            public string action;
            public string label;

            public static EventRecord Create(string category, string action, string label)
            {
                return new EventRecord
                {
                    category = category,
                    action = action,
                    label = label
                };
            }
        }

        /// <summary>
        /// Keep kMaxEvents stored in memory, for checking later. After kMaxEvents have
        /// been recorded, the oldest events will get overwritten.
        /// </summary>
        private const int kMaxEvents = 16;
        private int eventsRecorded_;
        private EventRecord[] events_;

        public FakeMeasurementProtocolService()
        {
            Reset();
        }

        public void PublishEvent(string category, string action, string label)
        {
            var record = EventRecord.Create(category, action, label);
            events_[eventsRecorded_] = record;
            eventsRecorded_++;
            if (eventsRecorded_ >= kMaxEvents)
            {
                eventsRecorded_ = 0;
            }
        }

        /// <summary>
        /// Clears all history of events.
        /// </summary>
        public void Reset()
        {
            eventsRecorded_ = 0;
            events_ = new EventRecord[16];
        }

        /// <summary>
        /// Returns whether or not an event with the given makeup has been recorded.
        /// </summary>
        public bool ContainsEvent(string category, string action, string label)
        {
            var expectedRecord = EventRecord.Create(category, action, label);

            // Count backwards to 0.
            for (int i = eventsRecorded_ - 1; i >= 0; i--)
            {
                if (events_[i].Equals(expectedRecord))
                {
                    return true;
                }
            }

            // Loop backwards and start from kMaxEvents down to eventsRecorded_.
            for (int i = kMaxEvents - 1; i >= eventsRecorded_; i--)
            {
                if (events_[i].Equals(expectedRecord))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
