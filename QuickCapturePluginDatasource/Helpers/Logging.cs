/*

   Copyright 2018 Esri

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

   See the License for the specific language governing permissions and
   limitations under the License.

*/
using ArcGIS.Desktop.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

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
				throw new ArgumentNullException("exc");
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
