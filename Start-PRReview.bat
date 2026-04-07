@echo off
title PR Review Launcher

echo Starting PR Review API (https://localhost:7015)...
start "PR Review API" cmd /k "cd /d "%~dp0PRReview.Api" && dotnet run --launch-profile https"

echo Waiting 5 seconds for API to initialise...
timeout /t 5 /nobreak >nul

echo Starting PR Review UI (http://localhost:4600)...
start "PR Review UI" cmd /k "cd /d "%~dp0PRReview.UI" && npm start"

echo.
echo Both services are starting:
echo   API   -^> https://localhost:7015/swagger
echo   UI    -^> http://localhost:4600
echo.
echo Close the two terminal windows to stop the services.
