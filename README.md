# Radonix NT CNC Interfaces

Editable CNC operator interfaces for Radonix NT on Windows x64. Browse the C# WinForms applications, machine simulation, PLC programs, tests, and Inno Setup sources.

| Interface | Machine configuration | Source |
| --- | --- | --- |
| CutterXYZ | 3-axis XYZ cutter | [Browse](interfaces/CutterXYZ) |
| RouterXYZ+TC | 3-axis XYZ router with tool change | [Browse](interfaces/RouterXYZ%2BTC) |
| RouterXYZAC+TC | 5-axis XYZAC router with tool change | [Browse](interfaces/RouterXYZAC%2BTC) |
| RouterXYZBC+TC | 5-axis XYZBC router with tool change | [Browse](interfaces/RouterXYZBC%2BTC) |
| RouterXYZAC+RTCP | 5-axis XYZAC router with RTCP | [Browse](interfaces/RouterXYZAC%2BRTCP) |

## Download and build

1. Open [Releases](https://github.com/RadonixNT/Source/releases) and choose the ZIP for your machine model.
2. Extract the model ZIP. For offline restore, also extract the shared `PublicPackages.zip` from the same release into the extracted model folder. To restore online, replace `NuGet.Config` with the included `NuGet.Online.Config`. Open the solution with Visual Studio and the .NET 8 Windows Desktop SDK.
3. Build the interface in Release x64. To compile an installer from your changes, use PowerShell 7 and Inno Setup 6 to run `Setup/BuildSetup.ps1`. Read the development guide in the ZIP for commands and prerequisites.

The model archives carry the matching Radonix binaries. The five models share the same third-party dependency archive, so you can reuse it for multiple models. For physical machine operation, obtain the compatible Acontis EC-Master/RTACC runtime through a licensed Radonix delivery or an authorized Acontis source and place its Windows x64 DLLs under `Runtime` before publishing or compiling the installer. Physical operation requires the applicable Radonix and Acontis licenses and machine commissioning. Third-party components retain their respective license terms.
