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
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Framework.Utilities;
using ArcGIS.Desktop.Mapping;
using QuickCapturePluginDatasource.Helpers;
using QuickCaptureSqliteDBCustomItem.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickCaptureSqliteDBCustomItem.Buttons {
	internal class AddQuickCaptureTableToMap : Button {
		protected async override void OnClick() {
			IProjectWindow catalog = Project.GetCatalogPane();
			IEnumerable<Item> items = catalog.SelectedItems;
			Map map = null;
			if (MapView.Active != null && MapView.Active.Map != null)
				map = MapView.Active.Map;
			else await QueuedTask.Run(() => { // If no map, add one just for this purpose
				map = MapFactory.Instance.CreateMap("QuickCapture Errors", ArcGIS.Core.CIM.MapType.Map, ArcGIS.Core.CIM.MapViewingMode.Map);
				ProApp.Panes.CreateMapPaneAsync(map);
			});
			ProgressorSource ps = new ProgressorSource("Adding tables to map...", true);
			await QueuedTask.Run(() => {
				IEnumerable<QuickCaptureVirtualTable> vts = items.OfType<QuickCaptureVirtualTable>();
				foreach (QuickCaptureVirtualTable item in vts) {
					if (item != null) {
						try {
							// Assumption: QuickCapture errors will only have features, not table-only data
							Table table = item.PluginDS.OpenTable(item.TableName);
							LayerFactory.Instance.CreateFeatureLayer((FeatureClass)table, map);
						} catch (Exception e) {
							string sMsgs = string.Join("\n", e.GetInnerExceptions().Select(exc => exc.Message));
							EventLog.Write(EventLog.EventType.Error, $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}: {sMsgs}");
							MessageBox.Show("Error opening errors table: " + sMsgs);
							System.Diagnostics.Debug.WriteLine($"Error opening table '{item.TableName}': {sMsgs}");
						}
					}
				}
			}, ps.Progressor);
		}
	}
}
