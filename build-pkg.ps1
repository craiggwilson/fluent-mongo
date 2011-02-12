$currentPath =  $myinvocation.mycommand.path | split-path -parent
$currentPath
push-location
set-location $currentPath
.\Tools\NuGet.exe pack "$currentPath\FluentMongo.nuspec" -o ".\PackageRelease\"
pop-location
