# Copyright 2015 Google Inc. All Rights Reserved.
# Licensed under the Apache License Version 2.0.
#
# Bootstraps the Google Cloud cmdlets into the current PowerShell session.

function Get-ScriptDirectory
{
    $invocation = (Get-Variable MyInvocation -Scope 1).Value
    return Split-Path $invocation.MyCommand.Path
}

$modulePath = Join-Path (Get-ScriptDirectory) "GoogleCloudPowerShell.psd1"
Import-Module $modulePath

cd c:\
clear

$welcomeBanner = "Google Cloud PowerShell"
Write-Output $welcomeBanner
