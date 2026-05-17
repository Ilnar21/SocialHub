FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SocialHub.Community.Api/SocialHub.Community.Api.csproj", "src/SocialHub.Community.Api/"]
COPY ["src/SocialHub.Community.Application/SocialHub.Community.Application.csproj", "src/SocialHub.Community.Application/"]
COPY ["src/SocialHub.Community.Domain/SocialHub.Community.Domain.csproj", "src/SocialHub.Community.Domain/"]
COPY ["src/SocialHub.Community.Infrastructure/SocialHub.Community.Infrastructure.csproj", "src/SocialHub.Community.Infrastructure/"]
RUN dotnet restore "src/SocialHub.Community.Api/SocialHub.Community.Api.csproj"
COPY . .
RUN dotnet publish "src/SocialHub.Community.Api/SocialHub.Community.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SocialHub.Community.Api.dll"]
