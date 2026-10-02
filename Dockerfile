FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props HospitalManagement.sln ./
COPY DAL/Hospital.DAL.csproj DAL/
COPY Hospital.BLL/Hospital.BLL.csproj Hospital.BLL/
COPY HospitalManagement/HospitalManagement.csproj HospitalManagement/
COPY Hospital.Tests/Hospital.Tests.csproj Hospital.Tests/
RUN dotnet restore HospitalManagement.sln

COPY . .
RUN dotnet publish HospitalManagement/HospitalManagement.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN mkdir -p /var/lib/careaxis/keys /app/App_Data/profile-images \
    && chown -R app:app /var/lib/careaxis /app
COPY --from=build --chown=app:app /app/publish .
USER app
ENTRYPOINT ["dotnet", "HospitalManagement.dll"]
