	
dotnet publish -r win-x64 -c Release -p:PublishSingleFile=true -p:PublishedTrimmed=true

dotnet publish --self-contained

dotnet publish -r win-x64 -c Release --self-contained $true --no-dependencies -p:PublishSingleFile=true -p:PublishedTrimmed=true

dotnet publish -r win-x64 -c Debug --self-contained $true --no-dependencies -p:PublishSingleFile=true -p:PublishedTrimmed=true

# to pass args add ' -- ' to command first
# not working in git codespaces
dotnet run -- -h

dotnet publish .\CollectSFData.csproj -r win-x64 -c Release --no-dependencies -p:PublishedTrimmed=true