#Integration Tests#

To run these just type `Invoke-Pester`. Things should work. If they don't, that is bad.

You may need to set the PowerShell execution policy. Simply run:

    Set-ExecutionPolicy Unrestricted

But you need to do this for _the x86 version of PowerShell_ that Visual Studio runs, so
run that command from:

    C:\Windows\syswow64\WindowsPowerShell\v1.0\powershell.exe

If you only want to run a subset of the integration tests, use the `-TestName` parameter. e.g.

    pester -TestName Read-GcsObject

