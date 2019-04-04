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
using ArcGIS.Desktop.Framework;
using System.Runtime.InteropServices;

namespace QuickCapturePluginTest {
	/// <summary>
	/// QuickCapturePluginTest implements a custom plugin datasource for reading csv files.  
	/// QuickCapturePluginTest is an add-in that allows access to error data stored in a SQLite database.  
	/// QuickCapturePluginTest contains the actual custom plugin datasource implementation to access SQLite data from within ArcGIS Pro. 
	/// </summary>
	/// <remarks>
	/// 1. This solution requires C# 7.2.  Currently you have to manually switch to that language version by using the 'Build' tab under the project properties, then use the 'Advanded' button to change the language as shown below.
	/// ![UI](Screenshots/screen1.png)  
	/// 1. This solution is using the **RBush NuGet**.  If needed, you can install the NuGet from the "NuGet Package Manager Console" by using this script: "Install-Package RBush".
	/// 1. This solution is using the **System.Collections.Immutable NuGet**.  If needed, you can install the NuGet from the "NuGet Package Manager Console" by using this script: "Install-Package System.Collections.Immutable".
	/// 1. In Visual Studio click the Build menu. Then select Build Solution.
	/// 1. Click Start button to open ArcGIS Pro.
	/// 1. In ArcGIS Pro create a new Map using the Empty Map Template.
	/// 1. In Visual Studio set a break point inside the TestCsv1.OnClick code-behind.
	/// 1. In ArcGIS Pro click on the 'Debug Add-in Code' button.
	/// 1. You can now step through the code showing how to use a custom plugin in code.
	/// 1. In ArcGIS Pro click on the 'Add Plugin Data to Map' button.
	/// 1. The test is now added to the current map.
	/// 1. Use the test data on the map or via the attribute table.
	/// ![UI](Screenshots/screen2.png)  
	/// </remarks>
	internal class Module1 : ArcGIS.Desktop.Framework.Contracts.Module {
    private static Module1 _this = null;

    /// <summary>
    /// Retrieve the singleton instance to this module here
    /// </summary>
    public static Module1 Current
    {
      get
      {
				//// Underlying SQLite libraries are native. 
				//// Manually set the DLL load path depending on the process.
				//var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Native");
				//if (IntPtr.Size == 8) // or: if(Environment.Is64BitProcess) // .NET 4.0
				//{
				//	path = Path.Combine(path, "X64");
				//} else {
				//	// X32
				//	path = Path.Combine(path, "X86");
				//}
				//NativeMethods.SetDllDirectory(path);

        return _this ?? (_this = (Module1)FrameworkApplication.FindModule("QuickCapturePlugin_Module"));
	  }
    }

		//private static class NativeMethods {
		//	[DllImport("kernel32.dll", CallingConvention = CallingConvention.Cdecl)]
		//	internal static extern bool SetDllDirectory(string pathName);
		//}

		#region Overrides
		/// <summary>
		/// Called by Framework when ArcGIS Pro is closing
		/// </summary>
		/// <returns>False to prevent Pro from closing, otherwise True</returns>
		protected override bool CanUnload()
    {
      //TODO - add your business logic
      //return false to ~cancel~ Application close
      return true;
    }

    #endregion Overrides
  }
}
