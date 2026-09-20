#!/usr/bin/env bash
# Simple curl script demonstrating login and calling protected endpoints

BASE_URL=${BASE_URL:-http://localhost:5000}

echo "Logging in..."
TOKEN=$(curl -s -X POST "$BASE_URL/api/auth/login" -H "Content-Type: application/json" -d '{"username":"admin","password":"password"}' | jq -r '.token')
echo "Token: $TOKEN"

echo "Calling /api/v1/breweries"
curl -s "$BASE_URL/api/v1/breweries" -H "Authorization: Bearer $TOKEN" -H "Accept: application/json" | jq '.'

echo "Calling autocomplete"
curl -s "$BASE_URL/api/v1/breweries/autocomplete?query=lag" -H "Authorization: Bearer $TOKEN" -H "Accept: application/json" | jq '.'
