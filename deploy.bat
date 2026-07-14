@echo off
echo 🚀 PrintingBooks Portal - Quick Deploy
echo =====================================
echo Target: drbaheegbook.runasp.net
echo.

REM Build and publish
echo 📦 Building application...
dotnet publish -c Release

REM Deploy using MSDeploy
echo 🌐 Deploying to RunASP.NET...
dotnet publish -c Release ^
  /p:PublishMethod=MSDeploy ^
  /p:MSDeployServiceURL=site79455.siteasp.net:8172 ^
  /p:DeployDefaultTarget=WebPublish ^
  /p:MSDeployPublishMethod=WMSVC ^
  /p:CreatePackageOnPublish=false ^
  /p:MSDeploySite=site79455 ^
  /p:UserName=site79455 ^
  /p:Password=Q#r8_q3D6Nj%% ^
  /p:AllowUntrustedCertificate=true ^
  /p:SkipExtraFilesOnServer=true

if %errorlevel% equ 0 (
    echo.
    echo ✅ Deployment successful!
    echo 🌐 URL: http://drbaheegbook.runasp.net/
    echo.
) else (
    echo.
    echo ❌ Deployment failed!
    echo.
)

pause