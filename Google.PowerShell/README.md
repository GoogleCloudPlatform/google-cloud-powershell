# Google.PowerShell

This assembly contains the cmdlets for manipulating Google Cloud Platform resources from PowerShell.

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

Note that these integration tests require you to be logged into a Google Cloud Platform account with
billing enabled, as it will actually perform storage commands. (e.g. creating new Cloud Storage buckets.)
