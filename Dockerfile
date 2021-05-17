FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /Pojeto1

# copy csproj and restore as distinct layers
COPY /. ./
COPY *.sln . 
RUN dotnet restore   
RUN dotnet publish -c release  

WORKDIR ./Carimbador/bin/Release/net5.0/publish/ 
 
ENTRYPOINT ["dotnet", "Carimbador.dll"]

 