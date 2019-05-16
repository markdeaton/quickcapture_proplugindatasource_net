using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Framework.Utilities;
using ESRI.ArcGIS.ItemIndex;
using QuickCapturePluginDatasource.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
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
	internal class QuickCaptureDBItem : CustomItemBase {
		
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
		/// add them to its child collection (see <b>AddRangeToChildren</b>).
		/// In this case, we unzip the archive and read the tables (feature service URLs)
		/// from the Errors.sqlite database.</remarks>
		public async override void Fetch() {
			//This is where the QuickCapture item is located
			string filePath = this.Path;
			string tempDBPath = null;

			IReadOnlyList<string> tables = new List<string>();
			List<QuickCaptureVirtualTable> events = new List<QuickCaptureVirtualTable>();

			PluginDatastore pluginws = null;

			ProgressorSource ps = new ProgressorSource("Reading archive...", true);
			await QueuedTask.Run(() => {
				try {
					// Extract the archive
					// NOTE: sqlite DB and attachments will be deleted from temp by ArcGIS Pro when it closes!

					// Create temp directory - convoluted, but guarantees no filename conflicts
					string tempDir = System.IO.Path.GetTempFileName();
					File.Delete(tempDir); Directory.CreateDirectory(tempDir);

					// Unzip to temp directory
					ZipFile.ExtractToDirectory(filePath, tempDir);

					// Sqlite database path
					tempDBPath = System.IO.Path.Combine(tempDir, "Errors.sqlite");

					pluginws = new PluginDatastore(
						 new PluginDatasourceConnectionPath("QuickCapture_Datasource",
							   new Uri(tempDBPath, UriKind.Absolute)));
					System.Diagnostics.Debug.Write("==========================\r\n");

					tables = pluginws.GetTableNames().ToList();
				} catch (Exception e) {
					string sMsgs = string.Join("\n", e.GetInnerExceptions().Select(exc => exc.Message));
					EventLog.Write(EventLog.EventType.Error, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}: {sMsgs}");
					MessageBox.Show(FrameworkApplication.Current.MainWindow, "Error reading archive and tables: " + sMsgs);
					pluginws?.Dispose();
				}
			}, ps.Progressor);

			foreach (string table in tables) {
				// TODO Assumption: it's okay to have a null timestamp for catalog items
				// TODO Get timestamp: 1) Implement ProPluginDatasourceTemplate::GetTSForTable(sTableName); 2) Cast pluginws to ProPluginDatasourceTemplate (?)
				QuickCaptureVirtualTable vTbl = new QuickCaptureVirtualTable(table, $"{tempDBPath}[{table}]", "QuickCapture_VirtualTable", pluginws);
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
	internal class QuickCaptureVirtualTable : CustomItemBase {
		private ImageSource largeIcon = null, smallIcon = null;
		private readonly PluginDatastore _pluginDS = null;

		public QuickCaptureVirtualTable(string name, string path, string type, PluginDatastore pluginws) : base(name, path, type) {
			this.DisplayType = "QuickCapture Virtual Table";
			this.ContextMenuID = "QuickCaptureSqliteDBCustomItem_ContextMenu";
			this._pluginDS = pluginws;
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
		public PluginDatastore PluginDS { get => _pluginDS; }
	}
}
