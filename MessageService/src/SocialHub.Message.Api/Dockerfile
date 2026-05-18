FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/SocialHub.Message.Api/SocialHub.Message.Api.csproj", "src/SocialHub.Message.Api/"]
COPY ["src/SocialHub.Message.Application/SocialHub.Message.Application.csproj", "src/SocialHub.Message.Application/"]
COPY ["src/SocialHub.Message.Domain/SocialHub.Message.Domain.csproj", "src/SocialHub.Message.Domain/"]
COPY ["src/SocialHub.Message.Infrastructure/SocialHub.Message.Infrastructure.csproj", "src/SocialHub.Message.Infrastructure/"]
RUN dotnet restore "src/SocialHub.Message.Api/SocialHub.Message.Api.csproj"
COPY . .
RUN dotnet publish "src/SocialHub.Message.Api/SocialHub.Message.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SocialHub.Message.Api.dll"]
