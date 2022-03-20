# Mirror

A console app to download a local copy of a website.

The user provides the URL as the only parameter of the app.
The program downloads the URL to a local copy, and tries to analyze it for further links to follow on the same domain.
Rinse and repeat till all links are exhausted.

## Where Are The Files?

The files are stored under `local-copies` folder created where the app runs.

## Reports

The app also creates a report JSON file that holds some metadata, errors that happened while scanning, and a list of all URLs that was scanned.
The report is stored in the same folder as the executable, and is called `scan-report-{yyyy}-{MM}-{dd}-{HH}-{mm}-{ss}.json`.

## How To Run

You either run it through the dotnet CLI, by invoking it as such:

```
dotnet run <url>
```

Example:

```
dotnet run https://microsoft.com
```

Or, of course, compile your own executable and just run it with the URL as the parameter.