@echo off
set "PROGRAM=%~dp0src\TurkiyeTakvimi.Arayuz\bin\Debug\TurkiyeTakvimi.exe"
if not exist "%PROGRAM%" (
    echo Program bulunamadi. Claude'a programi derle deyin.
    pause
    exit /b 1
)
start "" "%PROGRAM%"
