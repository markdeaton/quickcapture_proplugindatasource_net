# QuickCapture Errors Plugin Datasource

This project displays QuickCapture errors and photo attachments stored in a SQLite database, allowing for review.

The Visual Studio solution contains three projects:
* The plugin datasource (which is responsible for reading the sqlite data and presenting it to ArcGIS Pro as tables)
* The custom catalog items
* A file-browse button and dialog that acts as a secondary way to open a .qcr error archive

The file-browse button and dialog is not expected to be needed now that a custom catalog item is available. There is a customized build option that excludes that project from the build.

Make sure to choose and use a build option targeting the x64 platform, as that is the only option that will properly package and install the necessary SQLite support libraries and interop assembly for ArcGIS Pro.

## Licensing
This solution's projects use some third-party components, made available under various licenses.
* RBush (spatial indexer) - [MIT License](https://github.com/viceroypenguin/RBush/blob/master/LICENSE)
* Newtonsoft.Json - [MIT License](https://licenses.nuget.org/MIT)
* SQLite Core - [Public Domain](https://www.sqlite.org/copyright.html)
