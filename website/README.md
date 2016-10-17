# PowerShell Cmdlet Reference Website

This directory hosts the Cloud Tools for PowerShell cmdlet reference website,
seen at googlecloudplatform.github.io/google-cloud-powershell/.

## Building

The website just requires static content. However, the site renders the cmdlet
documentation stored in a JSON file. (`.\data\cmdletsFull.json`).

We need to regenerate that data file whenever we update our cmdlets, cmdlet
documentation, examples, etc. It is generated as a result of running the
`Tools\GenerateWebsiteData.ps1` script file. Simply rebuild the cmdlets locally
and then rerun the script. The JSON file will be edited in-place.

## Running

The easiest way to run the website locally for testing is to launch it using
the Google App Engine dev appserver. The `app.yaml` file contains the necessary
URL handling schenanigans so that the website will work the same as when it is
hosed on GitHub Pages.

On Windows:

````
& gcloud components install app-engine-python
$dev_appserver = "C:\Program Files (x86)\Google\Cloud SDK\google-cloud-sdk\bin\dev_appserver.py"
& python $dev_appserver .\website\app.yaml
````

On macOS:

````
$ python ~/google-cloud-sdk/bin/dev_appserver.py ./website/app.yaml
````

Once the app server is running, visit:
http://localhost:8080/google-cloud-powershell/

## Publishing

Any updates to the `website` folder will automatically be published to GitHub
Pages because of `.gitmodule` schenanigans.
