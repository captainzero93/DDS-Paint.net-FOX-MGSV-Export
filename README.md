**What it is:**  
A Paint.NET filetype plugin that lets you open and save DDS texture files (the ones DirectX 10+ games/engines use). It’s already bundled with Paint.NET 4.2.2+, so if you’re on a newer version, it’s probably already there however this version adds specific support for MGSV. Just drop the DLLs into the right folder and restart.

**How it handles DDS files:**  
- Only supports **single images + cube maps**. Texture arrays and volume textures are ignored.
- When loading mipmapped files, it only pulls in the base layer. You can toggle to **generate mipmaps on save** if needed.
- Cube maps use the standard crossed layout (4x4 UVs). Editing them is a bit clunky since Paint.NET isn’t built for it.

**Supported formats (roughly):**  
- Compressed: BC1 through BC7 (covers Linear/sRGB, Unsigned, Signed, Float)
- Uncompressed: R8, B4/B5 variants, 32-bit float, etc.
- Legacy DX9 stuff too: ATI1/ATI2, RXGB (used for normal maps), plain BGR/RGA layouts

**Installation:**  
Close Paint.NET → drop `DdsFileTypePlus.dll` + the architecture-specific IO DLLs into your `FileTypes` folder (location changes depending on Classic/Store/Portable install) → restart. If you manually drop a newer version in, it automatically overrides whatever came bundled.

**Under the hood / to build it yourself:**  
MIT licensed. Heavy reliance on Microsoft’s DirectXTex for parsing. To compile: VS2022 + Paint.NET 5+, plus a few reference path tweaks and post-build copy steps. Not something you’d touch unless you’re patching or porting it.

**TL;DR:** Drop-in plugin that gives Paint.NET native read/write support for most modern DDS textures, cube maps, and legacy DX9 formats. Works out of the box if you have a decent version installed, otherwise just drop the DLLs in the right folder and go.
