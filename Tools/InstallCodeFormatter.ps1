# Update from time-to-time by looking at:
# https://github.com/dotnet/codeformatter/releases
$release = "v1.0.0-alpha5"
$latestBinaryDrop = "https://github.com/dotnet/codeformatter/releases/download/$release/CodeFormatter.zip"

$installPath = "$env:APPDATA\CodeFormatter\"

If (Test-Path $installPath) {
    Remove-Item -Recurse -Force $installPath
}
New-Item -Type directory $installPath
Invoke-WebRequest $latestBinaryDrop -OutFile $installPath\codeformatter.zip

# Unzip the release
New-Item -Type directory $installPath\$release
Add-Type -Assembly "System.IO.Compression.FileSystem"
[IO.Compression.ZipFile]::ExtractToDirectory(
    "$installPath\codeformatter.zip",
    "$installPath\$release\")

# Add it to the user's PATH. Changes to $env:PATH will be lost when the
# current session quits, hence the call to the .NET API.
[System.Environment]::SetEnvironmentVariable(
    "PATH",
    "$installPath\$release\CodeFormatter\;$env:PATH")
