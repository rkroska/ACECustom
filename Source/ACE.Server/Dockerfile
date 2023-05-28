#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["ACE.Server/ACE.Server.csproj", "ACE.Server/"]
COPY ["ACE.Adapter/ACE.Adapter.csproj", "ACE.Adapter/"]
COPY ["ACE.Database/ACE.Database.csproj", "ACE.Database/"]
COPY ["ACE.Entity/ACE.Entity.csproj", "ACE.Entity/"]
COPY ["ACE.Common/ACE.Common.csproj", "ACE.Common/"]
COPY ["ACE.DatLoader/ACE.DatLoader.csproj", "ACE.DatLoader/"]
RUN dotnet restore "ACE.Server/ACE.Server.csproj"
COPY . .
WORKDIR "/src/ACE.Server"
RUN dotnet build "ACE.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ACE.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ACE.Server.dll"]

# ports and volumes
EXPOSE 9000-9001/udp
VOLUME /ace/Config /ace/Content /ace/Dats /ace/Logs


# health check
HEALTHCHECK --start-period=5m --interval=1m --timeout=3s \
  CMD netstat -an | grep 9000 > /dev/null; if [ 0 != $? ]; then exit 1; fi;
