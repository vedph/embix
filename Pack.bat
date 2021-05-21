@echo off
echo BUILD Embix packages
del .\Embix.Core\bin\Debug\*.*nupkg
del .\Embix.MySql\bin\Debug\*.*nupkg

cd .\Embix.Core
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Embix.MySql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
pause