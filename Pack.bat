@echo off
echo BUILD Embix packages
del .\Embix.Core\bin\Debug\*.*nupkg
del .\Embix.MySql\bin\Debug\*.*nupkg
del .\Embix.PgSql\bin\Debug\*.*nupkg
del .\Embix.Plugin.Greek\bin\Debug\*.*nupkg
del .\Embix.Search\bin\Debug\*.*nupkg
del .\Embix.Search.MySql\bin\Debug\*.*nupkg
del .\Embix.Search.PgSql\bin\Debug\*.*nupkg

cd .\Embix.Core
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.MySql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.PgSql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.Plugin.Greek
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix\Search
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix\Search.MySql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix\Search.PgSql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
pause
