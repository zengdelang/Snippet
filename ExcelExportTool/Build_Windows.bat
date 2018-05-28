@echo off

cd %~dp0

pyinstaller -F .\Source\ConvertExcelToJson.py --distpath=%~dp0Output --specpath=%~dp0Output\Temp --workpath=%~dp0Output\Temp

rd /q /s %~dp0Output\Temp

pause