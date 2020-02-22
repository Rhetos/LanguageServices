@REM HINT: SET SECOND ARGUMENT TO /NOPAUSE WHEN AUTOMATING THE BUILD.

CALL clean.bat || GOTO Error0
CALL build.bat || GOTO Error0
CALL tools\FindVisualStudio.bat || GOTO Error0 

@REM Find all test projects, and execute tests for each one:
FOR /F "tokens=*" %%a IN ('DIR "*.Test.dll" /s/b ^| FINDSTR /I ^"\\bin\\%Config%\\^"') DO vstest.console.exe "%%a" || GOTO Error0

@REM ================================================

@ECHO.
@ECHO %~nx0 SUCCESSFULLY COMPLETED.
@EXIT /B 0

:Error0
@ECHO.
@ECHO %~nx0 FAILED.
@EXIT /B 1
