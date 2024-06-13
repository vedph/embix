@echo off
echo PRESS ANY KEY TO INSTALL PACKAGES TO LOCAL NUGET FEED
echo Remember to generate the up-to-date package with dotnet pack.
pause
c:\exe\nuget add .\Embix.Core\bin\Debug\Embix.Core.3.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.MySql\bin\Debug\Embix.MySql.3.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.PgSql\bin\Debug\Embix.PgSql.3.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Plugin.Greek\bin\Debug\Embix.Plugin.Greek.3.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Search\bin\Debug\Embix.Search.3.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Search.MySql\bin\Debug\Embix.Search.MySql.3.0.0.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Embix.Search.PgSql\bin\Debug\Embix.Search.PgSql.3.0.0.nupkg -source C:\Projects\_NuGet
pause
