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
using Newtonsoft.Json.Linq;
using QuickCapturePluginDatasource.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickCapturePluginDatasource {
	/// <summary>
	/// Implements a custom plugin datasource for reading Sqlite database tables and feature classes
	/// </summary>
	/// <remarks>A per thread instance will be created (as needed) by Pro.</remarks>
	public class ProPluginDatasourceTemplate : PluginDatasourceTemplate {

		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[DllImport("kernel32.dll")]
		internal static extern uint GetCurrentThreadId();

		private uint _thread_id;
		private SQLiteConnection _dbConn;

		private Dictionary<string, VirtualTableInfo> _tables = new Dictionary<string, VirtualTableInfo>();
		private string _layerInfosDir;

		/// <summary>
		/// Open the specified workspace (database)
		/// </summary>
		/// <param name="connectionPath">The path to the database file</param>
		/// <remarks>
		/// .NET Clients access Open via the ArcGIS.Core.Data.PluginDatastore.PluginDatastore class
		/// whereas Native clients (Pro internals) access via IWorkspaceFactory</remarks>
		public override void Open(Uri connectionPath) {
			if (!System.IO.File.Exists(connectionPath.LocalPath)) {
				throw new System.IO.FileNotFoundException(connectionPath.LocalPath);
			}
			//initialize
			//Strictly speaking, tracking your thread id is only necessary if
			//your implementation uses internals that have thread affinity.
			_thread_id = GetCurrentThreadId();
			
			// The key is the table's display name, not its url
			_tables = new Dictionary<string, VirtualTableInfo>();

			// Get LayerInfos
			_layerInfosDir = Path.Combine(Path.GetDirectoryName(connectionPath.LocalPath), Properties.Settings.Default.DirName_LayerInfos);
			string connection = @"Read Only=True;Data Source=" + connectionPath.LocalPath;
			_dbConn = new SQLiteConnection(connection);
			_dbConn.Open();

			// For QuickCapture, we'll create virtual tables based on what kind of geometry and features we find in the Feature records JSON
			GetTableNames();
		}

		/// <summary>
		/// Constructs a virtual table name from info in the supplied layerInfos file.
		/// </summary>
		/// <param name="sLyrUrl">The feature service URL</param>
		/// <param name="sLyrInfosFilePath">Location of the layerInfos file for this service/virtual table</param>
		/// <returns>Name of the virtual layer for these features</returns>
		private string TableName(string sLyrUrl, string sLyrInfoJson) {
			// Assumption: Open() is called before anything else, so this will always have access to the layerInfos for table name lookup
			string sTblName;
			//return sLyrName;
			try {
				dynamic lyrInfo = JObject.Parse(sLyrInfoJson);
				sTblName = lyrInfo.name.ToString();
			} catch (Exception) { // Problem with JSON; divine the name another way
				Uri uri = new Uri(sLyrUrl);
				int iSegs = uri.Segments.Length;
				string sLyrName1 = uri.Segments[iSegs - 3].EndsWith("/") ? uri.Segments[iSegs - 3].Substring(0, uri.Segments[iSegs - 3].Length - 1) : uri.Segments[iSegs - 3];
				string sLyrName2 = uri.Segments[iSegs - 1].EndsWith("/") ? uri.Segments[iSegs - 1].Substring(0, uri.Segments[iSegs - 1].Length - 1) : uri.Segments[iSegs - 1];
				sTblName = sLyrName1 + "-" + sLyrName2;
			}
			return sTblName;
		}

		public override void Close() {
			//Dispose of any cached table instances here
			foreach (var table in _tables.Values) {
				try {
					table.Dispose();
				} finally { /* no op */ }
			}
			_tables.Clear();

			if (_dbConn != null) {
				_dbConn.Close();
				_dbConn.Dispose();
			}
		}

		/// <summary>
		/// Open the specified table
		/// </summary>
		/// <param name="name">The name of the table to open</param>
		/// <returns><see cref="PluginTableTemplate"/></returns>
		public override PluginTableTemplate OpenTable(string name) {
			//This is only necessary if your internals have thread affinity
			//
			//If you are using shared data (eg "static") it is your responsibility
			//to manage access to it across multiple threads.
			if (_thread_id != GetCurrentThreadId()) {
				throw new ArcGIS.Core.CalledOnWrongThreadException();
			}

			if (!this.GetTableNames().Contains(name))
				throw new GeodatabaseTableException($"The table {name} was not found");
			// Otherwise there's at least a partial VirtualTableInfo record there
			if (_tables[name].Table == null) {
				// TODO Assumption: all feature geometries are WGS84
				_tables[name].Table = new ProPluginTableTemplate(_dbConn, name, _tables[name].FeatSvcUrl, _tables[name].LayerInfoJson);
			}
			return _tables[name].Table;
		}

		/// <summary>
		/// Get the table names available in the workspace
		/// </summary>
		/// <returns>IReadOnlyList{string}</returns>
		public override IReadOnlyList<string> GetTableNames() {
			// If we've already collected them, don't do it again
			if (_tables.Count <= 0) {
				List<string> tableNames = new List<string>();
				// TODO Assumption: Feature data is always found in a table named "Features".
				// TODO Get and use layerInfos to build table name here
				// TODO Assumption: Archive has been completely extracted by this point
				// Construct layerInfos path
				// Open layerInfos file for this table
				// Parse layerInfos 
				using (SQLiteCommand cmd = _dbConn.CreateCommand()) {
					cmd.CommandText = $"SELECT DISTINCT " +
									  $"	f.{Properties.Settings.Default.FieldName_FeatureSvcLayer}," +
									  $"	l.{Properties.Settings.Default.FieldName_lyr_FileName}," +
									  $"	MAX({Properties.Settings.Default.FieldName_Timestamp}) " +
									  $"FROM {Properties.Settings.Default.TableName_Features} AS f LEFT JOIN {Properties.Settings.Default.TableName_LayerInfo} as l " +
									  $"	ON f.{Properties.Settings.Default.FieldName_FeatureSvcLayer} = l.{Properties.Settings.Default.FieldName_FeatureSvcLayer} " +
									  $"GROUP BY f.{Properties.Settings.Default.FieldName_FeatureSvcLayer}";
					SQLiteDataReader reader = cmd.ExecuteReader();
					while (reader.Read()) {
						string sLyrUrl = reader[0].ToString();
						string sLayerInfosFilePath = Path.Combine(_layerInfosDir, reader[1].ToString());
						string sLayerInfo = File.ReadAllText(sLayerInfosFilePath, Encoding.Default);
						string sLyrName = TableName(sLyrUrl, sLayerInfo);
						int? tableUpdated = reader[2] as int?;
						System.Diagnostics.Debug.WriteLine(sLyrName);
						_tables.Add(sLyrName, new VirtualTableInfo(sLyrUrl, sLayerInfo, tableUpdated)); // We'll add table info in OpenTable()
					}
				}
			}
			return _tables.Keys.ToList();
		}

		/// <summary>
		/// Returns whether or not SQL queries are supported on the plugin
		/// </summary>
		/// <remarks>Returning false (default) means that the WhereClause of an
		/// incoming query filter will always be empty (regardless of what clients
		/// set it to)</remarks>
		/// <returns>true or false</returns>
		public override bool IsQueryLanguageSupported() {
			return true;
		}

	}
}
