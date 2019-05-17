using ArcGIS.Desktop.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickCapturePluginDatasource.Helpers {
	/// <summary>
	/// Extensions to help with error logging
	/// </summary>
	public static class Extensions {
		private static string EVENT_HEADING = "QuickCapture";
		/// <summary>
		/// Enumerate all nested exceptions
		/// </summary>
		/// <param name="exc">Exception with possible nested inner exceptions</param>
		/// <see>https://stackoverflow.com/questions/9314172/getting-all-messages-from-innerexceptions</see>
		/// <returns></returns>
		public static IEnumerable<Exception> GetInnerExceptions(this Exception exc) {
			if (exc == null) {
				throw new ArgumentNullException("ex");
			}

			var innerException = exc;
			do {
				yield return innerException;
				innerException = innerException.InnerException;
			}
			while (innerException != null);
		}
		/// <summary>
		/// Log exception info for diagnostic use
		/// </summary>
		/// <param name="exc">Exception with possible nested inner exceptions</param>
		/// <param name="preamble">Text that will go into the log before the error strings</param>
		/// <param name="eventType">Type of log entry to write: info, warning, error (defaults to error)</param>
		public static void LogException(this Exception exc, string preamble, EventLog.EventType eventType = EventLog.EventType.Error) {
			string sEvtText = $"{preamble}: {string.Join(",\n", exc.GetInnerExceptions().Select(e => e.Message))}";
			sEvtText.LogEvent(eventType);
		}

		/// <summary>
		/// Log info for diagnostic use, adding a standard preamble header to identify this plugin
		/// </summary>
		/// <param name="sEvtText">Text to go into the event log</param>
		/// <param name="eventType">Type of log entry to write: info, warning, error (defaults to error)</param>
		public static void LogEvent(this string sEvtText, EventLog.EventType eventType = EventLog.EventType.Error) {
			string sEvtTextFull = EVENT_HEADING + ": " + sEvtText;
			EventLog.Write(eventType, sEvtTextFull);
		}
	}
}
