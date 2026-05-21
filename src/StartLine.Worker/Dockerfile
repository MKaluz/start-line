FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/StartLine.Worker/StartLine.Worker.csproj", "src/StartLine.Worker/"]
COPY ["src/StartLine.Application/StartLine.Application.csproj", "src/StartLine.Application/"]
COPY ["src/StartLine.Domain/StartLine.Domain.csproj", "src/StartLine.Domain/"]
COPY ["src/StartLine.Infrastructure/StartLine.Infrastructure.csproj", "src/StartLine.Infrastructure/"]
RUN dotnet restore "src/StartLine.Worker/StartLine.Worker.csproj"
COPY . .
WORKDIR "/src/src/StartLine.Worker"
RUN dotnet build "StartLine.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StartLine.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StartLine.Worker.dll"]
