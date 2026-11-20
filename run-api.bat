@echo off
chcp 65001 >nul
echo [1/2] Khoi dong BilingualVocabApi...
echo API se chay tai http://localhost:5046
echo Nhan Ctrl+C de dung.
echo.
"C:\Program Files\dotnet\dotnet.exe" run --project BilingualVocabApi/BilingualVocabApi.csproj
pause

