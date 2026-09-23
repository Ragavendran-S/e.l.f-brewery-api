# PowerShell script to start the API, call endpoints using admin/password, capture requests/responses and write CSV
# Usage: From repository root run: powershell -ExecutionPolicy Bypass -File .\scripts\generate_api_csv.ps1

param(
	[string]$ProjectPath = "e.l.f. Beauty\e.l.f. Beauty.csproj",
	[string]$BaseUrl = "http://localhost:5000",
	[string]$OutputCsv = "scripts\api_responses.csv"
)

function Wait-For-Url {
	param($url, $timeoutSeconds = 60)
	$start = Get-Date
	while (((Get-Date) - $start).TotalSeconds -lt $timeoutSeconds) {
		try {
			$r = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -ErrorAction Stop
			return $true
		} catch {
			Start-Sleep -Milliseconds 500
		}
	}
	return $false
}

# Start the API with explicit URL and environment so it binds predictably
$env:ASPNETCORE_URLS = $BaseUrl
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Starting web API (project: $ProjectPath) using $BaseUrl..."
$proc = Start-Process -FilePath dotnet -ArgumentList "run --project '$ProjectPath' --no-launch-profile" -PassThru -WindowStyle Hidden

# Wait for app to be ready (poll swagger or root)
$ready = Wait-For-Url -url "$BaseUrl/swagger/index.html" -timeoutSeconds 30
if (-not $ready) {
	# fallback: try root
	$ready = Wait-For-Url -url "$BaseUrl/" -timeoutSeconds 15
}

if (-not $ready) {
	Write-Host "Server did not become ready in time. Proceeding anyway..."
}

# Helper to safely json-encode payloads for CSV cells
function SafeJson([object]$obj) {
	if ($null -eq $obj) { return "" }
	try { return ($obj | ConvertTo-Json -Depth 10) -replace '"','""' } catch { return ($obj.ToString()) -replace '"','""' }
}

$rows = @()

# 1) Login to get token
$loginUrl = "$BaseUrl/api/auth/login"
$loginPayload = @{ Username = 'admin'; Password = 'password' }
try {
	$loginResp = Invoke-RestMethod -Uri $loginUrl -Method Post -Body ($loginPayload | ConvertTo-Json) -ContentType 'application/json' -ErrorAction Stop
	# Expect either object with token or plain string. Try common keys
	$token = $null
	if ($loginResp -is [System.Management.Automation.PSCustomObject]) {
		if ($loginResp.token) { $token = $loginResp.token }
		elseif ($loginResp.access_token) { $token = $loginResp.access_token }
		elseif ($loginResp.Token) { $token = $loginResp.Token }
		else { $token = ($loginResp | ConvertTo-Json -Depth 5) }
	} else {
		$token = $loginResp
	}
	$status = 200
} catch {
	$token = $null
	$status = ($_.Exception.Response.StatusCode.value__ 2>$null) -as [int]
	if (-not $status) { $status = 500 }
	$loginError = $_.Exception.Message
}

$rows += [pscustomobject]@{
	Method = 'POST'; Url = $loginUrl; RequestPayload = SafeJson $loginPayload; ResponseStatus = $status; ResponseBody = SafeJson $loginResp
}

# Prepare Authorization header for subsequent requests
$authHeader = @{}
if ($token) { $authHeader = @{ Authorization = "Bearer $token" } }

# Define endpoints to call (method, url, optional payload)
$endpoints = @(
	@{ Method = 'GET'; Url = "$BaseUrl/api/v1/breweries"; Payload = $null },
	@{ Method = 'GET'; Url = "$BaseUrl/api/v1/breweries?search=ale&page=1&pageSize=5"; Payload = $null },
	@{ Method = 'GET'; Url = "$BaseUrl/api/v1/breweries/autocomplete?query=ale"; Payload = $null },
	@{ Method = 'GET'; Url = "$BaseUrl/api/v1/breweries/Sierra%20Nevada"; Payload = $null },
	@{ Method = 'POST'; Url = "$BaseUrl/api/v1/breweries"; Payload = @{ Name = 'Test Brewery From Script'; BreweryType = 'micro'; City = 'Testville'; State = 'TS' } },
	@{ Method = 'GET'; Url = "$BaseUrl/api/v2/breweries"; Payload = $null },
	@{ Method = 'GET'; Url = "$BaseUrl/api/test/protected"; Payload = $null },
	@{ Method = 'POST'; Url = "$BaseUrl/api/auth/validate"; Payload = $token }
)

foreach ($ep in $endpoints) {
	$method = $ep.Method
	$url = $ep.Url
	$payload = $ep.Payload

	try {
		if ($method -eq 'GET') {
			$resp = Invoke-RestMethod -Uri $url -Method Get -Headers $authHeader -ErrorAction Stop
			$code = 200
		} elseif ($method -eq 'POST') {
			if ($payload -is [string]) {
				$body = $payload
				$resp = Invoke-RestMethod -Uri $url -Method Post -Body $body -Headers $authHeader -ContentType 'application/json' -ErrorAction Stop
			} else {
				$body = $payload | ConvertTo-Json -Depth 10
				$resp = Invoke-RestMethod -Uri $url -Method Post -Body $body -Headers $authHeader -ContentType 'application/json' -ErrorAction Stop
			}
			$code = 200
		} else {
			$resp = "Unsupported method"
			$code = 0
		}
	} catch {
		$resp = $_.Exception.Response | ForEach-Object { try { ($_ | ConvertTo-Json -Depth 5) } catch { $_.ToString() } }
		$code = ($_.Exception.Response.StatusCode.value__ 2>$null) -as [int]
		if (-not $code) { $code = 500 }
	}

	$rows += [pscustomobject]@{
		Method = $method
		Url = $url
		RequestPayload = SafeJson $payload
		ResponseStatus = $code
		ResponseBody = SafeJson $resp
	}
}

# Write CSV
$rows | Export-Csv -Path $OutputCsv -NoTypeInformation -Force
Write-Host "Wrote CSV to $OutputCsv"

# Stop the server process we started
try {
	if ($proc -and -not $proc.HasExited) {
		Write-Host "Stopping server (PID $($proc.Id))..."
		$proc | Stop-Process -Force
	}
} catch {
	Write-Host "Failed to stop server process: $_"
}

Write-Host "Done."
