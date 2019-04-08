﻿using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using QuickCaptureSqliteDBCustomItem.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickCaptureSqliteDBCustomItem.Buttons {
	internal class AddQuickCaptureTableToMap : Button {
		protected async override void OnClick() {
			IProjectWindow catalog = Project.GetCatalogPane();
			IEnumerable<Item> items = catalog.SelectedItems;
			ProgressorSource ps = new ProgressorSource("Adding tables to map...", true);
			await QueuedTask.Run(() => {
				IEnumerable<QuickCaptureVirtualTable> vts = items.OfType<QuickCaptureVirtualTable>();
				foreach (QuickCaptureVirtualTable item in vts) {
					if (item != null) {
						try {
							Table table = item.PluginDS.OpenTable(item.TableName);
							LayerFactory.Instance.CreateFeatureLayer((FeatureClass)table, MapView.Active.Map);
						} catch (Exception e) {
							System.Diagnostics.Debug.WriteLine($"Error opening table '{item.TableName}': {e.Message}");
						}
					}
				}
			}, ps.Progressor);
		}
	}
}