@echo off
echo Building ACE Server with Split Arrow Tests...
echo.

echo Step 1: Building the solution...
dotnet build Source/ACE.Server/ACE.Server.csproj --verbosity minimal
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

echo.
echo Step 2: Running Split Arrow Tests...
dotnet test Source/ACE.Server.Tests/ACE.Server.Tests.csproj --filter "FullyQualifiedName~SplitArrowTests" --verbosity normal --no-build
if %ERRORLEVEL% neq 0 (
    echo Split Arrow tests failed!
    exit /b 1
)

echo.
echo ✅ Build and Split Arrow Tests completed successfully!
echo.
pause
