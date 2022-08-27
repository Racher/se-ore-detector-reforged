set out=%AppData%\SpaceEngineers\Mods\"Ore Detector Reforged Test"
rmdir %out% /s /q
mkdir %out%
xcopy *.txt %out%\ /dy
xcopy *.steamtxt %out%\ /dy
xcopy *.md %out%\ /dy
xcopy *.sln %out%\ /dy
xcopy thumb.jpg %out%\ /dy
xcopy modinfo.sbmi %out%\ /dy
xcopy metadata.mod %out%\ /dy
xcopy .\Data\*.cs %out%\Data\ /sdy
xcopy .\Data\*.csproj %out%\Data\ /sdy
xcopy .\Data\*.sbc %out%\Data\ /sdy

set out=%AppData%\SpaceEngineers\Mods\"Ore Detector Reforged"
rmdir %out% /s /q
mkdir %out%
xcopy *.txt %out%\ /dy
xcopy *.steamtxt %out%\ /dy
xcopy *.md %out%\ /dy
xcopy *.sln %out%\ /dy
xcopy thumb.jpg %out%\ /dy
copy modinfo.sbmi-stable %out%\modinfo.sbmi /dy
xcopy metadata.mod %out%\ /dy
xcopy .\Data\*.cs %out%\Data\ /sdy
xcopy .\Data\*.csproj %out%\Data\ /sdy
xcopy .\Data\*.sbc %out%\Data\ /sdy
