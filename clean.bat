REM Delete all "bin", "obj" and "TestResults" subfolders:
@FOR /F "delims=" %%i IN ('dir bin /s/b/ad') DO DEL /F/S/Q "%%i" && RD /S/Q "%%i"
@FOR /F "delims=" %%i IN ('dir obj /s/b/ad') DO DEL /F/S/Q "%%i" && RD /S/Q "%%i"
@FOR /F "delims=" %%i IN ('dir TestResult? /s/b/ad') DO DEL /F/S/Q "%%i" && RD /S/Q "%%i"

REM Delete build logs:
@DEL *.log

REM Delete build installation result:
@RD /S/Q dist
@MD dist

REM Delete external dependencies cache (downloaded NuGet packages):
@RD /S/Q packages

@REM Additional comment line so that Clean.bat does not return error code from last command if there are no folders to delete.
