Param(
	[switch]$NonInteractive
)

# Convenience script to initialize user-secrets for the web project
# Usage:
#   ./scripts/init-dev-env.ps1           # interactive prompts
#   ./scripts/init-dev-env.ps1 -NonInteractive

$projFolder = "e.l.f. Beauty"
if (Test-Path $projFolder) { Set-Location $projFolder }

Write-Host "Initializing user-secrets for project: $(Get-Location)"

dotnet user-secrets init | Out-Null

if ($NonInteractive) {
	Write-Host "Applying non-interactive placeholder secrets..."
	dotnet user-secrets set "Jwt:Key" "${([Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 } | ForEach-Object { [byte]$_ })))}"
	dotnet user-secrets set "Jwt:Issuer" "brewery-api"
	dotnet user-secrets set "Jwt:Audience" "brewery-api"
	dotnet user-secrets set "Auth:Username" "admin"
	dotnet user-secrets set "Auth:Password" "password"
	Write-Host "Placeholder secrets written to user-secrets (replace with secure values before use)."
}
else {
	$answer = Read-Host "Do you want to write placeholder secrets now? (y/N)"
	if ($answer -match '^[yY]') {
		dotnet user-secrets set "Jwt:Key" "${([Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 } | ForEach-Object { [byte]$_ })))}"
		dotnet user-secrets set "Jwt:Issuer" "brewery-api"
		dotnet user-secrets set "Jwt:Audience" "brewery-api"
		dotnet user-secrets set "Auth:Username" "admin"
		dotnet user-secrets set "Auth:Password" "password"
		Write-Host "Placeholder secrets written to user-secrets (replace with secure values before use)."
	}
	else {
		Write-Host "No secrets were written. You can add them later with 'dotnet user-secrets set'."
	}
}
