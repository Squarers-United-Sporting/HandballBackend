@echo off
set /A errors=0
set /A debug=1
echo Starting the server!!
timeout /t 2

:START
set current_branch=
for /F "delims=" %%n in ('git branch --show-current') do set "current_branch=%%n"
if "%current_branch%"=="" echo Not a git branch! && goto :ERROR
if "%debug%"=="1" goto DEBUG
git stash
git checkout master
git pull
:DEBUG
set GIT_REVISION=
for /F "delims=" %%n in ('git rev-parse master') do set "GIT_REVISION=%%n"
goto SUCCESS


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
docker compose up --build --exit-code-from handball-backend
SET /A EXIT_CODE=%ERRORLEVEL%
if %EXIT_CODE%==0 goto :EOF
if %EXIT_CODE%==1 echo A server restart was requested! && timeout 1 && goto :SUCCESS
if %EXIT_CODE%==2 echo A server rebuilds was requested! && timeout 1 && goto :BUILD
if %EXIT_CODE%==3 echo A server git update was requested! && timeout 1 && goto :START

:EOF