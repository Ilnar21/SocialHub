FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SocialHub.Notification.Api/SocialHub.Notification.Api.csproj", "src/SocialHub.Notification.Api/"]
COPY ["src/SocialHub.Notification.Application/SocialHub.Notification.Application.csproj", "src/SocialHub.Notification.Application/"]
COPY ["src/SocialHub.Notification.Domain/SocialHub.Notification.Domain.csproj", "src/SocialHub.Notification.Domain/"]
COPY ["src/SocialHub.Notification.Infrastructure/SocialHub.Notification.Infrastructure.csproj", "src/SocialHub.Notification.Infrastructure/"]
RUN dotnet restore "src/SocialHub.Notification.Api/SocialHub.Notification.Api.csproj"
COPY . .
RUN dotnet publish "src/SocialHub.Notification.Api/SocialHub.Notification.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SocialHub.Notification.Api.dll"]
