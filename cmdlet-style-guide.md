# PowerShell cmdlet Style Guide #

This guide outlines the our practices when writing PowerShell cmdlets. It is a
work in-progress, and if you have suggestions or altering opinions please feel
free to submit an issue for discussion.

[RFC2119][RFC2119]

## Cmdlets which destroy data MUST use [`SupportsShouldProcess`][SupportsShouldProcess].
## Cmdlets MUST call [`ShouldProcess`][ShouldProcess] before executing data destroying operations.
## [`ConfirmImpact`][ConfirmImpact] MAY be left default and SHOULD NOT be set to `High`.

In order to avoid accidentally destroying data (deleting a Google Cloud Storage Bucket,
deleting a Google Compute Engine Disk, etc.) it should be possible to prompt user to confirm the operation.

A cmdlet can call the [`ShouldProcess`][ShouldProcess] method to potentially prompt the user before proceeding.
Whether this prompt is triggered depends on the values of [`ConfirmImpact`][ConfirmImpact] and `$ConfirmPreference`.

Cmdlets decorated with a [`CmdletAttribute`][CmdletAttribute] that has [`SupportsShouldProcess`][SupportsShouldProcess]
set to `true` automatically get `-WhatIf` and `-Confirm` parameters,
which modify the functionality of [`ShouldProcess`][ShouldProcess].

`-WhatIf` is used for a cmdlet to "go through the motions" of its operation, but
to not actually complete its action. It is used so you can test "what if" the
command were actually run. [`ShouldProcess`][ShouldProcess] automatically returns false.

Note that passing `-WhatIf` prevents the [`ShouldProcess`][ShouldProcess] prompt from appearing.

`-Confirm` is used to ensure the confirmation prompt of [`ShouldProcess`][ShouldProcess] appears,
regardless of the values of `$ConfirmPreference` and [`ConfirmImpact`][ConfirmImpact].

## Cmdlets SHOULD call [`ShouldContinue`][ShouldContinue] during unexpected/hidden data destroying operations.
## Cmdlets MUST have at least one operation path that does not hit a [`ShouldContinue`][ShouldContinue] call.
## Cmdlets calling [`ShouldContinue`][ShouldContinue] MUST add a `Force` parameter.
## The `Force` parameter MUST bypass the call to [`ShouldContinue`][ShouldContinue].

Cmdlets executing suprising data destroying operations,
such as deleting a Google Cloud Storage Bucket with Objects still inside,
should call the [`ShouldContinue`][ShouldContinue] method to prompt the user before proceeding.
This method ignores the ConfirmImpact setting.

[`ShouldContinue`][ShouldContinue] should be used sparingly, and only when a cmdlet unexpectidly is going beyond its obvious
contract. There must be at least one normal path a cmdlet can take that does not call [`ShouldContinue`][ShouldContinue].

````
# Removing a folder with a child item prompts before continuing.
PS C:\> Remove-Item "C:\Users\user\AppData\Local\Temp\VSD92CC.tmp"

Confirm
The item at C:\Users\user\AppData\Local\Temp\VSD92CC.tmp has children and the Recurse parameter was not specified.
If you continue, all children will be removed with the item. Are you sure you want to continue?
[Y] Yes  [A] Yes to All  [N] No  [L] No to All  [S] Suspend  [?] Help (default is "Y"): N

# No prompt for removing only the expected item.
PS C:\> Remove-Item "C:\Users\user\AppData\Local\Temp\VSD92CC.tmp\install.log"
PS C:\> Remove-Item "C:\Users\user\AppData\Local\Temp\VSD92CC.tmp"
PS C:\>
````


## Cmdlets MAY add a `Force` parameter to bypass basic restrictions.
## The `Force` parameter MUST be a [switch parameter][SwitchParameter].

There are classes of restrictions which are not that important, but should
prevent cmdlets from doing harm. For example, when copying a file if a file
already exists at the new location and has the read-only attribute set, the
cmdlet will fail.

However, by specifying the `Force` parameter, the cmdlet will override these
restrictions.

````
# Fails because alread-exists.txt has the read-only attribute set.
PS C:\> Copy-Item -Path temp.txt -Destination .\already-exists.txt
Copy-Item : Access to the path 'C:\already-exists.txt' is denied.
At line:1 char:1
+ Copy-Item -Path temp.txt -Destination .\already-exists.txt
+ ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : PermissionDenied: (C:\temp.txt:FileInfo) [Copy-Item], UnauthorizedAccessException
    + FullyQualifiedErrorId : CopyFileInfoItemUnauthorizedAccessError,Microsoft.PowerShell.Commands.CopyItemCommand

# Works with -Force
PS C:\> Copy-Item -Path temp.txt -Destination .\already-exists.txt -Force
````

## Cmdlet parameter names SHOULD be as simple as possible.

Keep parameter names as simple as possible. For example, if uploading a file,
avoid parameter names like `LocalFile` or `FileToUpload` and instead just
go with `File`.

At times a more descriptive name might be appropriate, such as `InputObjectName`
when coupled with `OutputObjectName`. But in general, avoid adjectives modifying
the parameter name if the purpose of the parameter is clear without it.

## Cmdlets SHOULD mark one parameter with [`ValueFromPipeline`][ValueFromPipeline] set to `true` for every parameter set.

The pipeline is a very useful feature of PowerShell, and cmdlets should endevor to make use of it.
Enabling usage of the pipeline is usually a simple matter of setting a parameters to have
[`ValueFromPipeline`][ValueFromPipeline] `=` `true`,
and ensuring the work requiring the pipelined parameter is done in the [`ProcessRecord`][ProcessRecord] method.
[`ProcessRecord`][ProcessRecord] is called for every pipeline value,
while [`BeginProcessing`][BeginProcessing] and [`EndProcessing`][EndProcessing] will leave the pipeline parameter
uninitalized.

## Cmdlets SHOULD prefer to use simple arrays such as `string[]` to generic lists such as `List<string>` for their parameters.

When using the Get-Help cmdlet, generic lists appear as ``List` ``, hiding the type they contain. Arrays, however,
appear as the correct `string[]`. Additionally, usage of the two from a user perspective is essentially
identical, due to PowerShell's type conversion magic.

[RFC2119]: http://www.ietf.org/rfc/rfc2119.txt
[CmdletAttribute]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdletattribute.aspx
[SupportsShouldProcess]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdletcommonmetadataattribute.supportsshouldprocess.aspx
[ConfirmImpact]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdletcommonmetadataattribute.confirmimpact.aspx
[ValueFromPipeline]: https://msdn.microsoft.com/en-us/library/system.management.automation.parameterattribute.valuefrompipeline
[BeginProcessing]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.beginprocessing
[ProcessRecord]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.processrecord
[EndProcessing]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.endprocessing
[ShouldProcess]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.shouldprocess.aspx
[ShouldContinue]: https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.shouldcontinue.aspx
[SwitchParameter]: https://msdn.microsoft.com/en-us/library/system.management.automation.switchparameter.aspx
