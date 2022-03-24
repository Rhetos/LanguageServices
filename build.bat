SETLOCAL

@SET Config=%1%
@IF [%1] == [] SET Config=Release

CALL Tools\FindVisualStudio.bat || GOTO Error0
CALL Tools\CheckLicenceInSourceFiles.bat || GOTO Error0

REM NuGet Automatic Package Restore requires "NuGet.exe restore" to be executed before the command-line build.
WHERE /Q NuGet.exe || ECHO ERROR: Please download the NuGet.exe command line tool. && GOTO Error0
NuGet.exe restore "Rhetos.LanguageServices.sln" -NonInteractive || GOTO Error0
MSBuild.exe "Rhetos.LanguageServices.sln" /target:rebuild /p:Configuration=%Config% /verbosity:minimal /fileLogger || GOTO Error0

@REM ================================================

@ECHO.
@ECHO %~nx0 SUCCESSFULLY COMPLETED.
@EXIT /B 0

:Error0
@ECHO.
@ECHO %~nx0 FAILED.
@EXIT /B 1
