FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ClubGear.csproj ./
COPY Contracts/Plugin/ClubGear.Plugin.Contracts.csproj Contracts/Plugin/
RUN dotnet restore ClubGear.csproj

COPY . .
RUN dotnet publish ClubGear.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ClubGear.dll"]
