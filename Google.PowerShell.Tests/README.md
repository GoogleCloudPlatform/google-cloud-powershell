# Unit Tests

Download and install the NUnit test adapter. See:
http://www.nuget.org/packages/NUnitTestAdapter/.

You can install this from Nuget via:

    Install-Package NUnitTestAdapter

## Scope

Unit tests have generally been used for things that are non-cmdlet related.
While it would be faster to run a battery of unit tests against the cmdlets,
unfortunately we have yet to come up with a good design that doesn't require an
inordinate amount of boiler plate code to mock/stub/fake the underlying Google
APIs. (Pull Requests welcome, of course.)