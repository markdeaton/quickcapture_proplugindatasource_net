namespace QuickCapturePlugin.Helpers {
	internal class VirtualTableInfo: System.IDisposable {
		private string _featSvcUrl;
		private ProPluginTableTemplate _table = null;

		public VirtualTableInfo(string featSvcUrl, ProPluginTableTemplate table) {
			_featSvcUrl = featSvcUrl;
			_table = table;
		}
		public VirtualTableInfo() {}
		public VirtualTableInfo(string featSvcUrl) {
			_featSvcUrl = featSvcUrl;
		}

		public ProPluginTableTemplate Table { get => _table; set => _table = value; }
		public string FeatSvcUrl { get => _featSvcUrl; set => _featSvcUrl = value; }

		public void Dispose() {
			_table?.Dispose();
		}
	}
}
