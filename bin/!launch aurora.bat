:START
@ECHO OFF
echo starting Aurora.32BitLaunch.exe
echo -------------------------------
@ECHO ON
Aurora.32BitLaunch.exe
@ECHO OFF
if %ERRORLEVEL% == 0 GOTO EXIT
echo Crash detected @ %Time% %Date%, return code = %ERRORLEVEL%
echo Aurora.Sim crash detected @ %Time% %Date%, return code = %ERRORLEVEL% >> crash.log
echo.
GOTO START

:EXIT
echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
echo !!! received a return code of 0, Aurora.Sim has been safely shut down !!!
echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
pause