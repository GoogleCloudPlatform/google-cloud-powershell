#Requires -RunAsAdministrator
#
# Updates machine.config to put a machine-wide assembly redirect in place.
# This is to work around https://github.com/google/google-api-dotnet-client/issues/555
# while the fix is in progress.
#
# The script adds the following element to the .NET Framework's machine.config
# file for both x86 and x64 frameworks:
#  <runtime>
#    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
#      <dependentAssembly>
#        <assemblyIdentity name="System.Net.Http.Primitives" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
#        <bindingRedirect oldVersion="0.0.0.0-4.2.29.0" newVersion="4.2.29.0" />
#      </dependentAssembly>
#    </assemblyBinding>
#  </runtime>
#
# To restore configs, run the following commands from an elevated command prompt:
<#
del "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config"
move "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config.bak" "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config"
del "C:\Windows\Microsoft.NET\Framework\v4.0.30319\Config\machine.config"
move "C:\Windows\Microsoft.NET\Framework\v4.0.30319\Config\machine.config.bak" "C:\Windows\Microsoft.NET\Framework\v4.0.30319\Config\machine.config"
#>

function UpdateMachineConfig($path) {
    # Back up the file before we modify it.
    copy $path ($path + ".bak")
    $xml = [xml](Get-Content($path))
    
    $runtime = $xml.configuration["runtime"]
    
    $assemblyBinding = $xml.CreateElement("assemblyBinding")
    $assemblyBinding.SetAttribute("xmlns", "urn:schemas-microsoft-com:asm.v1")

    $dependentAssembly = $xml.CreateElement("dependentAssembly")

    $assemblyIdentity = $xml.CreateElement("assemblyIdentity")
    $assemblyIdentity.SetAttribute("name", "System.Net.Http.Primitives")
    $assemblyIdentity.SetAttribute("publicKeyToken", "b03f5f7f11d50a3a")
    $assemblyIdentity.SetAttribute("cultural", "neutral")

    $bindingRedirect = $xml.CreateElement("bindingRedirect")
    $bindingRedirect.SetAttribute("oldVersion", "0.0.0.0-4.2.29.0")
    $bindingRedirect.SetAttribute("newVersion", "4.2.29.0")

    $dependentAssembly.AppendChild($assemblyIdentity)
    $dependentAssembly.AppendChild($bindingRedirect)
    $assemblyBinding.AppendChild($dependentAssembly)
    $runtime.AppendChild($assemblyBinding)

    $xml.Save($path)
}

$path = "$Env:WinDir\Microsoft.NET\Framework\v4.0.30319\Config\machine.config"
Write-Host "Updating file: ${path}"
UpdateMachineConfig $path | Out-Null

$path = "$Env:WinDir\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config"
Write-Host "Updating file: ${path}"
UpdateMachineConfig $path | Out-Null

Write-Host "Done"
pause
