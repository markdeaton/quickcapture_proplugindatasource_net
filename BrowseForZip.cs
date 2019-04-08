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
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.PluginDatastore;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace QuickCapturePluginTest {
    /// <summary>
    /// Example of accessing a Plugin Workspace via the ArcGIS.Core.Data API
    /// </summary>
    internal class BrowseForZip : Button {
		protected async override void OnClick() {
			// Browse for zip file
			OpenFileDialog dlgFiles = new OpenFileDialog() {
				Title = "Select a QuickCapture error archive",
				DefaultExt = ".zip",
				Filter = "Zip files (.zip)|*.zip"
			};
			string filename;
			bool? res = dlgFiles.ShowDialog();
			if (res == true) {
				filename = dlgFiles.FileName;
			} else return;

			IReadOnlyList<string> tables = new List<string>();
			PluginDatastore pluginws = null;

			ProgressorSource ps = new ProgressorSource("Reading archive...", true);
			await QueuedTask.Run(() => {
				try {
					// TODO Assumption: it's okay that sqlite DB and attachments will be deleted from temp by ArcGIS Pro when it closes

					// Create temp directory - convoluted, but guarantees no filename conflicts
					string tempDir = Path.GetTempFileName();
					File.Delete(tempDir); Directory.CreateDirectory(tempDir);

					// Unzip to temp directory
					ZipFile.ExtractToDirectory(filename, tempDir);

					// Sqlite database path
					string db_path = Path.Combine(tempDir, "Errors.sqlite");

					pluginws = new PluginDatastore(
						 new PluginDatasourceConnectionPath("QuickCapturePlugin_Datasource",
							   new Uri(db_path, UriKind.Absolute)));
					System.Diagnostics.Debug.Write("==========================\r\n");

					tables = pluginws.GetTableNames();
				} catch (Exception e) {
					System.Diagnostics.Debug.WriteLine("Error while reading archive and tables: " + e.Message, e);
					MessageBox.Show("Error reading archive and tables: " + e.Message);
					pluginws?.Dispose();
				}
			}, ps.Progressor);

			if (tables.Count <= 0) return;

			// Show list of tables for the user to choose from
			//string table_name;
			TableChooser dlgTables = new TableChooser(tables) {
				Owner = FrameworkApplication.Current.MainWindow
			};
			bool? isTableChosen = dlgTables.ShowDialog();

			System.Collections.IList tableNames;
			if (isTableChosen ?? false) tableNames = dlgTables.SelectedTableNames;
			else return;

			ps.Message = "Reading tables...";
			await QueuedTask.Run(() => {
				foreach (string table_name in tableNames) {
					using (var table = pluginws.OpenTable(table_name)) {
						try {
							LayerFactory.Instance.CreateFeatureLayer((FeatureClass)table, MapView.Active.Map);
						} catch (Exception e) {
							MessageBox.Show("Error opening table: " + e.Message);
						}
					}
				}
			}, ps.Progressor);

			pluginws.Dispose();
		}
	}
}
