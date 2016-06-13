# PowerShell cmdlet Style Guide #

This guide outlines the our practices when writing PowerShell cmdlets. It is a
work in-progress, and if you have suggestions or altering opinions please feel
free to submit an issue for discussion.

[RFC2119](http://www.ietf.org/rfc/rfc2119.txt)

# Cmdlets which destroy data MUST use `SupportsShouldProcess`
# Cmdlets MUST call `ShouldProcess` during data destroying situations (e.g. Deleting a GceDisk or GcsBucket).

In order to avoid accidentally destroying data (deleting a file, shutting down a
virtual machine, etc.) users should be prompt to confirm the operation.

Cmdlets decorated with `Cmdlet`'s [SupportsShouldProcess](https://msdn.microsoft.com/en-us/library/system.management.automation.cmdletcommonmetadataattribute.supportsshouldprocess.aspx)
parameter automatically get `-WhatIf` and `-Confirm` parameters, which modify the functionality of `ShouldProcess`

`-WhatIf` is used for a cmdlet to "go through the motions" of its operation, but
to not actually complete its action. It is used so you can test "what if" the
command were actually run. ShouldProcess automatically returns false.

Note that passing `-WhatIf` prevents the `ShouldProcess` prompt from appearing.

`-Confirm` is used to ensure the confirmation prompt of `ShouldProcess` appears, regardless of the value of `$ConfirmPreference` and 

A cmdlet can call  the [ShouldProcess](https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.shouldprocess.aspx)
method to prompt the user before proceeding. Cmdlets that do so MUST set the `SupportsShouldProcess` property
of the Cmdlet attribute. [ConfirmImpact](https://msdn.microsoft.com/en-us/library/system.management.automation.cmdletcommonmetadataattribute.confirmimpact.aspx)
MAY be default and SHOULD NOT be set to High.

# Cmdlets SHOULD call `ShouldContinue` during unexpected/hidden data destroying operations (e.g. Deleting a GcsBucket with Objects still inside).

A cmdlet can call the [ShouldContinue](https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.shouldcontinue.aspx) method to prompt
the user before proceeding. Cmdlets that call this method must include a `Force` flag. This method ignroes
and ConfirmImpact setting.

`ShouldContinue` should be used sparingly, and only when a cmdlet unexpectidly is going beyond its obvious
contract. There MUST be at least one normal path a cmdlet can take that does not call `ShouldContinue`.

````
# Removing a folder with a child item prompts
# before continuing.
PS C:\> Remove-Item "C:\Users\chrsmith\AppData\Local\Temp\VSD92CC.tmp"

Confirm
The item at C:\Users\chrsmith\AppData\Local\Temp\VSD92CC.tmp has children and the Recurse parameter was not specified. If you continue, all children will be removed with the item. Are you sure you want to continue?
[Y] Yes  [A] Yes to All  [N] No  [L] No to All  [S] Suspend  [?] Help (default is "Y"): n
PS C:\> Remove-Item "C:\Users\chrsmith\AppData\Local\Temp\VSD92CC.tmp\install.log"
PS C:\> Remove-Item "C:\Users\chrsmith\AppData\Local\Temp\VSD92CC.tmp"
PS C:\>
````


# Cmdlets MAY add `-Force` to bypass basic restrictions
# Cmdlets calling `ShouldContinue` MUST add `-Force`

There are classes of restrictions which are not that important, but should
prevent cmdlets from doing harm. For example, when copying a file if a file
already exists at the new location and has the read-only attribute set, the
cmdlet will fail.

However, by specifying the `-Force` parameter, the cmdlet will override these
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

# Cmdlet parameter names SHOULD be as simple as possible

Keep parameter names as simple as possible. For example, if uploading a file,
avoid parameter names like `LocalFile` or `FileToUpload` and instead just
go with `File`.

At times a more descriptive name might be appropriate, such as `InputObjectName`
when coupled with `OutputObjectName`. But in general, avoid adjectives modifying
the parameter name if the purpose of the parameter is clear without it.
