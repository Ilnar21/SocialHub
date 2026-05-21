FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/SocialHub.Post.Domain/SocialHub.Post.Domain.csproj", "src/SocialHub.Post.Domain/"]
COPY ["src/SocialHub.Post.Application/SocialHub.Post.Application.csproj", "src/SocialHub.Post.Application/"]
COPY ["src/SocialHub.Post.Infrastructure/SocialHub.Post.Infrastructure.csproj", "src/SocialHub.Post.Infrastructure/"]
COPY ["src/SocialHub.Post.Api/SocialHub.Post.Api.csproj", "src/SocialHub.Post.Api/"]
RUN dotnet restore "src/SocialHub.Post.Api/SocialHub.Post.Api.csproj"
COPY . .
WORKDIR "/src/src/SocialHub.Post.Api"
RUN dotnet publish "SocialHub.Post.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SocialHub.Post.Api.dll"]
