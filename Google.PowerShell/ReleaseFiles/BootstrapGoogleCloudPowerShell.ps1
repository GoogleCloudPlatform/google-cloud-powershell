# Copyright 2015 Google Inc. All Rights Reserved.
# Licensed under the Apache License Version 2.0.
#
# Bootstraps the Google Cloud cmdlets into the current PowerShell session.

$modulePath = Get-ChildItem $PSScriptRoot -Include "GoogleCloud.psd1" -Recurse | Select -First 1
Import-Module $modulePath

$Env:UserProfile
clear

$welcomeBanner = "Google Cloud PowerShell"
Write-Output $welcomeBanner
