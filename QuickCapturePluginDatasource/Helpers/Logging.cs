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
		/// <summary>
		/// Enumerate all nested exceptions
		/// </summary>
		/// <param name="ex"></param>
		/// <see>https://stackoverflow.com/questions/9314172/getting-all-messages-from-innerexceptions</see>
		/// <returns></returns>
		public static IEnumerable<Exception> GetInnerExceptions(this Exception ex) {
			if (ex == null) {
				throw new ArgumentNullException("ex");
			}

			var innerException = ex;
			do {
				yield return innerException;
				innerException = innerException.InnerException;
			}
			while (innerException != null);
		}
	}
}
