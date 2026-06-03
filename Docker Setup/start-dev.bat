@echo off
REM =============================================================================
REM  Start GenService — DEVELOPMENT Environment
REM  Hot reload ON for both API and React frontend
REM  Run this script as Administrator for best results (releases stuck ports)
REM =============================================================================

echo.
echo  ========================================================
echo   GenService — Starting DEVELOPMENT Environment
echo  ========================================================
echo.

REM Release stuck Windows NAT port bindings (WSL2 Docker bug workaround)
echo  Releasing stuck ports...
net stop winnat >nul 2>&1
net start winnat >nul 2>&1
echo  Ports released.
echo.

echo  Services starting:
echo    SQL Server     : localhost:1433
echo    Redis          : localhost:6379
echo    Azurite        : localhost:10000
echo    RabbitMQ       : localhost:5672
echo    Seq (Logs)     : http://localhost:5341
echo    API            : http://localhost:8080
echo    Frontend       : http://localhost:5173  ^<-- open this
echo.

docker compose -f docker-compose.yml up

pause
