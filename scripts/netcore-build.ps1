	
dotnet publish -r win-x64 -c Release -p:PublishSingleFile=true -p:PublishedTrimmed=true

dotnet publish --self-contained

dotnet publish -r win-x64 -c Release --self-contained $true --no-dependencies -p:PublishSingleFile=true -p:PublishedTrimmed=true

dotnet publish -r win-x64 -c Debug --self-contained $true --no-dependencies -p:PublishSingleFile=true -p:PublishedTrimmed=true

# to pass args add ' -- ' to command first
# not working in git codespaces
dotnet run -- -h

# 20-10-25 19:19:22 not working
dotnet publish .\CollectSFData.csproj -r win-x64 -c Release --no-dependencies -p:PublishedTrimmed=true

dotnet publish .\CollectSFData\CollectSFData.csproj -f netcoreapp3.1 -r win-x64 -c Release --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=false


dotnet store --manifest .\CollectSFData\CollectSFData.csproj --runtime win10-x64 --framework netcoreapp3.1 --skip-optimization

dotnet publish .\CollectSFData\CollectSFData.csproj --manifest C:\Users\jagilber\.dotnet\store\x64\netcoreapp3.1\artifact.xml -f netcoreapp3.1 -r win-x64 -c Release --self-contained $true -p:PublishSingleFile=true -p:PublishedTrimmed=false --force 