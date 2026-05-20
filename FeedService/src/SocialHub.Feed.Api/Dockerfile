FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SocialHub.Feed.Api/SocialHub.Feed.Api.csproj", "src/SocialHub.Feed.Api/"]
COPY ["src/SocialHub.Feed.Application/SocialHub.Feed.Application.csproj", "src/SocialHub.Feed.Application/"]
COPY ["src/SocialHub.Feed.Domain/SocialHub.Feed.Domain.csproj", "src/SocialHub.Feed.Domain/"]
COPY ["src/SocialHub.Feed.Infrastructure/SocialHub.Feed.Infrastructure.csproj", "src/SocialHub.Feed.Infrastructure/"]
RUN dotnet restore "src/SocialHub.Feed.Api/SocialHub.Feed.Api.csproj"
COPY . .
WORKDIR "/src/src/SocialHub.Feed.Api"
RUN dotnet build "./SocialHub.Feed.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SocialHub.Feed.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SocialHub.Feed.Api.dll"]
