FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .

# Restore and publish the API project (adjust path if your csproj name differs)
RUN dotnet restore "e.l.f. Beauty/e.l.f-brewery-api/e.l.f-brewery-api.csproj"
RUN dotnet publish "e.l.f. Beauty/e.l.f-brewery-api/e.l.f-brewery-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "e.l.f. Beauty.dll"]
