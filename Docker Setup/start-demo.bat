@echo off
REM =============================================================================
REM  Start GenService — MANAGEMENT DEMO (Production-like)
REM  Production builds, Nginx reverse proxy, demo data pre-seeded.
REM =============================================================================

echo.
echo  ========================================================
echo   GenService — Starting MANAGEMENT DEMO
echo  ========================================================
echo.
echo  First-time startup takes 3-5 minutes (building images + seeding data).
echo  Subsequent starts take about 30 seconds.
echo.
echo  When ready, open:  http://localhost
echo.
echo  DEMO LOGIN CREDENTIALS:
echo  +--------------------------+-------------------+--------------------+
echo  ^|  Role                    ^|  Email            ^|  Password          ^|
echo  +--------------------------+-------------------+--------------------+
echo  ^|  Department Manager      ^|  manager@demo.local  ^|  DemoManager2026! ^|
echo  ^|  Supervisor              ^|  supervisor@demo.local^| DemoSuper2026!   ^|
echo  ^|  Technician              ^|  technician@demo.local^| DemoTech2026!    ^|
echo  ^|  Driver                  ^|  driver@demo.local   ^|  DemoDriver2026!  ^|
echo  +--------------------------+-------------------+--------------------+
echo.
echo  Press Ctrl+C to stop the demo environment.
echo.

docker compose -f docker-compose.demo.yml up --build

pause
