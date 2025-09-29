@echo off
set /A errors=0
echo Starting the server!!
timeout /t 2
:START
set current_branch=
for /F "delims=" %%n in ('git branch --show-current') do set "current_branch=%%n"
if "%current_branch%"=="" echo Not a git branch! && goto :ERROR
git stash
git checkout master
git pull
:BUILD
dotnet clean .\HandballBackend\HandballBackend.csproj -c Release
if %ERRORLEVEL% neq 0 goto :ERROR
dotnet publish .\HandballBackend\HandballBackend.csproj -c Release ^
  --runtime win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  --framework net9.0 ^
  --output G:\Programming\c#\HandballBackend\build
if %ERRORLEVEL% neq 0 goto :ERROR
git checkout %current_branch%
git stash pop
goto :SUCCESS


:ERROR
SET /A errors=%errors%+1
if %errors%==1 echo There was an error building/downloading the branch! Waiting 10 seconds and trying again && timeout 10
if %errors%==2 echo There was an error building/downloading the branch! Waiting 60 seconds and trying again && timeout 60
if %errors%==3 echo There was an error building/downloading the branch! Waiting 5 minutes and trying again && timeout 300
if %errors% gtr 3 echo The file has failed to start %errors% times! Exiting && pause && goto :EOF
goto :START

:SUCCESS
SET /A errors=0
cls
mkdir .\build\resources
xcopy .\HandballBackend\resources .\build\resources
cd .\build
.\HandballBackend.exe -l false -u -b -s
SET /A EXIT_CODE=%ERRORLEVEL%
cd ..
if %EXIT_CODE%==0 goto :EOF
if %EXIT_CODE%==1 echo A server restart was requested! && timeout 1 && goto :SUCCESS
if %EXIT_CODE%==2 echo A server rebuilds was requested! && timeout 1 && goto :BUILD
if %EXIT_CODE%==3 echo A server git update was requested! && timeout 1 && goto :START

:EOF