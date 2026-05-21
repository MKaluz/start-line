FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/StartLine.Api/StartLine.Api.csproj", "src/StartLine.Api/"]
COPY ["src/StartLine.Application/StartLine.Application.csproj", "src/StartLine.Application/"]
COPY ["src/StartLine.Domain/StartLine.Domain.csproj", "src/StartLine.Domain/"]
COPY ["src/StartLine.Infrastructure/StartLine.Infrastructure.csproj", "src/StartLine.Infrastructure/"]
RUN dotnet restore "src/StartLine.Api/StartLine.Api.csproj"
COPY . .
WORKDIR "/src/src/StartLine.Api"
RUN dotnet build "StartLine.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "StartLine.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StartLine.Api.dll"]
