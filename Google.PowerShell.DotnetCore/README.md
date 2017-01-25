# Google.PowerShell

This assembly contains the cmdlets for manipulating Google Cloud Platform resources from PowerShell running on .NET Core.

# Building

## On Windows

First, you will have to install [.NET Core SDK](https://www.microsoft.com/net/core#windows).

Then run the following commands in PowerShell:

```
cd .\Google.PowerShell.DotnetCore
dotnet restore
dotnet publish -r win10-x64 (or win81-x64 for Windows 8.1 and Windows Server 2012 R2)
```

The published folder will be returned on the terminal. Typically, this would be ```bin\Debug\netstandard1.6\win10-x64\publish``` or ```bin\Debug\netstandard1.6\win81-x64\publish```, depending on your runtime.

To run the module, you will first need to install [PowerShell Core](https://github.com/PowerShell/PowerShell/blob/master/docs/installation/windows.md#msi). After installing, run:
```
& "$env:ProgramFiles\PowerShell\<PowerShellVersion>\powershell.exe```"
Import-Module "<ProjectDirectory>\Google.PowerShell.DotnetCore\bin\Debug\netstandard1.6\win10-x64\publish\Google.PowerShell.dll"
```

## On Mac OS 10.11 (x64)
First, you will have to install [.NET Core SDK](https://www.microsoft.com/net/core#macos).

Then run the following commands in the terminal:
```
cd .\Google.PowerShell.DotnetCore
dotnet restore
dotnet publish --runtime osx.10.11-x64 --configuration Linux
```

The published folder will be returned on the terminal. Typically, this would be ```bin\Linux\netstandard1.6\osx.10.11-x64\publish```.

To run the module, you will first need to install [PowerShell Core](https://github.com/PowerShell/PowerShell/blob/master/docs/installation/linux.md#macos-1011). After installing, run:
```
powershell
Import-Module "<ProjectDirectory>\Google.PowerShell.DotnetCore\bin\Linux\netstandard1.6\win10-x64\publish\Google.PowerShell.dll"
```

## On Ubuntu 16.04 (x64)
First, you will have to install [.NET Core SDK](https://www.microsoft.com/net/core#ubuntu).

You will also have to install OpenSSL:

```apt-get install libcurl4-openssl-dev```

Then run the following commands in the terminal:
```
cd .\Google.PowerShell.DotnetCore
dotnet restore
dotnet publish --runtime ubuntu.16.04-x64 --configuration Linux
```

The published folder will be returned on the terminal. Typically, this would be ```bin\Linux\netstandard1.6\ubuntu.16.04-x64\publish```.

To run the module, you will first need to install [PowerShell Core](https://github.com/PowerShell/PowerShell/blob/master/docs/installation/linux.md#ubuntu-1604). After installing, run:
```
powershell
Import-Module "<ProjectDirectory>\Google.PowerShell.DotnetCore\bin\Linux\netstandard1.6\ubuntu.16.04-x64\publish\Google.PowerShell.dll"
```
