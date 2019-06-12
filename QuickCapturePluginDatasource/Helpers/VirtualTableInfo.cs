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
namespace QuickCapturePluginDatasource.Helpers {
	/// <summary>
	/// Maintains information on each feature service found in the Errors.sqlite DB
	/// and treats each one as a feature class that can be added to the map and inspected.
	/// </summary>
	internal class VirtualTableInfo: System.IDisposable {
		// Note that we're keeping a lot of information here that also lives within the ProPluginTableTemplate.
		// That's because we need to use this info before we want to expend the effort of opening the table, reading
		// its data, etc.
		private string _featSvcUrl;
		private ProPluginTableTemplate _table = null;
		private string _layerInfo;
		private int? _timestamp;

		//public VirtualTableInfo(string featSvcUrl, ProPluginTableTemplate table) {
		//	_featSvcUrl = featSvcUrl;
		//	_table = table;
		//}
		//public VirtualTableInfo() {}
		public VirtualTableInfo(string featSvcUrl, string layerInfoJson, int? lastUpdated) {
			_featSvcUrl = featSvcUrl;
			_layerInfo = layerInfoJson;
			_timestamp = lastUpdated;
		}

		public ProPluginTableTemplate Table { get => _table; set => _table = value; }
		public string FeatSvcUrl { get => _featSvcUrl; set => _featSvcUrl = value; }
		public string LayerInfoJson { get => _layerInfo; set => _layerInfo = value; }
		public int? Timestamp { get => _timestamp;  }

		public void Dispose() {
			_table?.Dispose();
		}
	}
}
