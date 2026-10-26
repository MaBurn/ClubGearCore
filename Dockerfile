FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ClubGear.csproj ./
COPY Contracts/Plugin/ClubGear.Plugin.Contracts.csproj Contracts/Plugin/
RUN dotnet restore ClubGear.csproj

COPY . .
RUN dotnet publish ClubGear.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Writable state (SQLite database, mail pickup directory) belongs here so a deployment can
# mount a volume over it. It is created while still root and handed to the app user: Docker
# copies ownership from the image when it initialises a named volume, whereas a volume
# mounted onto a path absent from the image would end up owned by root and unwritable.
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "ClubGear.dll"]
