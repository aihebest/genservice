@echo off
REM =============================================================================
REM  Start GenService — TEST / UAT Environment
REM  Production builds. Separate isolated database.
REM =============================================================================

echo.
echo  ========================================================
echo   GenService — Starting TEST Environment
echo  ========================================================
echo.
echo  Services starting:
echo    SQL Server     : localhost:1434  (TEST — separate from Dev)
echo    Redis          : localhost:6380
echo    RabbitMQ Mgmt  : http://localhost:15673
echo    Seq (Logs)     : http://localhost:5342
echo    API            : http://localhost:8081
echo    Frontend       : http://localhost:5174  ^<-- open this
echo.
echo  Note: All three environments can run simultaneously.
echo        Dev uses port 5173, Test uses 5174, Demo uses 80.
echo.

docker compose -f docker-compose.test.yml up --build

pause
