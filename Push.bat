@echo off
echo PUSH PACKAGES TO NUGET
prompt
set nu=C:\Exe\nuget.exe
set src=-Source https://api.nuget.org/v3/index.json
%nu% push .\Embix.Core\bin\Release\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.MySql\bin\Release\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.PgSql\bin\Release\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Plugin.Greek\bin\Release\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Search\bin\Release\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Search.MySql\bin\Release\*.nupkg %src% -SkipDuplicate
%nu% push .\Embix.Search.PgSql\bin\Release\*.nupkg %src% -SkipDuplicate
echo COMPLETED
echo on
