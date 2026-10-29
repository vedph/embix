@echo off
echo BUILD Embix packages
del .\Embix.Core\bin\Release\*.*nupkg
del .\Embix.MySql\bin\Release\*.*nupkg
del .\Embix.PgSql\bin\Release\*.*nupkg
del .\Embix.Plugin.Greek\bin\Release\*.*nupkg
del .\Embix.Search\bin\Release\*.*nupkg
del .\Embix.Search.MySql\bin\Release\*.*nupkg
del .\Embix.Search.PgSql\bin\Release\*.*nupkg

cd .\Embix.Core
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.MySql
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.PgSql
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.Plugin.Greek
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.Search
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.Search.MySql
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.Search.PgSql
dotnet pack -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
pause
