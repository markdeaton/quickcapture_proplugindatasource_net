using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ESRI.ArcGIS.ItemIndex;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickCaptureSqliteDBCustomItem.Items {
	/// <summary>
	/// Example custom project item. A custom project item is a custom item which
	/// can be persisted in a project file
	/// </summary>
	/// <remarks>
	/// As a <i>project</i> item, QuakeProjectItems can save state into the aprx. Conversely,
	/// when a project is opened that contains a persisted QuakeProjectItem, QuakeProjectItem
	/// is asked to re-hydrate itself (based on the name, catalogpath, and type that was
	/// saved in the project)</remarks>
	internal class QuickCaptureDBItem : CustomProjectItemBase {
		
		protected QuickCaptureDBItem() : base() { }
		protected QuickCaptureDBItem(ItemInfoValue iiv) : base(FlipBrowseDialogOnly(iiv)) { }
		private ImageSource smallIcon = null, largeIcon = null;

		/// <summary>
		/// This constructor is called if the project item was saved into the project.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="catalogPath"></param>
		/// <param name="typeID"></param>
		/// <param name="containerTypeID"></param>
		/// <remarks>Custom project items cannot <b>not</b> be saved into the project if
		/// the user clicks (or executes) save</remarks>
		public QuickCaptureDBItem(string name, string catalogPath, string typeID, string containerTypeID) :
		  base(name, catalogPath, typeID, containerTypeID) {
		}

		private static ItemInfoValue FlipBrowseDialogOnly(ItemInfoValue iiv) {
			iiv.browseDialogOnly = "FALSE";
			return iiv;
		}

		/// <summary>
		/// Gets whether the project item can contain child items
		/// </summary>
		public override bool IsContainer => true;

		public override ImageSource LargeImage {
			get {
				if (largeIcon == null) {
					BitmapImage bi3 = new BitmapImage();
					bi3.BeginInit();
					bi3.UriSource = new Uri("pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/Geodatabase32.png", UriKind.Absolute);
					bi3.EndInit();
					largeIcon = bi3 as ImageSource;
				}
				return largeIcon;
			}
		}

		public override Task<ImageSource> SmallImage {
			get {
				if (smallIcon == null) {
					BitmapImage bi3 = new BitmapImage();
					bi3.BeginInit();
					bi3.UriSource = new Uri("pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/EditingUnselectedGeodatabase16.png", UriKind.Absolute);
					bi3.EndInit();
					smallIcon = bi3 as ImageSource;
				}
				return Task.FromResult(smallIcon);
			}
		}

		/// <summary>
		/// Fetch is called if <b>IsContainer</b> = <b>true</b> and the project item is being
		/// expanded in the Catalog pane for the first time.
		/// </summary>
		/// <remarks>The project item should instantiate items for each of its children and
		/// add them to its child collection (see <b>AddRangeToChildren</b>)</remarks>
		public override void Fetch() {
			Parse();
		}

		private async void Parse() {
			//This is where the QuickCapture item is located
			string filePath = this.Path;

			IReadOnlyList<string> tables = new List<string>();
			List<QuickCaptureVirtualTable> events = new List<QuickCaptureVirtualTable>();

			PluginDatastore pluginws = null;

			ProgressorSource ps = new ProgressorSource("Reading archive...", true);
			await QueuedTask.Run(() => {
				try {
					// TODO Assumption: it's okay that sqlite DB and attachments will be deleted from temp by ArcGIS Pro when it closes

					// Create temp directory - convoluted, but guarantees no filename conflicts
					string tempDir = System.IO.Path.GetTempFileName();
					File.Delete(tempDir); Directory.CreateDirectory(tempDir);

					// Unzip to temp directory
					ZipFile.ExtractToDirectory(filePath, tempDir);

					// Sqlite database path
					string db_path = System.IO.Path.Combine(tempDir, "Errors.sqlite");

					pluginws = new PluginDatastore(
						 new PluginDatasourceConnectionPath("QuickCapturePlugin_Datasource",
							   new Uri(db_path, UriKind.Absolute)));
					System.Diagnostics.Debug.Write("==========================\r\n");

					tables = pluginws.GetTableNames().ToList();
				} catch (Exception e) {
					System.Diagnostics.Debug.WriteLine("Error while reading archive and tables: " + e.Message, e);
					MessageBox.Show("Error reading archive and tables: " + e.Message);
					pluginws?.Dispose();
				}
			}, ps.Progressor);

			foreach (string table in tables) {
				// TODO Assumption: it's okay to have a null timestamp for these catalog items
				QuickCaptureVirtualTable vTbl = new QuickCaptureVirtualTable(table, $"{filePath}->{table}", "QuickCapture_VirtualTable", null, pluginws);
				events.Add(vTbl);
			}

			//Add the event "child" items to the child collection
			this.AddRangeToChildren(events);
		}

	}

	/// <summary>
	/// QuickCapture virtual tables (each is a feature service). These are children of a QuickCaptureDBItem
	/// </summary>
	/// <remarks>QuickCaptureVirtualTables are, themselves, custom items</remarks>
	internal class QuickCaptureVirtualTable : CustomItemBase/*, IDisposable*/ {
		private ImageSource largeIcon = null, smallIcon = null;

		public QuickCaptureVirtualTable(string name, string path, string type, string lastModifiedTime, PluginDatastore pluginws) : base(name, path, type, lastModifiedTime) {
			this.DisplayType = "QuickCapture Virtual Table";
			this.ContextMenuID = "QuickCaptureSqliteDBCustomItem_ContextMenu";
			this._pluginDs = pluginws;
		}


		public override ImageSource LargeImage {
			get {
				if (largeIcon == null) {
					BitmapImage bi3 = new BitmapImage();
					bi3.BeginInit();
					bi3.UriSource = new Uri("pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/TableOpen32.png", UriKind.Absolute);
					bi3.EndInit();
					largeIcon = bi3 as ImageSource;
				}
				return largeIcon;
			}
		}

		public override Task<ImageSource> SmallImage {
			get {
				if (smallIcon == null) {
					BitmapImage bi3 = new BitmapImage();
					bi3.BeginInit();
					bi3.UriSource = new Uri("pack://application:,,,/ArcGIS.Desktop.Resources;component/Images/TableOpen16.png", UriKind.Absolute);
					bi3.EndInit();
					smallIcon = bi3 as ImageSource;
				}
				return Task.FromResult(smallIcon);
			}
		}

		public string TableName { get => this.Name; }
		private PluginDatastore _pluginDs = null;
		public PluginDatastore PluginDS { get => _pluginDs; }

		//#region IDisposable Support
		//private bool disposedValue = false; // To detect redundant calls

		//protected virtual void Dispose(bool disposing) {
		//	if (!disposedValue) {
		//		if (disposing) {
		//			_pluginDs.Dispose();
		//		}

		//		// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
		//		// TODO: set large fields to null.

		//		disposedValue = true;
		//	}
		//}

		//// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		//// ~QuickCaptureVirtualTable() {
		////   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		////   Dispose(false);
		//// }

		//// This code added to correctly implement the disposable pattern.
		//public void Dispose() {
		//	// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//	Dispose(true);
		//	// TODO: uncomment the following line if the finalizer is overridden above.
		//	// GC.SuppressFinalize(this);
		//}
		//#endregion
	}
}
