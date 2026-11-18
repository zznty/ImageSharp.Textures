using System.Diagnostics.CodeAnalysis;

// Texel format names mirror the DXGI/Vulkan identifiers so that container mapping tables read the same
// way as their specifications. The underscore is part of the public API.
[assembly: SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "Texel format names mirror the container format identifiers (DXGI/Vulkan).",
    Scope = "type",
    Target = "~T:ImageSharp.Textures.TexelFormat")]

// DDS DXGI format names are the DXGI_FORMAT identifiers, kept verbatim so the mapping reads like the spec.
[assembly: SuppressMessage(
    "Naming",
    "CA1707:Identifiers should not contain underscores",
    Justification = "DXGI format names are the DXGI_FORMAT identifiers.",
    Scope = "type",
    Target = "~T:ImageSharp.Textures.Formats.Dds.Enums.DxgiFormat")]
