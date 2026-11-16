# DDS FileType Plus - MGSV / FOX Engine

Paint.NET filetype plugin for DDS, with MGSV / FOX Engine support. Needs Paint.NET 5.2 or newer. Paint.NET ships a stock DDS plugin; this replaces it.

## MGSV
- Match loaded DDS: saves with the opened file's format, header style, exact mip count and cube-map state. Use this for textures exported by FtexTool / FoxTexBrowser.
- MGSV compatibility: mip chain stops at 4x4 like FOX ftex files.
- Generate mip maps and MGSV compatibility are on by default.
- Legacy DX9 headers (DXT1/DXT5/A8R8G8B8/L8) as FtexTool expects.

## DDS support
- Single images and cube maps (crossed layout). Texture arrays and volume textures are not supported.
- Mipmapped files load the base level only; mips are regenerated on save.
- BC1-BC7 (Linear/sRGB, Unsigned/Signed, BC6H float), uncompressed R8, B4G4R4A4, B5G5R5A1, B5G6R5, 32-bit float and 8-bit RGBA/BGRA variants.
- Legacy DX9: ATI1/ATI2, RXGB, BGR/RGB layouts.

## Install
Close Paint.NET, copy `DdsFileTypePlus.dll` and `DdsFileTypePlusIO_x64.dll` into the FileTypes folder, restart.
- Classic: `C:\Program Files\paint.net\FileTypes`
- Store: `Documents\paint.net App Files\FileTypes`
- Portable: `FileTypes` beside `paintdotnet.exe`

## Build
VS 2022/2026 with the C++ tools, .NET 11 SDK, Paint.NET 5.2. Run `BUILD_MGSV_VS2026.bat` (as Administrator to install too). Output in `BUILD_OUTPUT`.

MIT licensed. Based on pdn-ddsfiletype-plus by Nicholas Hayes; uses Microsoft DirectXTex.
