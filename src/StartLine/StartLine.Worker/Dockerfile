# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["StartLine.Worker/StartLine.Worker.csproj", "StartLine.Worker/"]
COPY ["StartLine.Application/StartLine.Application.csproj", "StartLine.Application/"]
COPY ["StartLine.Domain/StartLine.Domain.csproj", "StartLine.Domain/"]
COPY ["StartLine.Infrastructure/StartLine.Infrastructure.csproj", "StartLine.Infrastructure/"]
RUN dotnet restore "./StartLine.Worker/StartLine.Worker.csproj"
COPY . .
WORKDIR "/src/StartLine.Worker"
RUN dotnet build "./StartLine.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./StartLine.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StartLine.Worker.dll"]
