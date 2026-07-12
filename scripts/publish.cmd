@echo off
rem Publish a self-contained Squirrel binary for Windows.
cd /d "%~dp0.."
dotnet publish src\Squirrel.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish\win-x64
echo Done: publish\win-x64\Squirrel.exe
