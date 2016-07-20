# Integration Tests#

The suite of integration use the [Pester](https://github.com/pester/Pester)
framework.

The easiest way to install it on your system is via the
[Choclatey package](https://chocolatey.org/packages/pester).

## Running Tests

Once installed, you should be able to run Pester by typing `Invoke-Pester` or
simply `pester`.

Pester will run all Pester tests under the current folder (those matching
`*.Tests.ps1`). You can specify a limited set of tests by passing a file,
directory, or wildcard to the pester path:

    pester .\Google.PowerShell.IntegrationTests\Storage\

If you only want to run a single testcase, use the `-TestName` parameter.

    pester \Google.PowerShell.IntegrationTests\Storage\ -TestName Read-GcsObject

## Troubleshooting

However, you may need to set the PowerShell execution policy.
Simply run:

    Set-ExecutionPolicy Unrestricted

But you need to do this for _the x86 version of PowerShell_ that Visual Studio
runs, so run that command from:

    C:\Windows\syswow64\WindowsPowerShell\v1.0\powershell.exe

