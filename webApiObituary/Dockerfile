# Multi-stage Dockerfile for the webApiObituary API (optimized for caching)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files to leverage Docker cache during restore
COPY ["comp4976-a1-obituary.sln", "./"]
COPY ["webApiObituary/Assignment1.csproj", "webApiObituary/"]
COPY ["comp4976-a1-obituary.ServiceDefaults/comp4976-a1-obituary.ServiceDefaults.csproj", "comp4976-a1-obituary.ServiceDefaults/"]
COPY ["MemorialRegistry.Shared/MemorialRegistry.Shared.csproj", "MemorialRegistry.Shared/"]

RUN dotnet restore "webApiObituary/Assignment1.csproj"

# Copy everything and publish
COPY . .
WORKDIR /src/webApiObituary
RUN dotnet publish "Assignment1.csproj" -c Release -o /app/publish /p:TrimUnusedDependencies=true

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Assignment1.dll"]
