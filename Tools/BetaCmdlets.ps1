# List of beta cmdlets.
$cmdletsToBeExported = @("Get-GcLogEntry", "New-GcLogEntry", "New-GcLogMonitoredResource",
                         "Get-GcLog", "Remove-GcLog", "New-GcLogSink", "Get-GcLogSink",
                         "Update-GcLogSink", "Remove-GcLogSink", "Get-GcpsTopic",
                         "New-GcpsTopic", "Remove-GcpsTopic", "New-GcpsSubscription",
                         "Get-GcpsSubscription", "Set-GcpsSubscriptionConfig",
                         "Remove-GcpsSubscription", "New-GcpsMessage", "Publish-GcpsMessage",
                         "Get-GcpsMessage", "Set-GcpsAckDeadline", "Send-GcpsAck"
                         "Get-GcIamPolicyBinding", "Add-GcIamPolicyBinding",
                         "Get-BqDataset", "Set-BqDataset", "New-BqDataset", "Remove-BqDataset",
                         "Get-BqTable", "Set-BqTable", "New-BqTable", "Remove-BqTable",
                         "New-BqSchema", "Set-BqSchema", "Add-BqTableRows", "Get-BqTableRows",
                         "Get-BqJob", "Start-BqJob", "Receive-BqJob", "Stop-BqJob")
$cmdletsToBeExported
