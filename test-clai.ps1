# Test script for CLAI
Write-Host "Testing CLAI with real terminal commands" -ForegroundColor Cyan
Write-Host ""

# Navigate to the built executable
cd src\Clai\bin\Debug\net9.0

# Test commands to send to CLAI
$testCommands = @(
    "echo Hello from CLAI",
    "dir",
    "exit"
)

Write-Host "Starting CLAI..." -ForegroundColor Green
Write-Host "Will execute the following commands:" -ForegroundColor Yellow
$testCommands | ForEach-Object { Write-Host "  - $_" }
Write-Host ""

# Run CLAI with test commands
# Note: This will run interactively, you'll need to type the commands manually
.\Clai.exe