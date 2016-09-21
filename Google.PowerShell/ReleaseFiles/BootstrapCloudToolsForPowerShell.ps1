# Copyright 2015 Google Inc. All Rights Reserved.
# Licensed under the Apache License Version 2.0.
#
# Bootstraps the Google Cloud cmdlets into the current PowerShell session.

function Get-ScriptDirectory
{
    $invocation = (Get-Variable MyInvocation -Scope 1).Value
    return Split-Path $invocation.MyCommand.Path
}

$modulePath = Join-Path (Get-ScriptDirectory) "GoogleCloud.psd1"
Import-Module $modulePath

$Env:UserProfile
clear

Write-Output "Google Cloud Tools for PowerShell"
