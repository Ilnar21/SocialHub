FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SocialHub.Moderation.Api.csproj .
RUN dotnet restore SocialHub.Moderation.Api.csproj
COPY . .
RUN dotnet publish SocialHub.Moderation.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SocialHub.Moderation.Api.dll"]
