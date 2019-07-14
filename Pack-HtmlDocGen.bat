
nuget pack ./HtmlDocGen.nuspec -Build -Symbols -OutputDirectory ".\(Stage)\Packages" -InstallPackageToOutputPath

IF NOT EXIST "..\(NuGetRepo)" GOTO NOCOPYTOGLOBALREPO
xcopy ".\(Stage)\Packages\HtmlDocGen.*" "..\(NuGetRepo)\" /d /r /y /s
:NOCOPYTOGLOBALREPO

PAUSE