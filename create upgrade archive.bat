del package\* /S /Q
rmdir package /S /Q
mkdir package
mkdir package\Regions
copy bin\Aurora.exe package
copy bin\Aurora.Server.exe package
copy bin\Aurora.32BitLaunch.exe package
copy bin\Aurora.exe.config package
copy bin\Aurora.Server.exe.config package
copy bin\Aurora.32BitLaunch.exe.config package
copy bin\*.bat package
copy bin\*.dll package
copy bin\*.ini package
copy bin\*.xml package
copy bin\*.html package
copy bin\*.example package
xcopy bin\ScriptEngines package\ScriptEngines /E /I
::skip this for now because it's huge!
::xcopy bin\DefaultInventory package\DefaultInventory /E /I
xcopy bin\Physics package\Physics /E /I
xcopy bin\Data package\Data /E /I
xcopy bin\Configuration package\Configuration /E /I
xcopy bin\AuroraServerConfiguration package\AuroraServerConfiguration /E /I
del upgrade.7z /Q
cd package
"C:\Program Files\7-Zip\7z.exe" a -t7z ..\upgrade_%date:~-4,4%-%date:~-7,2%-%date:~-10,2%_%time:~-11,2%-%time:~-8,2%.7z *
pause