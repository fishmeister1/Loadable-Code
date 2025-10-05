@echo off
echo Codeful Diagnostic Script
echo ========================
echo.

echo Checking installation directory...
if exist "%ProgramFiles%\Loadable\Code.exe" (
    echo ✓ Code.exe found in installation directory
    set "INSTALL_DIR=%ProgramFiles%\Loadable"
) else (
    echo ✗ Code.exe NOT found in %ProgramFiles%\Loadable\
    goto :error
)

echo.
echo Installation directory contents:
dir "%INSTALL_DIR%"
echo.

echo Checking .env file...
if exist "%INSTALL_DIR%\.env" (
    echo ✓ .env file found
) else (
    echo ⚠ .env file NOT found - this might cause startup issues
)

echo.
echo Attempting to run the application...
echo Working directory: %INSTALL_DIR%
cd /d "%INSTALL_DIR%"
echo Running: Code.exe
echo.
echo Testing formatting functionality...
echo - Bold text: **This should be bold**
echo - Code blocks: ```javascript
echo   console.log("test");
echo   ```
echo.
echo If formatting doesn't work in the installed version,
echo this is likely due to XAML resource loading issues in compiled apps.
echo.

"%INSTALL_DIR%\Code.exe"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ✗ Application failed to start with error code: %ERRORLEVEL%
    echo.
    echo Common solutions:
    echo 1. Make sure .NET 10.0 runtime is available or application is self-contained
    echo 2. Check if .env file with API key is present
    echo 3. Run as Administrator if there are permission issues
    echo 4. Check Windows Event Viewer for detailed error information
) else (
    echo ✓ Application started successfully
)

goto :end

:error
echo.
echo Installation appears to be incomplete or corrupted.
echo Please reinstall the application.

:end
echo.
pause