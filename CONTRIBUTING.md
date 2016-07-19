# Contributing

Want to contribute? Great! Please read this page so your submission can go
through smoothly.

## Contributor License Agreement

Before we can use your code, you must sign the
[Google Individual Contributor License Agreement](https://cla.developers.google.com/about/google-individual)
(CLA), which you can do online. The CLA is necessary mainly because you own the
copyright to your changes, even after your contribution becomes part of our
codebase, so we need your permission to use and distribute your code. We also
need to be sure of various other things — for instance that you'll tell us if
you know that your code infringes on other people's patents.

Contributions made by corporations are covered by a different agreement than
the one above. If you work for a company that wants to allow you to contribute
your work, then you'll need to sign a
[Software Grant and Corporate Contributor License Agreement](https://cla.developers.google.com/about/google-corporate).

You don't have to sign the CLA until after you've submitted your code for review
and a member has approved it, but you must do it before we can put your code
into the repository. Before you start working on a larger contribution, you
should get in touch with us first through the issue tracker with your idea so
that we can help out and possibly guide you. Coordinating up front makes it much
easier to avoid frustration later on.

## Developer Workflow

If you would like to add a new feature, cmdlet, or change, first
[create a new Issue](https://github.com/GoogleCloudPlatform/google-cloud-powershell/issues/new).
There we will triage the idea and discuss any design or implementation details.

Contributors are expected to do their work in a local fork and submit code for
consideration via a GitHub pull request.

Pull Requests are expected to meet the following requirements:

- Adhere to the coding style guide - We use the
[DotNet Foundation's Coding Style Guide](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md). Fortunately you can run the [codeformatter](https://github.com/dotnet/codeformatter) tool to clean up syntax as needed. (See
[RunCodeFormatter.ps1](https://github.com/GoogleCloudPlatform/google-cloud-powershell/blob/master/Tools/RunCodeFormatter.ps1).)
- Adds an appropriate amount of test coverage - See the `README.md` files in the
`Google.PowerShell.Tests` and `Google.PowerShell.IntegrationTests` folders for
more detail on how to run tests locally.
- And finally, that you have signed the CLA as mentioned above.

When the pull request process deems the change ready, it will be merged directly
into the tree. Congratulations and thank you!
