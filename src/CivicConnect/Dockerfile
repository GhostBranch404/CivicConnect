FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CivicConnect.sln ./
COPY src/CivicConnect/CivicConnect.csproj src/CivicConnect/
RUN dotnet restore CivicConnect.sln

COPY . .
RUN dotnet publish src/CivicConnect/CivicConnect.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update \
	&& apt-get install -y --no-install-recommends wget \
	&& rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CivicConnect.dll"]