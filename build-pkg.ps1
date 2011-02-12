$currentPath =  $myinvocation.mycommand.path | split-path -parent
$currentPath
push-location
set-location $currentPath
if(-not (Test-Path ".\PackageRelease")) {
	New-Item "PackageRelease" -type "Directory"
}
.\Tools\NuGet.exe pack "$currentPath\FluentMongo.nuspec" -o ".\PackageRelease\"
pop-location
