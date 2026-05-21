FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SocialHub.Moderation.Api/SocialHub.Moderation.Api.csproj", "src/SocialHub.Moderation.Api/"]
COPY ["src/SocialHub.Moderation.Application/SocialHub.Moderation.Application.csproj", "src/SocialHub.Moderation.Application/"]
COPY ["src/SocialHub.Moderation.Domain/SocialHub.Moderation.Domain.csproj", "src/SocialHub.Moderation.Domain/"]
COPY ["src/SocialHub.Moderation.Infrastructure/SocialHub.Moderation.Infrastructure.csproj", "src/SocialHub.Moderation.Infrastructure/"]
RUN dotnet restore "src/SocialHub.Moderation.Api/SocialHub.Moderation.Api.csproj"
COPY . .
RUN dotnet publish "src/SocialHub.Moderation.Api/SocialHub.Moderation.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SocialHub.Moderation.Api.dll"]
