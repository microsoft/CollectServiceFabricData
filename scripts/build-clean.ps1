$error.clear()
$ErrorActionPreference = 'continue'
$currentLocation = (get-location).Path

Set-Location ..\$PSScriptRoot

rd .\bin -re -fo
rd .\obj -re -fo


cd .\CollectSFData
rd .\bin -re -fo
rd .\obj -re -fo
cd ..

cd .\CollectSFDataDll
rd .\bin -re -fo
rd .\obj -re -fo
cd ..

cd .\CollectSFDataTest
rd .\bin -re -fo
rd .\obj -re -fo
cd ..

set-location $currentLocation