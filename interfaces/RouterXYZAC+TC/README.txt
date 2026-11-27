RouterXYZAC+TC — customer development guide

Extract the complete ZIP to a writable folder on Windows x64. Install the .NET 8 SDK with Windows Desktop support, or Visual Studio 2022 (17.8 or later) with the .NET desktop development workload. Open RouterXYZAC+TC.sln and select Release | x64.

Command line from the extracted folder:
  dotnet restore "RouterXYZAC+TC.sln" --configfile NuGet.Config
  dotnet build "RouterXYZAC+TC.sln" -c Release -p:Platform=x64 --no-restore
  dotnet test "RouterXYZAC+TC.Simulation.Tests/RouterXYZAC+TC.Simulation.Tests.csproj" -c Release -p:Platform=x64 --no-build
  dotnet publish "RouterXYZAC+TC/RouterXYZAC+TC.csproj" -c Release -p:Platform=x64 -o publish

The runnable build is in RouterXYZAC+TC/bin/Release/net8.0-windows. Keep the output files together. The publish output is framework-dependent and requires the x64 .NET 8 Desktop Runtime on the target PC.

Customizing your copy
  RouterXYZAC+TC/MainForm*.cs and associated resources contain the operator UI.
  Localization/Catalogs, PLCPrograms, Configuration, Machining and Simulation under the application project contain translations, factory programs, settings, machining integration and simulation sources.
  Router-Development.md describes this model. Reference/index.html opens the operator manual.
  Keep DeliveryReferences.props, Runtime and NuGet.Config alongside the solution. Runtime contains the matched Radonix DLLs, API XML documentation, native Core and ENI tools. PublicPackages contains the public NuGet dependencies for the application and tests. Extract PublicPackages.zip from the same release into this directory before offline restore. Packages contains the matched nine Radonix 1.0.0 packages for separate package consumers.
  Delivery.json records original file hashes. Keep the original ZIP and edit a working copy.

Building your installer
  Install PowerShell 7 and Inno Setup 6 on the development PC. Run this command from the extracted delivery.
    pwsh -NoProfile -File ./Setup/BuildSetup.ps1 -Version 1.0.0
  Edit Setup/Setup.iss to customize the installer. BuildSetup publishes your current interface in Release x64 and compiles the installer to Setup/Output. Use -InnoSetupCompiler if ISCC.exe is installed in a custom location.
  The installer packages your application, its runtime binaries, the operator manual and the NT-ESI device descriptions. Install the x64 .NET 8 Desktop Runtime and Visual C++ Runtime on the target separately. Physical operation also requires the configured Acontis RTACC/NDIS environment, machine licenses, network configuration and machine validation.


Public preview and machine prerequisites
  This preview is intended for interface development and offline build verification.
  For physical machine operation, obtain the compatible Acontis EC-Master/RTACC
  runtime files through your licensed Radonix delivery or an authorized Acontis
  source. Copy those Windows x64 runtime DLLs into Runtime before publishing
  or building an installer for machine operation. The ZIP contains Radonix
  runtime files and the local NuGet dependencies needed to build the source.
  Physical operation also requires the applicable Radonix and Acontis runtime
  licenses and machine-specific commissioning.

NuGet restore options
  Offline: extract PublicPackages.zip into this model's extracted folder so
  PublicPackages and NuGet.Config are side by side. Run the restore and build
  commands above.
  Online: replace NuGet.Config with the included NuGet.Online.Config before
  building the solution or installer. The online configuration uses nuget.org
  for third-party dependencies and the local Packages folder for Radonix.
