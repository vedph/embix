@echo off
echo PRESS ANY KEY TO INSTALL PACKAGES TO LOCAL NUGET FEED
echo Remember to generate the up-to-date package with dotnet pack.
pause
c:\exe\nuget add .\Embix.Core\bin\Release\Embix.Core.5.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.MySql\bin\Release\Embix.MySql.5.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.PgSql\bin\Release\Embix.PgSql.5.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Plugin.Greek\bin\Release\Embix.Plugin.Greek.5.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Search\bin\Release\Embix.Search.5.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Search.MySql\bin\Release\Embix.Search.MySql.5.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Search.PgSql\bin\Release\Embix.Search.PgSql.5.0.0.nupkg -source C:\Projects\_NuGet
pause
