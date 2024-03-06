$VERSION="$VERSION"
if ($VERSION -eq "") { $VERSION="1.0" }
echo $(
rd nil.Dns\bin -Force -Recurse -erroraction 'silentlycontinue'
rd nil.Dns\obj -Force -Recurse -erroraction 'silentlycontinue'
mkdir nuget -erroraction 'silentlycontinue'
) > $null
$REVISION=$(git rev-list --count origin/master)
echo "VERSION: $VERSION.$REVISION"
[System.IO.File]::WriteAllText("$(get-location)\\NiL.Dns\\Properties\\InternalInfo.cs","internal static class InternalInfo
{
    internal const string Version = ""$VERSION.$($REVISION)"";
    internal const string Year = ""$(get-date -Format yyyy)"";
}")
cd NiL.Dns
dotnet build -c Release -property:VersionPrefix=$VERSION.$($REVISION) -property:SignAssembly=true
dotnet pack -c Release -property:VersionPrefix=$VERSION.$($REVISION) -property:SignAssembly=true
mv -Force bin/release/NiL.Dns.$VERSION.$($REVISION).nupkg ../nuget/NiL.Dns.$VERSION.$($REVISION).nupkg
cd ..