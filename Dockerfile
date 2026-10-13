# ---- Docly API ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Docly.Api/Docly.Api.csproj", "Docly.Api/"]
COPY ["Docly.App/Docly.App.csproj", "Docly.App/"]
COPY ["Docly.Domain/Docly.Domain.csproj", "Docly.Domain/"]
COPY ["Docly.Infra/Docly.Infra.csproj", "Docly.Infra/"]
RUN dotnet restore "Docly.Api/Docly.Api.csproj"
COPY . .
RUN dotnet publish "Docly.Api/Docly.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    UseHttpsRedirection=false \
    FileStorage__RootPath=/data/uploads
EXPOSE 8080
# Uploads live on a volume owned by the unprivileged "app" user.
RUN mkdir -p /data/uploads && chown -R app:app /data
VOLUME /data
USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Docly.Api.dll"]
