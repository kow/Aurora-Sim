:START
@ECHO OFF
echo starting Aurora.Server.exe
echo --------------------------
@ECHO ON
Aurora.Server.exe
@ECHO OFF
if %ERRORLEVEL% == 0 GOTO EXIT
echo Crash detected @ %Time% %Date%, return code = %ERRORLEVEL%
echo Aurora.Server crash detected @ %Time% %Date%, return code = %ERRORLEVEL% >> crash.log
echo.
GOTO START

:EXIT
echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
echo !!! received a return code of 0, Aurora.Server has been safely shut down !!!
echo !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
pause