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
using ArcGIS.Core.Geometry;
using Newtonsoft.Json.Linq;
using QuickCapturePluginDatasource.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace QuickCapturePluginDatasource {
	/// <summary>
	/// (Custom) interface the sample uses to extract row information from the
	/// plugin table
	/// </summary>
	internal interface IPluginRowProvider {
		PluginRow FindRow(int oid, IEnumerable<string> columnFilter, SpatialReference sr);
	}

	// TODO Assumption: it's okay that any item added to Pro is in a temp location and won't be there if the Pro document is saved and later reopened

	/// <summary>
	/// Implements a plugin table.
	/// </summary>
	/// <remarks>The plugin table appears as an ArcGIS.Core.Data.Table or FeatureClass to
	/// .NET clients (add-ins) and as an ITable or IFeatureClass to native clients (i.e. Pro)
	/// </remarks>
	public class ProPluginTableTemplate : PluginTableTemplate, IDisposable, IPluginRowProvider {

		private SQLiteConnection _dbConn;
		private readonly string _name;
		private readonly string _attachmentsDir;
		/// <summary>
		/// This plugin table represents all features that failed to get stored into a particular feature service
		/// </summary>
		private readonly string _featSvcUrl;
		private DataTable _table;
		private RBush.RBush<RBushCoord3D> _rtree;
		private RBush.Envelope _extent;
		private RBush.Envelope _sr_extent;
		private Envelope _gisExtent;
		private GeometryType _shapeType = GeometryType.Unknown;
		private SpatialReference _sr;
		private bool _hasZ = false;

		internal ProPluginTableTemplate(SQLiteConnection dbConn, string name, string featSvcUrl, SpatialReference sr = null) {
			_dbConn = dbConn;
			_name = name;
			_featSvcUrl = featSvcUrl;
			_rtree = new RBush.RBush<RBushCoord3D>();
			//_sr = sr ?? SpatialReferences.WGS84;
			// _sr set when we first GetGeometryTypeInfo() after we start reading records
			_attachmentsDir = Path.Combine(Path.GetDirectoryName(_dbConn.FileName), Properties.Settings.Default.DirName_Attachments);

			Open();
		}

		/// <summary>
		/// Get the name of the table
		/// </summary>
		/// <returns>Table name</returns>
		public override string GetName() => _name;

		/// <summary>
		/// Gets whether native row count is supported
		/// </summary>
		/// <remarks>Return true if your table can get the row count without having
		/// to enumerate through all the rows (and count them)....which will be
		/// the default behavior if you return false</remarks>
		/// <returns>True or false</returns>
		public override bool IsNativeRowCountSupported() => true;

		/// <summary>
		/// Gets the native row count (if IsNativeRowCountSupported is true)
		/// </summary>
		/// <returns>The row count</returns>
		public override int GetNativeRowCount() => _rtree?.Count ?? _table.Rows.Count;

		/// <summary>
		/// Search the underlying plugin table using the input QueryFilter
		/// </summary>
		/// <param name="queryFilter"></param>
		/// <remarks>If the PluginDatasourceTemplate.IsQueryLanguageSupported returns
		/// false, the WhereClause will always be empty.<br/>
		/// The QueryFilter is never null (even if the client passed in null to the "outside"
		/// table or feature class).<br/>
		/// A FID set in the ObjectIDs collection of the query filter, if present, acts as
		/// the "super" set - or constraint - from which all selections should be made. 
		/// In other words, if the FID set contains ids {1,5,6,10} then a WhereClause
		/// on the query filter can only select from {1,5,6,10} and not from any other
		/// records.</remarks>
		/// <returns><see cref="PluginCursorTemplate"/></returns>
		public override PluginCursorTemplate Search(QueryFilter queryFilter) =>
													this.SearchInternal(queryFilter);

		/// <summary>
		/// Search the underlying plugin table using the input SpatialQueryFilter
		/// </summary>
		/// <remarks>A SpatialQueryFilter cann only be used by clients if the plugin
		/// table returns a GeometryType other than Unknown from GetShapeType().</remarks>
		/// <param name="spatialQueryFilter"></param>
		/// <returns><see cref="PluginCursorTemplate"/></returns>
		public override PluginCursorTemplate Search(SpatialQueryFilter spatialQueryFilter) =>
													this.SearchInternal(spatialQueryFilter);
		/// <summary>
		/// Gets the supported GeometryType if there is one, otherwise Unknown
		/// </summary>
		/// <remarks>Plugins returning a geometry type get a FeatureClass (which is also a Table) wrapper 
		/// and can be used as data sources for layers. Plugins returning a geometry type of Unknown
		/// get a Table wrapper and can be used as data sources for StandAloneTables only.</remarks>
		/// <returns></returns>
		public override GeometryType GetShapeType() {
			//Note: empty tables treated as non-geometry
			return _shapeType;
		}

		/// <summary>
		/// Get the extent for the dataset (if it has one)
		/// </summary>
		/// <remarks>Ideally, your plugin table should return an extent even if it is
		/// empty</remarks>
		/// <returns><see cref="Envelope"/></returns>
		public override Envelope GetExtent() {
			if (this.GetShapeType() != GeometryType.Unknown) {
				if (_gisExtent == null) {
					_gisExtent = _extent.ToEsriEnvelope(_sr, _hasZ);
				}
			}
			return _gisExtent;
		}

		/// <summary>
		/// Get the collection of fields accessible on the plugin table
		/// </summary>
		/// <remarks>The order of returned columns in any rows must match the
		/// order of the fields specified from GetFields()</remarks>
		/// <returns><see cref="IReadOnlyList{PluginField}"/></returns>
		public override IReadOnlyList<PluginField> GetFields() {
			// TODO Assumption: field list will be the same for all features stored in the same feature service (virtual table)
			var pluginFields = new List<PluginField>();
			
			foreach (var col in _table.Columns.Cast<DataColumn>()) {
				// Most fields will be strings
				FieldType fieldType = FieldType.String;

				// Special rule for geometry, OID, timestamp fields
				if (col.ColumnName == Properties.Settings.Default.FieldName_Shape) {
					fieldType = FieldType.Geometry;
				} else if (col.ColumnName == Properties.Settings.Default.FieldName_OID) {
					fieldType = FieldType.OID;
				} else if (col.ColumnName == Properties.Settings.Default.FieldName_Timestamp) {
					fieldType = FieldType.Date;
				}
				pluginFields.Add(new PluginField() {
					Name = col.ColumnName,
					AliasName = col.Caption,
					FieldType = fieldType
				});
			}

			return pluginFields;
		}

		#region IPluginRowProvider

		/// <summary>
		/// Custom interface specific to the way the sample is implemented.
		/// </summary>
		public PluginRow FindRow(int oid, IEnumerable<string> columnFilter, SpatialReference srout) {
			Geometry shape = null;

			List<object> values = new List<object>();
			var row = _table.Rows.Find(oid);
			//The order of the columns in the returned rows ~must~ match
			//GetFields. If a column is filtered out, an empty placeholder must
			//still be provided even though the actual value is skipped
			var columnNames = this.GetFields().Select(col => col.Name.ToUpper()).ToList();

			foreach (var colName in columnNames) {
				if (columnFilter.Contains(colName)) {
					//special handling for shape
					if (colName.Equals(Properties.Settings.Default.FieldName_Shape, StringComparison.CurrentCultureIgnoreCase)) {
						var buffer = row[Properties.Settings.Default.FieldName_Shape] as Byte[];
						shape = GeometryEngine.Instance.ImportFromEsriShape(EsriShapeImportFlags.esriShapeImportDefaults, buffer, _sr);
						if (srout != null) {
							if (!srout.Equals(_sr))
								shape = GeometryEngine.Instance.Project(shape, srout);
						}
						values.Add(shape);
					} else {
						values.Add(row[colName]);
					}
				} else {
					values.Add(System.DBNull.Value); //place holder
				}
			}
			return new PluginRow() { Values = values };
		}

		#endregion IPluginRowProvider

		#region Private
		/// <summary>
		/// Change a JSON attribute name to conform to ArcGIS field-naming restrictions
		/// </summary>
		/// <param name="sTableName"></param>
		/// <returns></returns>
		private string CleanFieldName(string sTableName) {
			// see https://support.esri.com/en/technical-article/000005588
			// see https://desktop.arcgis.com/en/arcmap/latest/manage-data/tables/fundamentals-of-adding-and-deleting-fields.htm#GUID-8E190093-8F8F-4132-AF4F-B0C9220F76B3
			string sCleanName = sTableName.Replace(" ", "_");
			char first = sCleanName.First();
			while (!char.IsLetterOrDigit(first) && first.ToString() != "_") {
				sCleanName = sCleanName.Remove(0, 1);
			}
			// No longer than 64 chars
			if (sCleanName.Length > 64) sCleanName = sCleanName.Substring(0, 64);
			return sCleanName;
		}
		private void InitializeFeatureColumns(string sFeat) {
			// Add the columns we pulled from SQLite; the rest will come from the Feature data
			DataColumn oid = new DataColumn(Properties.Settings.Default.FieldName_OID, typeof(Int32));
			_table.Columns.Add(oid);
			_table.PrimaryKey = new DataColumn[] { oid };
			_table.Columns.Add(Properties.Settings.Default.FieldName_FeatureID, typeof(string));
			_table.Columns.Add(Properties.Settings.Default.FieldName_Timestamp, typeof(DateTime));
			_table.Columns.Add(Properties.Settings.Default.FieldName_ErrorMsg, typeof(string));
			_table.Columns.Add(Properties.Settings.Default.FieldName_att_FileName, typeof(string));
			// Add a shape column
			_table.Columns.Add(Properties.Settings.Default.FieldName_Shape, typeof(Byte[]));
			// Now read through the feature attributes to get the rest of the columns
			dynamic feature = JObject.Parse(sFeat);
			dynamic attrs = feature.attributes;
			foreach (dynamic attr in attrs) {
				// TODO Use schema info once available, to add columns of type other than string
				_table.Columns.Add(CleanFieldName(attr.Name), typeof(string));
			}
		}
		/// <summary>
		/// Get basic information about the kind of geometry in this table
		/// </summary>
		/// <param name="sFeatId">The FeatureID value from SQLite</param>
		/// <param name="sGeom">Geometry JSON from "Feature" field</param>
		private void GetGeometryTypeInfo(string sFeatId, string sGeom) {
			// TODO Assumption: geometry is in a JSON format that can be imported by GeometryEngine.ImportFromJSON()
			// see https://developers.arcgis.com/documentation/common-data-types/geometry-objects.htm
			//JObject geom = (JObject)JObject.Parse(sFeat).GetValue("geometry");
			//JToken tknCoord, tknPaths, tknRings;
			//if (geom.TryGetValue("x", out tknCoord) && geom.TryGetValue("y", out tknCoord)) {
			//	// Point: x, y, optional z
			//	_shapeType = GeometryType.Point;
			//	_hasZ = geom.TryGetValue("z", out tknCoord);
			//} else if (geom.TryGetValue("paths", out tknPaths)) {
			//	// Polyline: array of array of points
			//	_shapeType = GeometryType.Polyline;
			//	_hasZ = tknPaths.First.First.Count() > 2;
			//} else if (geom.TryGetValue("rings", out tknRings)) {
			//	// Polygon: array of array of points
			//	_shapeType = GeometryType.Polygon;
			//	_hasZ = tknPaths.First.First.Count() > 2;
			//} else { // Not a valid geometry type
			//	throw new Exception($"Feature ID {sFeatId} isn't of type MapPoint, Polyline, or Polygon");
			//}
			Geometry geom = GeometryEngine.Instance.ImportFromJSON(JSONImportFlags.jsonImportDefaults, sGeom);
			_shapeType = geom.GeometryType;
			_hasZ = geom.HasZ;
			_sr = geom.SpatialReference;

			// Domain to verify coordinates (2D boundary only)
			_sr_extent = new RBush.Envelope(
			  minX: _sr.Domain.XMin,
			  minY: _sr.Domain.YMin,
			  maxX: _sr.Domain.XMax,
			  maxY: _sr.Domain.YMax
			);
			// Default to the Spatial Reference domain
			_extent = _sr_extent;
		}

		// TODO Assumption: timestamps are in 1970 epoch milliseconds, UTC
		private readonly DateTime EPOCH_START = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		private void Open() {
			// Initialize our data table
			_table = new DataTable();

			//Read in the data
			// TODO Assumption: SQLite Feature, FeatureId, Timestamp, ErrorMessage, FileName field names will always be the same
			string sQryTable =
				$"SELECT f.{Properties.Settings.Default.FieldName_OID}, f.{Properties.Settings.Default.FieldName_Feature}, "
				+ $"f.{Properties.Settings.Default.FieldName_FeatureID}, f.{Properties.Settings.Default.FieldName_Timestamp}, "
				+ $"f.{ Properties.Settings.Default.FieldName_ErrorMsg}, a.{Properties.Settings.Default.FieldName_att_FileName} "
				+ $"FROM {Properties.Settings.Default.TableName_Features} AS f LEFT JOIN {Properties.Settings.Default.TableName_Attachments} AS a "
				+ $"ON f.{Properties.Settings.Default.FieldName_FeatureID} = a.{Properties.Settings.Default.FieldName_att_FeatureID} "
				+ $"WHERE f.{Properties.Settings.Default.FieldName_FeatureSvcLayer} = '{_featSvcUrl}' "
				+ $"AND f.{Properties.Settings.Default.FieldName_Feature} IS NOT NULL ";

			using (SQLiteCommand cmd = _dbConn.CreateCommand()) {
				cmd.CommandText = sQryTable;
				cmd.CommandType = CommandType.Text;
				SQLiteDataReader reader = cmd.ExecuteReader();
				List<RBushCoord3D> coordsToBulkAdd = new List<RBushCoord3D>();
				while (reader.Read()) {
					string sFeat = reader[Properties.Settings.Default.FieldName_Feature].ToString();
					string sFeatId = reader[Properties.Settings.Default.FieldName_FeatureID].ToString();
					// TODO Assumption: okay to skip a feature error row with no data in Feature field - not true - need to provide w/null geometry
					if (sFeat.Length > 0) {
						// TODO Assumption: if feature data exists, it will be well-formed and correct - need to present nulls for invalid data
						JObject feat = JObject.Parse(sFeat);
						string sGeom = feat.Value<JObject>("geometry").ToString();
						if (_table.Columns.Count == 0) { // Need to set up columns
							InitializeFeatureColumns(sFeat);
							// Also get basic info about the geometry: point/line/polygon, has z values, ...
							GetGeometryTypeInfo(sFeatId, sGeom);
						}

						// Read data rows
						DataRow row = _table.NewRow();
						// TODO Assumption: since ArcGIS treats oids as int32, there won't ever be so many SQLite (long) rowids that they can't be cast to int32s
						// See http://desktop.arcgis.com/en/arcmap/latest/manage-data/databases/dbms-data-types-supported.htm#ESRI_SECTION1_E1310ADFB340464485BA2D2D167C9AE4
						Int64 loid = (Int64)reader[Properties.Settings.Default.FieldName_OID];
						Int32 oid = (Int32)loid;
						row[Properties.Settings.Default.FieldName_OID] = oid;
						row[Properties.Settings.Default.FieldName_FeatureID] = sFeatId;
						row[Properties.Settings.Default.FieldName_ErrorMsg] = reader[Properties.Settings.Default.FieldName_ErrorMsg].ToString();

						string sAttachment = reader[Properties.Settings.Default.FieldName_att_FileName].ToString();
						if (sAttachment.Length > 0)
							row[Properties.Settings.Default.FieldName_att_FileName] = Path.Combine(_attachmentsDir, sAttachment);

						DateTime? timestamp = null;
						try {
							long millis = Int64.Parse(reader[Properties.Settings.Default.FieldName_Timestamp].ToString());
							timestamp = EPOCH_START.AddMilliseconds(millis);
						} finally {
							row[Properties.Settings.Default.FieldName_Timestamp] = timestamp;
						}
						
						// Shape
						Geometry geom = BuildFeatureGeometry(sGeom);
						RBush.Envelope envThisFeature = new RBush.Envelope(geom.Extent.XMin, geom.Extent.YMin, geom.Extent.XMax, geom.Extent.YMax);
						if (!_sr_extent.Contains(envThisFeature))
							throw new ArcGIS.Core.Data.GeodatabaseFeatureException(
							  $"Feature ID {sFeatId} falls outside the defined spatial reference ({_sr.Wkid})");
						else {
							// Add geometry to datarow
							row[Properties.Settings.Default.FieldName_Shape] = geom.ToEsriShape();
							// And add it to the list to add to the index
							RBushCoord3D rbushCoord;
							switch (geom.GeometryType) {
								case GeometryType.Point:
									rbushCoord = new RBushCoord3D(new Coordinate3D((MapPoint)geom), oid);
									coordsToBulkAdd.Add(rbushCoord);
									break;
								case GeometryType.Polyline:
								case GeometryType.Polygon:
									foreach (MapPoint pt in ((Multipart)geom).Points) {
										rbushCoord = new RBushCoord3D(new Coordinate3D(pt), oid);
										coordsToBulkAdd.Add(rbushCoord);
									}
									break;
							}
							// Update extent
							if (_extent.Equals(_sr_extent)) {
								_extent = envThisFeature;
							} else {
								_extent = _extent.Union2D(envThisFeature);
							}
							//    var rbushCoord = new RBushCoord3D(coord, (int)row["OBJECTID"]);
							//    _rtree.Insert(rbushCoord);

							//    //update max and min for use in the extent
							//    if (_rtree.Count == 1)
							//    {
							//      //first record
							//      _extent = rbushCoord.Envelope;
							//    }
							//    else
							//    {
							//      _extent = rbushCoord.Envelope.Union2D(_extent);
							//    }
							//  }
						}

						// Attributes
						JObject attrs = feat.Value<JObject>("attributes");
						foreach (dynamic attr in attrs) {
							string sFldName = CleanFieldName(attr.Key);
							if (_table.Columns.Contains(sFldName)) {
								row[sFldName] = attr.Value.ToString();
							}
						}

						_table.Rows.Add(row);
					}
				}
				// Update spatial index
				_rtree.BulkLoad(coordsToBulkAdd);
			}
		}

		private Geometry BuildFeatureGeometry(string sGeom) {
			// TODO Assumption: all QuickCapture features have geometries - need to test w/null geometries
			// If we've gotten here, the JSON should have a point, polyline, or polygon geometry
			Geometry geom = GeometryEngine.Instance.ImportFromJSON(JSONImportFlags.jsonImportDefaults, sGeom);
			return geom;
		}

		private PluginCursorTemplate SearchInternal(QueryFilter qf) {
			var oids = this.ExecuteQuery(qf);
			var columns = this.GetQuerySubFields(qf);

			return new ProPluginCursorTemplate(this,
											oids,
											columns,
											qf.OutputSpatialReference);
		}

		/// <summary>
		/// Implement querying with a query filter
		/// </summary>
		/// <param name="qf"></param>
		/// <returns></returns>
		private List<int> ExecuteQuery(QueryFilter qf) {
			string OIDFIELD = Properties.Settings.Default.FieldName_OID;

			//are we empty?
			if (_table.Rows.Count == 0)
				return new List<int>();

			SpatialQueryFilter sqf = null;
			if (qf is SpatialQueryFilter) {
				sqf = qf as SpatialQueryFilter;
			}

			List<int> result = new List<int>();
			bool emptyQuery = true;

			//fidset - this takes precedence over anything else in
			//the query. If a fid set is specified then all selections
			//for the given query are intersections from the fidset
			if (qf.ObjectIDs.Count() > 0) {
				emptyQuery = false;

				result = null;
				result = _table.AsEnumerable().Where(
				  row => qf.ObjectIDs.Contains((int)row[OIDFIELD]))
				  .Select(row => (int)row[OIDFIELD]).ToList();

				//anything selected?
				if (result.Count() == 0) {
					//no - specifying a fidset trumps everything. The client
					//specified a fidset and nothing was selected so we are done
					return result;
				}
			}

			//where clause
			if (!string.IsNullOrEmpty(qf.WhereClause)) {
				emptyQuery = false;
				var sort = OIDFIELD;//default
				if (!string.IsNullOrEmpty(qf.PostfixClause)) {
					//The underlying System.Data.DataTable used by the sample supports "ORDER BY"
					//It should be a comma-separated list of column names and a default direction
					//COL1 ASC, COL2 DESC  (note: "ASC" is not strictly necessary)
					//Anything else and there will be an exception
					sort = qf.PostfixClause;
				}

				//do the selection
				var oids = _table.Select(qf.WhereClause, sort)
							 .Select(row => (int)row[OIDFIELD]).ToList();

				//consolidate whereclause selection with fidset
				if (result.Count > 0 && oids.Count() > 0) {
					var temp = result.Intersect(oids).ToList();
					result = null;
					result = temp;
				} else {
					result = null;
					result = oids;
				}

				//anything selected?
				if (result.Count() == 0) {
					//no - where clause returned no rows or returned no rows
					//common to the specified fidset
					return result;
				}
			}

			//filter geometry for spatial select
			if (sqf != null) {
				if (sqf.FilterGeometry != null) {
					emptyQuery = false;

					bool filterIsEnvelope = sqf.FilterGeometry is Envelope;
					//search spatial index first
					// TODO question: does RBush support projections other than WGS84 Geographic?
					var extent = sqf.FilterGeometry.Extent;
					var candidates = _rtree.Search(extent.ToRBushEnvelope());

					//consolidate filter selection with current fidset
					if (result.Count > 0 && candidates.Count > 0) {
						var temp = candidates.Where(pt => result.Contains(pt.ObjectID)).ToList();
						candidates = null;
						candidates = temp;
					}
					//anything selected?
					if (candidates.Count == 0) {
						//no - filter query returned no rows or returned no rows
						//common to the specified fidset
						return new List<int>();
					}

					//do we need to refine the spatial search?
					if (filterIsEnvelope &&
					  (sqf.SpatialRelationship == SpatialRelationship.Intersects ||
					  sqf.SpatialRelationship == SpatialRelationship.IndexIntersects ||
					  sqf.SpatialRelationship == SpatialRelationship.EnvelopeIntersects)) {
						//no. This is our final list
						List<int> oidvals = candidates.Select(pt => pt.ObjectID).OrderBy(oid => oid).Distinct().ToList();
						return oidvals;
					}

					//refine based on the exact geometry and relationship
					List<int> oids = new List<int>();
					//foreach (var candidate in candidates) {
					foreach (DataRow row in _table.Rows) {
						Byte[] geomBytes = (Byte[])row[Properties.Settings.Default.FieldName_Shape];
						Geometry geom = GeometryEngine.Instance.ImportFromEsriShape(EsriShapeImportFlags.esriShapeImportDefaults, geomBytes, _sr);
						if (GeometryEngine.Instance.HasRelationship(
								sqf.FilterGeometry, geom,
								  sqf.SpatialRelationship)) {
							oids.Add((int)row[Properties.Settings.Default.FieldName_OID]);
						}
					}
					//anything selected?
					if (oids.Count == 0) {
						//no - further processing of the filter geometry query
						//returned no rows
						return new List<int>();
					}
					result = null;
					//oids has already been consolidated with any specified fidset
					result = oids;
				}
			}

			//last chance - did we execute any type of query?
			if (emptyQuery) {
				//no - the default is to return all rows
				result = null;
				result = _table.Rows.Cast<DataRow>()
				  .Select(row => (int)row[OIDFIELD]).OrderBy(x => x).ToList();
			}
			return result;
		}

		private List<string> GetQuerySubFields(QueryFilter qf) {
			//Honor Subfields in Query Filter
			string columns = qf.SubFields ?? "*";
			List<string> subFields;
			if (columns == "*") {
				subFields = this.GetFields().Select(col => col.Name.ToUpper()).ToList();
			} else {
				var names = columns.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				subFields = names.Select(n => n.ToUpper()).ToList();
			}

			return subFields;
		}

		#endregion Private

		#region IDisposable

		private bool _disposed = false;
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			System.Diagnostics.Debug.WriteLine("Table being disposed");

			if (_disposed)
				return;

			if (disposing) {
				_table?.Clear();
                _table?.Dispose();
				_table = null;                
				_rtree?.Clear();
				_rtree = null;
				_sr = null;
				_gisExtent = null;
			}
			_disposed = true;
		}
		#endregion
	}
}
