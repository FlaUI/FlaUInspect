# Command Line Interface (CLI) Usage

FlaUInspect supports several command line arguments for automation and quick launching.

## Launch Options

| Argument | Description |
|Data|Data|
| `--uia2` | Starts FlaUInspect using the UIA2 backend directly, skipping the selection dialog. |
| `--uia3` | Starts FlaUInspect using the UIA3 backend directly, skipping the selection dialog. |

## Process Export Options

FlaUInspect can be used to export the UI automation tree of a running process to a JSON file without opening the GUI.

| Argument | Description |
|Data|Data|
| `--process <ProcessName>` | Specifies the name of the process to attach to (e.g., `Notepad`). This is required for export operations. |
| `--export_json [filename]` | Triggers the JSON export. If a filename is provided, it writes to that file. Otherwise, it defaults to `export.json`. |
| `--export_json_options <Options>` | A comma-separated list of properties to include in the export. |

### Export Options

The following properties can be specified in `--export_json_options`:

*   `ControlType` (Default)
*   `ClassName` (Default)
*   `Name` (Default)
*   `AutomationId` (Default)
*   `HelpText`
*   `BoundingRectangle`
*   `ProcessId`
*   `IsEnabled`
*   `IsOffscreen`
*   `SupportedPatterns`

If `--export_json_options` is omitted, the default properties are used.

## Examples

**Launch with UIA3:**
```powershell
FlaUInspect.exe --uia3
```

**Export Notepad's UI tree to default `export.json`:**
```powershell
FlaUInspect.exe --process Notepad --export_json
```

**Export Calculator's UI tree to `calc-dump.json` including specific properties:**
```powershell
FlaUInspect.exe --process Calculator --export_json calc-dump.json --export_json_options "Name,AutomationId,BoundingRectangle,IsEnabled"
```
