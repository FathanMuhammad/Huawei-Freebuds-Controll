@echo off
echo ========================================================
echo   Building and Publishing FreeBudsController (.NET 8)
echo ========================================================
echo.

dotnet publish FreeBudsController\FreeBudsController.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   BUILD SUKSES! File executable ada di folder: publish\
    echo ========================================================
) else (
    echo.
    echo [!] Build GAGAL. Cek pesan error di atas.
)
pause
