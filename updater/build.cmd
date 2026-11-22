@echo off
setlocal
cd /d "%~dp0"
if not exist "..\dist\updater" mkdir "..\dist\updater"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /utf8output /out:"..\dist\updater\HP2-UpdateAndPlay.exe" /resource:bundled.json,HP2.bundled.json /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.Web.Extensions.dll App.cs Catalog.cs GitHub.cs Transaction.cs Update.cs
exit /b %errorlevel%
