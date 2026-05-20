FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SocialHub.Auth.Api/SocialHub.Auth.Api.csproj", "src/SocialHub.Auth.Api/"]
COPY ["src/SocialHub.Auth.Application/SocialHub.Auth.Application.csproj", "src/SocialHub.Auth.Application/"]
COPY ["src/SocialHub.Auth.Domain/SocialHub.Auth.Domain.csproj", "src/SocialHub.Auth.Domain/"]
COPY ["src/SocialHub.Auth.Infrastructure/SocialHub.Auth.Infrastructure.csproj", "src/SocialHub.Auth.Infrastructure/"]
RUN dotnet restore "src/SocialHub.Auth.Api/SocialHub.Auth.Api.csproj"
COPY . .
WORKDIR "/src/src/SocialHub.Auth.Api"
RUN dotnet build "./SocialHub.Auth.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SocialHub.Auth.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SocialHub.Auth.Api.dll"]
