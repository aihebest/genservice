@echo off
REM =============================================================================
REM  Stop ALL GenService environments (Dev + Test + Demo)
REM =============================================================================

echo.
echo  Stopping all GenService environments...
echo.

docker compose -f docker-compose.yml      down 2>nul
docker compose -f docker-compose.test.yml down 2>nul
docker compose -f docker-compose.demo.yml down 2>nul

echo.
echo  All environments stopped.
echo  Data volumes are preserved. Run start-*.bat to restart.
echo.
echo  To also DELETE all data volumes (full reset):
echo    docker compose -f docker-compose.yml      down -v
echo    docker compose -f docker-compose.test.yml down -v
echo    docker compose -f docker-compose.demo.yml down -v
echo.

pause
