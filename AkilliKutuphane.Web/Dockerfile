FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Build context should be solution root:
# docker build -f AkilliKutuphane.Web/Dockerfile -t akilli-kutuphane-web .
COPY ["AkilliKutuphane.sln", "./"]
COPY ["AkilliKutuphane.Web/AkilliKutuphane.Web.csproj", "AkilliKutuphane.Web/"]
COPY ["AkilliKutuphane.Business/AkilliKutuphane.Business.csproj", "AkilliKutuphane.Business/"]
COPY ["AkilliKutuphane.Data/AkilliKutuphane.Data.csproj", "AkilliKutuphane.Data/"]

RUN dotnet restore "AkilliKutuphane.Web/AkilliKutuphane.Web.csproj"

COPY . .
WORKDIR "/src/AkilliKutuphane.Web"
RUN dotnet publish "AkilliKutuphane.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AkilliKutuphane.Web.dll"]
