@echo off
echo PUSH PACKAGES TO NUGET
prompt
set nu=C:\Exe\nuget.exe
set src=-Source https://api.nuget.org/v3/index.json
%nu% push .\Embix.Core\bin\Debug\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.MySql\bin\Debug\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.PgSql\bin\Debug\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Plugin.Greek\bin\Debug\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Search\bin\Debug\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Search.MySql\bin\Debug\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Search.PgSql\bin\Debug\*.nupkg %src% -SkipDuplicate
echo COMPLETED
echo on
