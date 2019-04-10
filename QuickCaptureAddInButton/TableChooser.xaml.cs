using System.Collections.Generic;
using System.Windows.Controls;

namespace QuickCaptureAddInBrowseButton {
	/// <summary>
	/// Interaction logic for TableChooser.xaml
	/// </summary>
	public partial class TableChooser : ArcGIS.Desktop.Framework.Controls.ProWindow {
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="tables">Enumerable list of table name strings</param>
		public TableChooser(IReadOnlyList<string> tables) {
			InitializeComponent();
			lbTables.ItemsSource = tables;
		}

		public System.Collections.IList SelectedTableNames { get => lbTables.SelectedItems; }

		private void LbTables_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
			btnOK.IsEnabled = (((ListBox)sender).SelectedItems.Count > 0);
		}

		private void BtnCancel_Click(object sender, System.Windows.RoutedEventArgs e) {
			DialogResult = false;
		}

		private void BtnOK_Click(object sender, System.Windows.RoutedEventArgs e) {
			DialogResult = true;
		}
	}
}
