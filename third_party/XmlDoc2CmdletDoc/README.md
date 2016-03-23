# XmlDoc2CmdletDoc

Binary release of the **XmlDoc2CmdletDoc** tool. Build from [red-gate/XmlDoc2CmdletDoc](https://github.com/red-gate/XmlDoc2CmdletDoc)
at commit [954951c017](https://github.com/red-gate/XmlDoc2CmdletDoc/commit/954951c017669c89e17c7ce520b891782c667d2f).

---

It's easy to write good help documentation for PowerShell *script* modules (those written in the PowerShell script language). You just write specially formatted comments alongside the source code for your cmdlets, and the PowerShell host automatically uses those comments to provide good inline help for your cmdlets' users. **XmlDoc2CmdletDoc** brings this same functionality to PowerShell *binary* modules (those written in C# or VB.NET). You no longer need to use *CmdletHelpEditor* or *PowerShell Cmdlet Help Editor* to manually edit a separate help file. Instead, this tool will automatically generate your PowerShell module's help file from XML Doc comments in your source code.

For more details, [Michael Sorens](https://www.simple-talk.com/author/michael-sorens/) has written a [comprehensive guide to documenting your PowerShell binary cmdlets](https://www.simple-talk.com/dotnet/software-tools/documenting-your-powershell-binary-cmdlets/) using XmlDoc2CmdletDoc.

To create a .dll-Help.xml file for your binary PowerShell module, simply call:

```batchfile
XmlDoc2CmdletDoc.exe C:\Full\Path\To\MyPowerShellModule.dll
```
