# Cloud Tools for PowerShell

[![Build status](https://ci.appveyor.com/api/projects/status/r11ovv4348852ktt?svg=true)](https://ci.appveyor.com/project/GoogleCloudPowerShell/google-cloud-powershell)

This repository contains PowerShell cmdlets for interacting with the Google
Cloud Platform. You can use Cloud Tools for PowerShell to manage your existing
cloud resources or create new ones.

# Installation

You can install Cloud Tools for PowerShell from the PowerShell gallery by running
`Install-Module GoogleCloud`. When you first use the module, you will be prompted
to install the Google Cloud SDK. Select yes and the module will download
and install the SDK.

Alternatively, you can install the [Google Cloud SDK](https://cloud.google.com/sdk/docs/quickstart-windows)
directly by downloading and installing the installer (the instruction is in the link).
Cloud Tools for PowerShell will be included in the SDK by default.

Cloud Tools for PowerShell uses your Cloud SDK credentials. So if you have not
already, run `gcloud auth login` or `gcloud init` to login.

# Documentation

You can learn more about Cloud Tools for PowerShell, with Quick Start and How-To
guides, at https://cloud.google.com/powershell/.

You can see a full cmdlet reference at:
https://googlecloudplatform.github.io/google-cloud-powershell/

# Support

To get help on using these cmdlets, please
[log an issue](https://github.com/GoogleCloudPlatform/google-cloud-powershell/issues/new)
with this project. While we will eventually be able to offer support on
StackOverflow, but for now your best bet is to contact the dev team directly.

Patches are encouraged, and may be submitted by forking this project and
submitting a Pull Request. See [CONTRIBUTING.md](CONTRIBUTING.md) for more
information.

# License

Apache 2.0. See [LICENSE](LICENSE) for more information.
