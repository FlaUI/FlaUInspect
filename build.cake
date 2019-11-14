///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var slnFile = @"src\FlaUInspect.sln";
var artifactDir = new DirectoryPath("artifacts");

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore(slnFile);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    var buildLogFile = artifactDir.CombineWithFilePath("BuildLog.txt");
    var buildSettings = new MSBuildSettings {
        Verbosity = Verbosity.Minimal,
        Configuration = configuration,
        PlatformTarget = PlatformTarget.MSIL,
    }.AddFileLogger(new MSBuildFileLogger {
        LogFile = buildLogFile.ToString(),
        MSBuildFileLoggerOutput = MSBuildFileLoggerOutput.All
    });
    MSBuild(slnFile, buildSettings);
});

Task("Package")
    .IsDependentOn("Build")
    .Does(() =>
{
    var version = "1.3.0";
    var tempDir = artifactDir.Combine("build-temp");
    CleanDirectory(tempDir);

    // Zip
    var ignoredExts = new string[] { ".pdb", ".xml", ".pdb" };
    var ignoredFiles = new string[] { ".vshost.", "RANDOM_SEED" };
    var files = GetFiles("./src/FlaUInspect/bin/**/*.*")
        .Where(f => !ignoredExts.Contains(f.GetExtension().ToLower()))
        .Where(f => !ignoredFiles.Any(x => f.GetFilename().FullPath.Contains(x)));
    CopyFiles(files, tempDir, true);
    CopyFiles("LICENSE.txt", tempDir);
    CopyFiles("CHANGELOG.md", tempDir);
    CopyFiles("VERIFICATION.txt", tempDir);
    files = GetFiles($"{tempDir}/**/*./");
    Zip(tempDir, artifactDir.CombineWithFilePath($"FlaUInspect_{version}.zip"), files);

    // Chocolatey
    var chocolateyPackSettings   = new ChocolateyPackSettings {
        Id                      = "flauinspect",
        Title                   = "FlaUInspect",
        Version                 = version,
        Authors                 = new[] { "Roemer" },
        Owners                  = new[] { "Roemer" },
        Summary                 = "Tool to inspect Windows applications how UIA sees them.",
        Description             = "This application allows to inspect Windows applications with Microsoft UIA (UI Automation) and shows how UIA sees the application.",
        ProjectUrl              = new Uri("https://github.com/FlaUI/FlaUInspect"),
        PackageSourceUrl        = new Uri("https://github.com/FlaUI/FlaUInspect"),
        ProjectSourceUrl        = new Uri("https://github.com/FlaUI/FlaUInspect"),
        DocsUrl                 = new Uri("https://github.com/FlaUI/FlaUInspect/wiki"),
        BugTrackerUrl           = new Uri("https://github.com/FlaUI/FlaUInspect/issues"),
        Tags                    = new [] { "UI", "Automation", "UIA2", "UIA3", "UIA", "System.Windows.Automation", "Inspect" },
        Copyright               = $"Copyright 2016-{DateTime.Now.Year}",
        LicenseUrl              = new Uri("https://github.com/SomeUser/TestChocolatey/blob/master/LICENSE.md"),
        RequireLicenseAcceptance= false,
        IconUrl                 = new Uri("http://cdn.rawgit.com/SomeUser/TestChocolatey/master/icons/testchocolatey.png"),
        ReleaseNotes            = new [] {"https://github.com/FlaUI/FlaUInspect/blob/master/CHANGELOG.md"},
        Files                   = new [] {
                                    new ChocolateyNuSpecContent {
                                        Source = @"src\FlaUInspect\bin\**\*.*", Target = "tools", Exclude = @"**\*.pdb;**\*.xml;**\*.vshost.*;**\*RANDOM_SEED*"
                                    },
                                    new ChocolateyNuSpecContent {
                                        Source = "LICENSE.txt", Target = "tools"
                                    },
                                    new ChocolateyNuSpecContent {
                                        Source = "CHANGELOG.md", Target = "tools"
                                    },
                                    new ChocolateyNuSpecContent {
                                        Source = "VERIFICATION.txt", Target = "tools"
                                    }
                                },
        OutputDirectory         = artifactDir
    };
    ChocolateyPack(chocolateyPackSettings);
});

Task("Default")
.Does(() => {
    Information("Hello Cake!");
});

RunTarget(target);
