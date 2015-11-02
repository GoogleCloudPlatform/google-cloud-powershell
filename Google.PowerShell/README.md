# Google.PowerShell

This assembly contains the cmdlets for manipulating Google Cloud Platform resources from PowerShell.

# Installation

Horrible hack you need to add to your `machine.config` file to get PowerShell cmdlets to work.
This is because of [Issue #555](https://github.com/google/google-api-dotnet-client/issues/555).

As an admin, edit: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config`.

And replace the `<runtime/>` element with:

  <!-- BEGIN HACK -->
  <!-- http://stackoverflow.com/questions/18542812/powershell-cmdlet-missing-assembly-google-api -->
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http.Primitives" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.2.29.0" newVersion="4.2.29.0" />Sol
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="Google.Apis" publicKeyToken="4b01fa6e34db77ab" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-1.9.3.19379" newVersion="1.9.3.19379" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="Google.Apis.Core" publicKeyToken="4b01fa6e34db77ab" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-1.9.3.19379" newVersion="1.9.3.19379" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="Google.Apis.PlatformServices" publicKeyToken="4b01fa6e34db77ab" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-1.9.2.27818" newVersion="1.9.2.27818" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
  <!-- END HACK -->

And, because this is all fucking bullshit, add the following assembly redirect to the Test Explorer
process as well. (Not sure why it isn't picking it up from the machine.config.)

"notepad C:\PROGRAM FILES (X86)\MICROSOFT VISUAL STUDIO 14.0\COMMON7\IDE\COMMONEXTENSIONS\MICROSOFT\TESTWINDOW\te.processhost.managed.exe.Config"

      <dependentAssembly>
        <assemblyIdentity name="Google.Apis.Core" publicKeyToken="4b01fa6e34db77ab" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-1.9.3.19379" newVersion="1.9.3.19379" />
      </dependentAssembly>

## References

Because of versioning woes, you need the latest version of the core Google client libraries, but an older
version of the API-specific libraries.

https://www.nuget.org/packages/Google.Apis.Core
https://www.nuget.org/packages/Google.Apis.Storage.v1/
https://www.nuget.org/packages/Google.Apis.Compute.v1/

    PM> Install-Package Google.Apis.Core -Version 1.9.3
	PM> Install-Package Google.Apis.Auth -Version 1.9.3
    PM> Install-Package Google.Apis
	PM> Install-Package Google.Apis.Storage.v1
    PM> Install-Package Google.Apis.Compute.v1

And then the PowerShell libraries:

    PM> Install-Package System.Management.Automation

# Building and Debugging

Debugging the `Google.PowerShell.dll` library can be done by pressing F5 within Visual Studio. This
will start the Power Shell with the Visual Studio debugger attached.

To enable F5 running, edit the project properties and on build launch external program:

    C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe

Set the command-line arguments:

    -noexit -command "&{ import-module .\Google.PowerShell.dll -verbose }"

## gsutil ##

To compare Google Cloud Storage commands with the gsutil tool, add the -D option. This will dump
HTTP requests which can be inspected to identify the exact request/parameters used. For example:

    gsutil -D acl get gs://test-bucket/

# Running Tests

## Unit Tests

The `Google.PowerShell.Tests.dll` unit test library is using NUnit v2.6.4. To run the tests within
Visual Studio in the Test Explorer window you need to install the `NUnit Test Adapter`. Go to
`Tools -> Extensions and Updates` and find it on the Visual Studio Gallery.

Then rebuild the solution, and the Unit Tests should show up in the Test Explorer.

## Integration Tests

The `Google.PowerShell.IntegrationTests` test library is using the Pester PowerShell test framework.
The tests described there will show up in the Test Explorer window after you install the
`PowerShell Tools for Visual Studio 2015`. Also through the Visual Studio gallery.
