# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/API/TaskFlow.API.csproj", "src/API/"]
COPY ["src/Application/TaskFlow.Application.csproj", "src/Application/"]
COPY ["src/Infrastructure/TaskFlow.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Domain/TaskFlow.Domain.csproj", "src/Domain/"]
RUN dotnet restore "src/API/TaskFlow.API.csproj"

COPY . .
RUN dotnet build "src/API/TaskFlow.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/API/TaskFlow.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskFlow.API.dll"]
