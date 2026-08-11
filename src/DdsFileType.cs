////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-ddsfiletype-plus, a DDS FileType plugin
// for Paint.NET that adds support for the DX10 and later formats.
//
// Copyright (c) 2017-2026 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using DdsFileTypePlus.Interop;
using PaintDotNet;
using PaintDotNet.Dds;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace DdsFileTypePlus
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class DdsFileType : PropertyBasedFileType
    {
        private static readonly IReadOnlyList<string> FileExtensions = new string[] { ".dds" };

        private readonly IServiceProvider services;
        private readonly IDdsStringResourceManager strings;

        public DdsFileType(IServiceProvider services) :
            base(GetFileTypeName(services.GetService<IDdsFileTypePlusStrings>()),
                 new FileTypeOptions()
                 {
                     LoadExtensions = FileExtensions,
                     SaveExtensions = FileExtensions
                 })
        {
            this.services = services;
            IDdsFileTypePlusStrings ddsFileTypePlusStrings = services.GetService<IDdsFileTypePlusStrings>();

            if (ddsFileTypePlusStrings != null)
            {
                this.strings = new PdnLocalizedStringResourceManager(ddsFileTypePlusStrings);
            }
            else
            {
                this.strings = new BuiltinStringResourceManager();
            }
        }

        private static string GetFileTypeName(IDdsFileTypePlusStrings strings)
        {
            return strings?.TryGetString(DdsFileTypePlusStringName.FileType_Name) ?? Properties.Resources.FileType_Name;
        }

        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new()
            {
                new BooleanProperty(PropertyNames.MatchLoadedDds, true),
                CreateFileFormat(),
                new BooleanProperty(PropertyNames.ErrorDiffusionDithering, true),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.BC7CompressionSpeed, BC7CompressionSpeed.Medium, false),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.ErrorMetric, DdsErrorMetric.Perceptual, false),
                new BooleanProperty(PropertyNames.CubeMap, false),
                new BooleanProperty(PropertyNames.GenerateMipMaps, false),
                new BooleanProperty(PropertyNames.MgsvCompatibility, false),
                CreateMipMapResamplingAlgorithm(),
                new BooleanProperty(PropertyNames.UseGammaCorrection, true),
                new StringProperty(PropertyNames.FoxEngineCredit),
                new StringProperty(PropertyNames.PluginVersion),
            };

            List<PropertyCollectionRule> rules = new()
            {
                new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(
                    PropertyNames.ErrorDiffusionDithering,
                    PropertyNames.FileFormat,
                    new object[]
                    {
                        DdsFileFormat.BC1,
                        DdsFileFormat.BC1Srgb,
                        DdsFileFormat.BC2,
                        DdsFileFormat.BC2Srgb,
                        DdsFileFormat.BC3,
                        DdsFileFormat.BC3Srgb,
                        DdsFileFormat.BC3Rxgb,
                        DdsFileFormat.B4G4R4A4,
                        DdsFileFormat.B5G5R5A1,
                        DdsFileFormat.B5G6R5
                    },
                    true),
                new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(
                    PropertyNames.BC7CompressionSpeed,
                    PropertyNames.FileFormat,
                    new object[]
                    {
                        DdsFileFormat.BC6HUnsigned,
                        DdsFileFormat.BC7,
                        DdsFileFormat.BC7Srgb
                    },
                    true),
                new ReadOnlyBoundToValueRule<object, StaticListChoiceProperty>(
                    PropertyNames.ErrorMetric,
                    PropertyNames.FileFormat,
                    new object[]
                    {
                        DdsFileFormat.BC1,
                        DdsFileFormat.BC1Srgb,
                        DdsFileFormat.BC2,
                        DdsFileFormat.BC2Srgb,
                        DdsFileFormat.BC3,
                        DdsFileFormat.BC3Srgb,
                        DdsFileFormat.BC3Rxgb,
                    },
                    true),
                new ReadOnlyBoundToBooleanRule(PropertyNames.MipMapResamplingAlgorithm, PropertyNames.GenerateMipMaps, true),
                new ReadOnlyBoundToBooleanRule(PropertyNames.UseGammaCorrection, PropertyNames.GenerateMipMaps, true)
            };

            return new PropertyCollection(props, rules);

            static StaticListChoiceProperty CreateFileFormat()
            {
                object[] values = new object[]
                {
                    DdsFileFormat.BC1,
                    DdsFileFormat.BC1Srgb,
                    DdsFileFormat.BC2,
                    DdsFileFormat.BC2Srgb,
                    DdsFileFormat.BC3,
                    DdsFileFormat.BC3Srgb,
                    DdsFileFormat.BC3Rxgb,
                    DdsFileFormat.BC4Unsigned,
                    DdsFileFormat.BC4Ati1,
                    DdsFileFormat.BC5Unsigned,
                    DdsFileFormat.BC5Ati2,
                    DdsFileFormat.BC5Signed,
                    DdsFileFormat.BC6HUnsigned,
                    DdsFileFormat.BC7,
                    DdsFileFormat.BC7Srgb,
                    DdsFileFormat.B8G8R8A8,
                    DdsFileFormat.B8G8R8A8Srgb,
                    DdsFileFormat.B8G8R8X8,
                    DdsFileFormat.B8G8R8X8Srgb,
                    DdsFileFormat.R8G8B8A8,
                    DdsFileFormat.R8G8B8A8Srgb,
                    DdsFileFormat.R8G8B8X8,
                    DdsFileFormat.B5G5R5A1,
                    DdsFileFormat.B4G4R4A4,
                    DdsFileFormat.B5G6R5,
                    DdsFileFormat.B8G8R8,
                    DdsFileFormat.R8Unsigned,
                    DdsFileFormat.R8G8Unsigned,
                    DdsFileFormat.R8G8Signed,
                    DdsFileFormat.R32Float,
                };

                int defaultChoiceIndex = Array.IndexOf(values, DdsFileFormat.BC1);

                return new StaticListChoiceProperty(PropertyNames.FileFormat, values, defaultChoiceIndex, false);
            }

            static StaticListChoiceProperty CreateMipMapResamplingAlgorithm()
            {
                object[] values = new object[]
                {
                    ResamplingAlgorithm.Cubic,
                    ResamplingAlgorithm.CubicSmooth,
                    ResamplingAlgorithm.Linear,
                    ResamplingAlgorithm.LinearLowQuality,
                    ResamplingAlgorithm.AdaptiveHighQuality,
                    ResamplingAlgorithm.Lanczos3,
                    ResamplingAlgorithm.Fant,
                    ResamplingAlgorithm.NearestNeighbor,
                };

                int defaultChoiceIndex = Array.IndexOf(values, ResamplingAlgorithm.Cubic);

                return new StaticListChoiceProperty(PropertyNames.MipMapResamplingAlgorithm, values, defaultChoiceIndex, false);
            }
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultSaveConfigUI(props);

            PropertyControlInfo matchLoadedDdsPCI = configUI.FindControlForPropertyName(PropertyNames.MatchLoadedDds);
            matchLoadedDdsPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            matchLoadedDdsPCI.ControlProperties[ControlInfoPropertyNames.Description].Value =
                "Match loaded DDS (format, header style, exact mip count and cube-map state). " +
                "If this document was not loaded from a supported DDS, the manual settings below are used.";

            PropertyControlInfo formatPCI = configUI.FindControlForPropertyName(PropertyNames.FileFormat);
            formatPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            formatPCI.SetValueDisplayName(DdsFileFormat.BC1, this.strings.GetString("DdsFileFormat_BC1"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC1Srgb, this.strings.GetString("DdsFileFormat_BC1Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC2, this.strings.GetString("DdsFileFormat_BC2"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC2Srgb, this.strings.GetString("DdsFileFormat_BC2Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC3, this.strings.GetString("DdsFileFormat_BC3"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC3Srgb, this.strings.GetString("DdsFileFormat_BC3Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC3Rxgb, this.strings.GetString("DdsFileFormat_BC3Rxgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC4Unsigned, this.strings.GetString("DdsFileFormat_BC4Unsigned"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC4Ati1, this.strings.GetString("DdsFileFormat_BC4ATI1"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC5Unsigned, this.strings.GetString("DdsFileFormat_BC5Unsigned"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC5Ati2, this.strings.GetString("DdsFileFormat_BC5ATI2"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC5Signed, this.strings.GetString("DdsFileFormat_BC5Signed"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC6HUnsigned, this.strings.GetString("DdsFileFormat_BC6HUnsigned"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC7, this.strings.GetString("DdsFileFormat_BC7"));
            formatPCI.SetValueDisplayName(DdsFileFormat.BC7Srgb, this.strings.GetString("DdsFileFormat_BC7Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B8G8R8A8, this.strings.GetString("DdsFileFormat_B8G8R8A8"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B8G8R8A8Srgb, this.strings.GetString("DdsFileFormat_B8G8R8A8Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B8G8R8X8, this.strings.GetString("DdsFileFormat_B8G8R8X8"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B8G8R8X8Srgb, this.strings.GetString("DdsFileFormat_B8G8R8X8Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R8G8B8A8, this.strings.GetString("DdsFileFormat_R8G8B8A8"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R8G8B8A8Srgb, this.strings.GetString("DdsFileFormat_R8G8B8A8Srgb"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R8G8B8X8, this.strings.GetString("DdsFileFormat_R8G8B8X8"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B5G5R5A1, this.strings.GetString("DdsFileFormat_B5G5R5A1"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B4G4R4A4, this.strings.GetString("DdsFileFormat_B4G4R4A4"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B5G6R5, this.strings.GetString("DdsFileFormat_B5G6R5"));
            formatPCI.SetValueDisplayName(DdsFileFormat.B8G8R8, this.strings.GetString("DdsFileFormat_B8G8R8"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R8Unsigned, this.strings.GetString("DdsFileFormat_R8Unsigned"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R8G8Unsigned, this.strings.GetString("DdsFileFormat_R8G8Unsigned"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R8G8Signed, this.strings.GetString("DdsFileFormat_R8G8Signed"));
            formatPCI.SetValueDisplayName(DdsFileFormat.R32Float, this.strings.GetString("DdsFileFormat_R32Float"));

            PropertyControlInfo ditheringPCI = configUI.FindControlForPropertyName(PropertyNames.ErrorDiffusionDithering);
            ditheringPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            ditheringPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("ErrorDiffusionDithering_Description");

            PropertyControlInfo compressionModePCI = configUI.FindControlForPropertyName(PropertyNames.BC7CompressionSpeed);
            compressionModePCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("BC7CompressionSpeed_DisplayName");
            compressionModePCI.SetValueDisplayName(BC7CompressionSpeed.Fast, this.strings.GetString("BC7CompressionSpeed_Fast"));
            compressionModePCI.SetValueDisplayName(BC7CompressionSpeed.Medium, this.strings.GetString("BC7CompressionSpeed_Medium"));
            compressionModePCI.SetValueDisplayName(BC7CompressionSpeed.Slow, this.strings.GetString("BC7CompressionSpeed_Slow"));

            PropertyControlInfo errorMetricPCI = configUI.FindControlForPropertyName(PropertyNames.ErrorMetric);
            errorMetricPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("ErrorMetric_DisplayName");
            errorMetricPCI.ControlType.Value = PropertyControlType.RadioButton;
            errorMetricPCI.SetValueDisplayName(DdsErrorMetric.Perceptual, this.strings.GetString("ErrorMetric_Perceptual"));
            errorMetricPCI.SetValueDisplayName(DdsErrorMetric.Uniform, this.strings.GetString("ErrorMetric_Uniform"));

            PropertyControlInfo cubemapPCI = configUI.FindControlForPropertyName(PropertyNames.CubeMap);
            cubemapPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            cubemapPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("CubeMap_Description");

            PropertyControlInfo generateMipPCI = configUI.FindControlForPropertyName(PropertyNames.GenerateMipMaps);
            generateMipPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            generateMipPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("GenerateMipMaps_Description");

            PropertyControlInfo mgsvCompatibilityPCI = configUI.FindControlForPropertyName(PropertyNames.MgsvCompatibility);
            mgsvCompatibilityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            mgsvCompatibilityPCI.ControlProperties[ControlInfoPropertyNames.Description].Value =
                "MGSV / FOX Engine compatibility (stop generated mipmaps at 4x4 instead of 1x1).";

            PropertyControlInfo mipResamplingPCI = configUI.FindControlForPropertyName(PropertyNames.MipMapResamplingAlgorithm);
            mipResamplingPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.Cubic, this.strings.GetString("ResamplingAlgorithm_Cubic"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.CubicSmooth, this.strings.GetString("ResamplingAlgorithm_CubicSmooth"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.Linear, this.strings.GetString("ResamplingAlgorithm_Linear"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.LinearLowQuality, this.strings.GetString("ResamplingAlgorithm_LinearLowQuality"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.AdaptiveHighQuality, this.strings.GetString("ResamplingAlgorithm_AdaptiveHighQuality"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.Lanczos3, this.strings.GetString("ResamplingAlgorithm_Lanczos3"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.Fant, this.strings.GetString("ResamplingAlgorithm_Fant"));
            mipResamplingPCI.SetValueDisplayName(ResamplingAlgorithm.NearestNeighbor, this.strings.GetString("ResamplingAlgorithm_NearestNeighbor"));

            PropertyControlInfo gammaCorrectionPCI = configUI.FindControlForPropertyName(PropertyNames.UseGammaCorrection);
            gammaCorrectionPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            gammaCorrectionPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("UseGammaCorrection_Description");

            PropertyControlInfo foxEngineCreditInfo = configUI.FindControlForPropertyName(PropertyNames.FoxEngineCredit);
            foxEngineCreditInfo.ControlType.Value = PropertyControlType.Label;
            foxEngineCreditInfo.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            foxEngineCreditInfo.ControlProperties[ControlInfoPropertyNames.Description].Value =
                "* Custom version of exporter designed for FOX Engine by Choom Raider";

            PropertyControlInfo pluginVersionInfo = configUI.FindControlForPropertyName(PropertyNames.PluginVersion);
            pluginVersionInfo.ControlType.Value = PropertyControlType.Label;
            pluginVersionInfo.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            pluginVersionInfo.ControlProperties[ControlInfoPropertyNames.Description].Value = "DdsFileTypePlus MGSV v" + VersionInfo.PluginVersion;

            return configUI;
        }

        protected override Document OnLoad(Stream input)
        {
            DdsReferenceInfo reference = null;
            DdsReferenceInfo.TryRead(input, out reference);

            Document document = DdsReader.Load(input, this.services);

            if (reference != null)
            {
                DdsReferenceStore.Set(document, reference);
            }

            return document;
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            bool matchLoadedDds = token.GetProperty<BooleanProperty>(PropertyNames.MatchLoadedDds).Value;
            DdsFileFormat fileFormat = (DdsFileFormat)token.GetProperty(PropertyNames.FileFormat).Value;
            bool errorDiffusionDithering = token.GetProperty<BooleanProperty>(PropertyNames.ErrorDiffusionDithering).Value;
            BC7CompressionSpeed compressionSpeed = (BC7CompressionSpeed)token.GetProperty(PropertyNames.BC7CompressionSpeed).Value;
            DdsErrorMetric errorMetric = (DdsErrorMetric)token.GetProperty(PropertyNames.ErrorMetric).Value;
            bool cubeMap = token.GetProperty<BooleanProperty>(PropertyNames.CubeMap).Value;
            bool generateMipmaps = token.GetProperty<BooleanProperty>(PropertyNames.GenerateMipMaps).Value;
            bool mgsvCompatibility = token.GetProperty<BooleanProperty>(PropertyNames.MgsvCompatibility).Value;
            ResamplingAlgorithm mipSampling = (ResamplingAlgorithm)token.GetProperty(PropertyNames.MipMapResamplingAlgorithm).Value;
            bool useGammaCorrection = token.GetProperty<BooleanProperty>(PropertyNames.UseGammaCorrection).Value;

            int exactMipLevels = 0;
            int referenceWidth = 0;
            int referenceHeight = 0;
            DdsFileOptions? fileOptionsOverride = null;

            if (matchLoadedDds && DdsReferenceStore.TryGet(input, out DdsReferenceInfo reference))
            {
                fileFormat = reference.Format;
                cubeMap = reference.CubeMap;
                generateMipmaps = reference.MipLevels > 1;
                mgsvCompatibility = false;

                exactMipLevels = reference.MipLevels;
                referenceWidth = reference.Width;
                referenceHeight = reference.Height;
                fileOptionsOverride = reference.FileOptions;
            }

            DdsWriter.Save(this.services,
                           input,
                           output,
                           fileFormat,
                           errorDiffusionDithering,
                           compressionSpeed,
                           errorMetric,
                           cubeMap,
                           generateMipmaps,
                           mgsvCompatibility,
                           exactMipLevels,
                           referenceWidth,
                           referenceHeight,
                           fileOptionsOverride,
                           mipSampling,
                           useGammaCorrection,
                           scratchSurface,
                           progressCallback);
        }

        public enum PropertyNames
        {
            MatchLoadedDds,
            FileFormat,
            BC7CompressionSpeed,
            ErrorMetric,
            CubeMap,
            GenerateMipMaps,
            MgsvCompatibility,
            MipMapResamplingAlgorithm,
            ErrorDiffusionDithering,
            UseGammaCorrection,
            FoxEngineCredit,
            PluginVersion
        }
    }
}
