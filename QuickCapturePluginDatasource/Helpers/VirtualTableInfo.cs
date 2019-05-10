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
