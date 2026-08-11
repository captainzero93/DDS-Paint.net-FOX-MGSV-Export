////////////////////////////////////////////////////////////////////////
//
// MGSV / FOX Engine extension for DdsFileTypePlus.
//
// Remembers the DDS header metadata that Paint.NET itself does not keep
// when it loads only the top image of a DDS file.
//
////////////////////////////////////////////////////////////////////////

using DdsFileTypePlus.Interop;
using PaintDotNet;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace DdsFileTypePlus
{
    internal sealed class DdsReferenceInfo
    {
        internal int Width { get; }
        internal int Height { get; }
        internal int MipLevels { get; }
        internal bool CubeMap { get; }
        internal bool Dx10Header { get; }
        internal DdsFileFormat Format { get; }
        internal DdsFileOptions FileOptions { get; }

        private DdsReferenceInfo(
            int width,
            int height,
            int mipLevels,
            bool cubeMap,
            bool dx10Header,
            DdsFileFormat format,
            DdsFileOptions fileOptions)
        {
            Width = width;
            Height = height;
            MipLevels = mipLevels;
            CubeMap = cubeMap;
            Dx10Header = dx10Header;
            Format = format;
            FileOptions = fileOptions;
        }

        internal static bool TryRead(Stream input, out DdsReferenceInfo info)
        {
            info = null;

            if (input == null || !input.CanRead || !input.CanSeek)
            {
                return false;
            }

            long originalPosition = input.Position;

            try
            {
                input.Position = 0;

                using BinaryReader reader = new(input, Encoding.ASCII, leaveOpen: true);

                if (reader.ReadUInt32() != 0x20534444) // "DDS "
                {
                    return false;
                }

                if (reader.ReadUInt32() != 124)
                {
                    return false;
                }

                uint headerFlags = reader.ReadUInt32();
                int height = checked((int)reader.ReadUInt32());
                int width = checked((int)reader.ReadUInt32());

                reader.ReadUInt32(); // pitchOrLinearSize
                reader.ReadUInt32(); // depth
                uint mipMapCountRaw = reader.ReadUInt32();

                // Pixel format begins at absolute file offset 76.
                input.Position = 76;

                if (reader.ReadUInt32() != 32)
                {
                    return false;
                }

                uint pixelFormatFlags = reader.ReadUInt32();
                uint fourCC = reader.ReadUInt32();
                uint rgbBitCount = reader.ReadUInt32();
                uint rMask = reader.ReadUInt32();
                uint gMask = reader.ReadUInt32();
                uint bMask = reader.ReadUInt32();
                uint aMask = reader.ReadUInt32();

                // Caps2 is at absolute file offset 112.
                input.Position = 112;
                uint caps2 = reader.ReadUInt32();

                bool cubeMap = (caps2 & 0x00000200U) != 0;
                bool dx10Header = fourCC == MakeFourCC('D', 'X', '1', '0');

                DdsFileFormat format;
                DdsFileOptions fileOptions;

                if (dx10Header)
                {
                    if (input.Length < 148)
                    {
                        return false;
                    }

                    input.Position = 128;

                    uint dxgiFormat = reader.ReadUInt32();
                    reader.ReadUInt32(); // resourceDimension
                    uint miscFlag = reader.ReadUInt32();
                    reader.ReadUInt32(); // arraySize
                    reader.ReadUInt32(); // miscFlags2

                    cubeMap |= (miscFlag & 0x4U) != 0;

                    if (!TryMapDxgiFormat(dxgiFormat, out format))
                    {
                        return false;
                    }

                    fileOptions = DdsFileOptions.ForceDx10Header;
                }
                else
                {
                    if (!TryMapLegacyFormat(
                        pixelFormatFlags,
                        fourCC,
                        rgbBitCount,
                        rMask,
                        gMask,
                        bMask,
                        aMask,
                        out format,
                        out fileOptions))
                    {
                        return false;
                    }
                }

                const uint DdsdMipMapCount = 0x00020000U;
                int mipLevels = ((headerFlags & DdsdMipMapCount) != 0 && mipMapCountRaw > 0)
                    ? checked((int)mipMapCountRaw)
                    : 1;

                if (width <= 0 || height <= 0 || mipLevels <= 0)
                {
                    return false;
                }

                info = new DdsReferenceInfo(
                    width,
                    height,
                    mipLevels,
                    cubeMap,
                    dx10Header,
                    format,
                    fileOptions);

                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            finally
            {
                input.Position = originalPosition;
            }
        }

        private static bool TryMapDxgiFormat(uint dxgiFormat, out DdsFileFormat format)
        {
            // DXGI_FORMAT numeric values from the Windows SDK.
            switch (dxgiFormat)
            {
                case 28: format = DdsFileFormat.R8G8B8A8; return true;
                case 29: format = DdsFileFormat.R8G8B8A8Srgb; return true;
                case 41: format = DdsFileFormat.R32Float; return true;
                case 49: format = DdsFileFormat.R8G8Unsigned; return true;
                case 51: format = DdsFileFormat.R8G8Signed; return true;
                case 61: format = DdsFileFormat.R8Unsigned; return true;
                case 71: format = DdsFileFormat.BC1; return true;
                case 72: format = DdsFileFormat.BC1Srgb; return true;
                case 74: format = DdsFileFormat.BC2; return true;
                case 75: format = DdsFileFormat.BC2Srgb; return true;
                case 77: format = DdsFileFormat.BC3; return true;
                case 78: format = DdsFileFormat.BC3Srgb; return true;
                case 80: format = DdsFileFormat.BC4Unsigned; return true;
                case 83: format = DdsFileFormat.BC5Unsigned; return true;
                case 84: format = DdsFileFormat.BC5Signed; return true;
                case 85: format = DdsFileFormat.B5G6R5; return true;
                case 86: format = DdsFileFormat.B5G5R5A1; return true;
                case 87: format = DdsFileFormat.B8G8R8A8; return true;
                case 88: format = DdsFileFormat.B8G8R8X8; return true;
                case 91: format = DdsFileFormat.B8G8R8A8Srgb; return true;
                case 93: format = DdsFileFormat.B8G8R8X8Srgb; return true;
                case 95: format = DdsFileFormat.BC6HUnsigned; return true;
                case 98: format = DdsFileFormat.BC7; return true;
                case 99: format = DdsFileFormat.BC7Srgb; return true;
                case 115: format = DdsFileFormat.B4G4R4A4; return true;
                default:
                    format = default;
                    return false;
            }
        }

        private static bool TryMapLegacyFormat(
            uint pixelFormatFlags,
            uint fourCC,
            uint rgbBitCount,
            uint rMask,
            uint gMask,
            uint bMask,
            uint aMask,
            out DdsFileFormat format,
            out DdsFileOptions fileOptions)
        {
            const uint DdpfFourCC = 0x00000004U;
            const uint DdpfRgb = 0x00000040U;
            const uint DdpfLuminance = 0x00020000U;
            const uint DdpfBumpDuDv = 0x00080000U;

            fileOptions = DdsFileOptions.ForceLegacyDX9Formats;

            if ((pixelFormatFlags & DdpfFourCC) != 0)
            {
                if (fourCC == MakeFourCC('D', 'X', 'T', '1'))
                {
                    format = DdsFileFormat.BC1;
                    return true;
                }
                if (fourCC == MakeFourCC('D', 'X', 'T', '2') ||
                    fourCC == MakeFourCC('D', 'X', 'T', '3'))
                {
                    format = DdsFileFormat.BC2;
                    return true;
                }
                if (fourCC == MakeFourCC('D', 'X', 'T', '4') ||
                    fourCC == MakeFourCC('D', 'X', 'T', '5'))
                {
                    format = DdsFileFormat.BC3;
                    return true;
                }
                if (fourCC == MakeFourCC('R', 'X', 'G', 'B'))
                {
                    format = DdsFileFormat.BC3Rxgb;
                    fileOptions = DdsFileOptions.ForceBC3ToRXGB;
                    return true;
                }
                if (fourCC == MakeFourCC('A', 'T', 'I', '1'))
                {
                    format = DdsFileFormat.BC4Ati1;
                    return true;
                }
                if (fourCC == MakeFourCC('A', 'T', 'I', '2'))
                {
                    format = DdsFileFormat.BC5Ati2;
                    return true;
                }
                if (fourCC == MakeFourCC('B', 'C', '4', 'U'))
                {
                    format = DdsFileFormat.BC4Unsigned;
                    return true;
                }
                if (fourCC == MakeFourCC('B', 'C', '5', 'U'))
                {
                    format = DdsFileFormat.BC5Unsigned;
                    return true;
                }
                if (fourCC == MakeFourCC('B', 'C', '5', 'S'))
                {
                    format = DdsFileFormat.BC5Signed;
                    return true;
                }

                format = default;
                return false;
            }

            if ((pixelFormatFlags & DdpfRgb) != 0)
            {
                if (rgbBitCount == 32)
                {
                    if (rMask == 0x00FF0000U &&
                        gMask == 0x0000FF00U &&
                        bMask == 0x000000FFU)
                    {
                        format = aMask == 0xFF000000U
                            ? DdsFileFormat.B8G8R8A8
                            : DdsFileFormat.B8G8R8X8;
                        return true;
                    }

                    if (rMask == 0x000000FFU &&
                        gMask == 0x0000FF00U &&
                        bMask == 0x00FF0000U)
                    {
                        format = aMask == 0xFF000000U
                            ? DdsFileFormat.R8G8B8A8
                            : DdsFileFormat.R8G8B8X8;
                        return true;
                    }
                }
                else if (rgbBitCount == 24 &&
                         rMask == 0x00FF0000U &&
                         gMask == 0x0000FF00U &&
                         bMask == 0x000000FFU)
                {
                    format = DdsFileFormat.B8G8R8;
                    return true;
                }
                else if (rgbBitCount == 16)
                {
                    if (rMask == 0x0000F800U &&
                        gMask == 0x000007E0U &&
                        bMask == 0x0000001FU &&
                        aMask == 0)
                    {
                        format = DdsFileFormat.B5G6R5;
                        return true;
                    }

                    if (rMask == 0x00007C00U &&
                        gMask == 0x000003E0U &&
                        bMask == 0x0000001FU &&
                        aMask == 0x00008000U)
                    {
                        format = DdsFileFormat.B5G5R5A1;
                        return true;
                    }

                    if (rMask == 0x00000F00U &&
                        gMask == 0x000000F0U &&
                        bMask == 0x0000000FU &&
                        aMask == 0x0000F000U)
                    {
                        format = DdsFileFormat.B4G4R4A4;
                        return true;
                    }
                }
            }

            if ((pixelFormatFlags & DdpfLuminance) != 0)
            {
                if (rgbBitCount == 8 && rMask == 0x000000FFU)
                {
                    format = DdsFileFormat.R8Unsigned;
                    return true;
                }

                if (rgbBitCount == 16 &&
                    rMask == 0x000000FFU &&
                    aMask == 0x0000FF00U)
                {
                    format = DdsFileFormat.R8G8Unsigned;
                    return true;
                }
            }

            if ((pixelFormatFlags & DdpfBumpDuDv) != 0 &&
                rgbBitCount == 16 &&
                rMask == 0x000000FFU &&
                gMask == 0x0000FF00U)
            {
                format = DdsFileFormat.R8G8Signed;
                return true;
            }

            format = default;
            return false;
        }

        private static uint MakeFourCC(char a, char b, char c, char d)
        {
            return (uint)(byte)a |
                   ((uint)(byte)b << 8) |
                   ((uint)(byte)c << 16) |
                   ((uint)(byte)d << 24);
        }
    }

    internal static class DdsReferenceStore
    {
        private static readonly ConditionalWeakTable<Document, DdsReferenceInfo> references = new();

        internal static void Set(Document document, DdsReferenceInfo info)
        {
            if (document == null || info == null)
            {
                return;
            }

            references.Remove(document);
            references.Add(document, info);
        }

        internal static bool TryGet(Document document, out DdsReferenceInfo info)
        {
            info = null;

            if (document == null)
            {
                return false;
            }

            return references.TryGetValue(document, out info);
        }
    }
}
