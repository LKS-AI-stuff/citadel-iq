# Build context is the repository root (the API references sibling projects):
#   docker build -f CitadelIQ.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CitadelIQ.slnx ./
COPY CitadelIQ.Domain/*.csproj CitadelIQ.Domain/
COPY CitadelIQ.Application/*.csproj CitadelIQ.Application/
COPY CitadelIQ.Infrastructure/*.csproj CitadelIQ.Infrastructure/
COPY CitadelIQ.FluentMigrations/*.csproj CitadelIQ.FluentMigrations/
COPY CitadelIQ.Api/*.csproj CitadelIQ.Api/
RUN dotnet restore CitadelIQ.Api/CitadelIQ.Api.csproj
COPY CitadelIQ.Domain/ CitadelIQ.Domain/
COPY CitadelIQ.Application/ CitadelIQ.Application/
COPY CitadelIQ.Infrastructure/ CitadelIQ.Infrastructure/
COPY CitadelIQ.FluentMigrations/ CitadelIQ.FluentMigrations/
COPY CitadelIQ.Api/ CitadelIQ.Api/
RUN dotnet publish CitadelIQ.Api/CitadelIQ.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Raw uploaded files (Storage:DocumentsPath, relative to the content root) — mount a volume here.
RUN mkdir -p /app/App_Data/documents
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CitadelIQ.Api.dll"]
