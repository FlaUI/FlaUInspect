//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Test");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var slnFile = "./src/FlaUInspect.sln";
var artifactDir = new DirectoryPath("artifacts");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .WithCriteria(c => HasArgument("rebuild"))
    .Does(() =>
{
    CleanDirectory($"./src/FlaUInspect/bin/{configuration}");
});

Task("Build")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetBuild(slnFile, new DotNetBuildSettings
    {
        Configuration = configuration,
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetTest(slnFile, new DotNetTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
    });
});

Task("Package")
    .IsDependentOn("Test")
    .Does(() =>
{
    var version = "2.0.0";
    var chocolateyPackSettings = new ChocolateyPackSettings
    {
        Id = "FlaUInspect" + (configuration != "Debug" ? $".{configuration.ToUpper()}" : ""),
        Title = "FlaUI Inspector" + (configuration != "Debug" ? $" {configuration.ToUpper()}" : ""),
        Version = version,
        Authors = new[] { "Roemer", "K. Usenko {kDg}" },
        Owners = new[] { "Roemer", "K. Usenko {kDg}" },
        Summary = "Inspect and analyze UI Automation elements in Windows applications.",
        Description = "FlaUInspect is a UI Automation inspection tool for Windows applications, useful for accessibility and automation testing.",
        ProjectUrl = new Uri("https://github.com/FlaUI/FlaUInspect"),
        PackageSourceUrl = new Uri("https://github.com/FlaUI/FlaUInspect"),
        ProjectSourceUrl = new Uri("https://github.com/FlaUI/FlaUInspect"),
        DocsUrl = new Uri("https://github.com/FlaUI/FlaUInspect/wiki"),
        BugTrackerUrl = new Uri("https://github.com/FlaUI/FlaUInspect/issues"),
        Tags = new[] { "ui", "automation", "uia", "uia2", "uia3", "system.windows.automation", "inspect", "uiautomation", "accessibility", "windows", "testing" },
        Copyright = $"Copyright 2016-{DateTime.Now.Year}",
        LicenseUrl = new Uri("https://github.com/FlaUI/FlaUInspect/blob/main/LICENSE"),
        RequireLicenseAcceptance = false,
        IconUrl = new Uri("https://github.com/FlaUI/FlaUInspect/blob/main/FlaUInspect.png"),
        ReleaseNotes = new[] { "https://github.com/FlaUI/FlaUInspect/blob/main/CHANGELOG.md" },
        Files = new[] {
            new ChocolateyNuSpecContent {
                Source = @$"src\FlaUInspect\bin\{configuration}\net10.0-windows10.0.19041.0\**", Target = "tools"
            },
            new ChocolateyNuSpecContent {
                Source = "LICENSE", Target = @"tools\LICENSE"
            },
            new ChocolateyNuSpecContent {
                Source = "CHANGELOG.md", Target = @"tools\CHANGELOG.md"
            },
            new ChocolateyNuSpecContent {
                Source = "VERIFICATION", Target = @"tools\VERIFICATION"
            }
        },
        OutputDirectory = artifactDir
    };
    ChocolateyPack(chocolateyPackSettings);
});

Task("Push-Package")
    .Does(() =>
{
    var apiKey = System.IO.File.ReadAllText(".chocoapikey");

    var files = GetFiles($"{artifactDir}/*.nupkg");
    foreach (var package in files) {
        Information($"Pushing {package}");
        ChocolateyPush(package, new ChocolateyPushSettings {
            ApiKey = apiKey
        });
    }
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
