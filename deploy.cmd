set out=%AppData%\SpaceEngineers\Mods\"Ore Detector Reforged PTR"
rmdir %out% /sq
mkdir %out%
xcopy *.md %out%\ /dy
xcopy *.sln %out%\ /dy
xcopy thumb.jpg %out%\ /dy
xcopy modinfo.sbmi %out%\ /dy
xcopy metadata.mod %out%\ /dy
xcopy .\Data\*.cs %out%\Data\ /sdy
xcopy .\Data\*.csproj %out%\Data\ /sdy
xcopy .\Data\*.sbc %out%\Data\ /sdy