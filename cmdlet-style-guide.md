# PowerShell cmdlet Style Guide #

This guide outlines the best practices when writing PowerShell cmdlets. It is a
work in-progress, and if you have suggestions or altering opinions please feel
free to submit an issue for discussion.

[RFC2119](http://www.ietf.org/rfc/rfc2119.txt)

# Cmdlets SHOULD call `ShouldProcess` during unexpected situations

A cmdlet can call the [ShouldProcess(https://msdn.microsoft.com/en-us/library/system.management.automation.cmdlet.shouldprocess(v=vs.85).aspx)
method to prompt the user before proceeding.

This allows the user to adjudicate any unexpected situations. For example, when
the `Remove-Item` cmdlet is used and it would delete a folder but the
`-Recruse` flag is not set, `ShouldProcess` is called to prompt the user to
confirm this is what they want.

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

# Cmdlets which destroy data MUST use `SupportsShouldProcess`

In order to avoid accidentally destroying data (deleting a file, shutting down a
virtual machine, etc.) users should be prompt to confirm the operation.

Cmdlets decorated with `Cmdlet`'s [SupportsShouldProcess](https://msdn.microsoft.com/en-us/library/system.management.automation.cmdletcommonmetadataattribute.supportsshouldprocess(v=vs.85).aspx)
parameter automatically get `-WhatIf` and `-Confirm` parameters.

`-WhatIf` is used for a cmdlet to "go through the motions" of its operation, but
to not actually complete its action. It is used so you can test "what if" the
command were actually run.

Note that passing `-WhatIf` does not prevent the `ShouldProcess` prompt from
appearing. In fact, you may still get multiple `ShouldProcess` prompts (just
like you would if `-WhatIf` were not added.)

`-Confirm` is used to bypass the confirmation prompt by `ShouldProcess` and to
proceed with the operation silently.

# Cmdlets MAY add `-Force` to bypass basic restrictions

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
