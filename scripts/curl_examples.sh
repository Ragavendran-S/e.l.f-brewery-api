#!/usr/bin/env bash
set -euo pipefail

BASE_URL="http://localhost:5000"

# Obtain token
TOKEN=$(curl -s -X POST "$BASE_URL/api/auth/login" -H "Content-Type: application/json" -d '{"Username":"admin","Password":"password"}' | jq -r '.token')

echo "Token: $TOKEN"

echo "List breweries"
curl -s "$BASE_URL/api/v1/breweries?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN" | jq '.'

echo "Autocomplete"
curl -s "$BASE_URL/api/v1/breweries/autocomplete?query=lag" -H "Authorization: Bearer $TOKEN" | jq '.'
