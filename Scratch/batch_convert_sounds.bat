@echo off
setlocal enabledelayedexpansion

for %%f in (.\Input\*.wav) do (
    echo %%~nf
    .\xWMAEncode.exe .\Input\%%~nf.wav .\Output\%%~nf.xwm
)