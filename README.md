# SDK for HCC2 in C# (dotnet 6)

This packages provides the required interface between an application and HCC2 data using HCC2 modbus server as the data source.

This package must be installed along with the application:

### Create package source

for the first time, a package source must be created:
```
cd hcc2sdkcs
dotnet nuget add source $(pwd)/SDKPackages --name SDKPackages
```

### Build

To build a new version of this package:
```
cd hcc2sdkcs
### To package the release version of the SDK run the following (Recommended)
dotnet pack hcc2sdkcs.csproj -c Release -o SDKPackages

### To package the debug version of the SDK run
dotnet pack hcc2sdkcs.csproj -c Debug -o SDKPackages

It is advised to use the Release build for production releases.

```

The output of this build is the hcc2sdkcs/SDKPackages/ folder.
### Notes

1. We recommend not to change this code. 
2. Should you need to build a new version, you must then copy it into the applications that needs it.
3. Package installation into the application:
```
dotnet add package hcc2sdkcs --version "X.X.X" --source "SDKPackages" 
```
(X.X.X is the package version number)
