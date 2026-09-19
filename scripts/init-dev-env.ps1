<#
Initialises dotnet user-secrets for local development.

Usage:
  ./scripts/init-dev-env.ps1        # interactive prompt
  ./scripts/init-dev-env.ps1 -NonInteractive  # write placeholder values

This script must be run from the repository root. It changes directory into the
"e.l.f. Beauty" project (where the UserSecretsId is declared) before running
`dotnet user-secrets` commands.

Note: Do not commit real secrets. Use this to set local secrets only.
#>

param(
	[switch]$NonInteractive
)

$projectPath = Join-Path $PSScriptRoot "..\e.l.f. Beauty"
Write-Host "Changing directory to project: $projectPath"
Set-Location $projectPath

if ($NonInteractive) {
	Write-Host "Initializing user-secrets and writing placeholder values..."
	dotnet user-secrets init
	dotnet user-secrets set "Jwt:Key" "REPLACE_WITH_STRONG_SECRET"
	dotnet user-secrets set "Jwt:Issuer" "elf-beauty"
	dotnet user-secrets set "Jwt:Audience" "elf-beauty"
	dotnet user-secrets set "Auth:Username" "local-admin"
	dotnet user-secrets set "Auth:Password" "local-password"
	Write-Host "Placeholder user-secrets created. Replace them with real values before use."
	exit 0
}

Write-Host "Interactive mode: you will be prompted to enter secrets (values will be written to user-secrets)."
dotnet user-secrets init

$jwtKey = Read-Host "Enter Jwt:Key (paste the full secret)"
if ([string]::IsNullOrWhiteSpace($jwtKey)) {
	Write-Host "Jwt:Key cannot be empty. Aborting." -ForegroundColor Red
	exit 1
}
dotnet user-secrets set "Jwt:Key" "$jwtKey"

$issuer = Read-Host "Enter Jwt:Issuer (press Enter to use 'elf-beauty')"
if ([string]::IsNullOrWhiteSpace($issuer)) { $issuer = "elf-beauty" }
dotnet user-secrets set "Jwt:Issuer" "$issuer"

$aud = Read-Host "Enter Jwt:Audience (press Enter to use 'elf-beauty')"
if ([string]::IsNullOrWhiteSpace($aud)) { $aud = "elf-beauty" }
dotnet user-secrets set "Jwt:Audience" "$aud"

$user = Read-Host "Enter Auth:Username (press Enter to use 'admin')"
if ([string]::IsNullOrWhiteSpace($user)) { $user = "admin" }
dotnet user-secrets set "Auth:Username" "$user"

$pwd = Read-Host "Enter Auth:Password (visible while typing)"
dotnet user-secrets set "Auth:Password" "$pwd"

Write-Host "Local user-secrets configured. Do not commit secrets to source control."
