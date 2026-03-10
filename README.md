# Texture Toolbox

A **Unity Editor** window for batch processing textures — resize, reformat, pad to power-of-two, set mipmaps, pack RGBA channels, and build texture atlases — all from a single panel.

## Overview

Working with large texture libraries is slow when settings must be changed one at a time. The Texture Toolbox processes every texture in a selected folder in a single pass, applying whichever combination of operations you enable, then logs the results in the same window.

## Features

- **Batch Folder Processing**: Point the tool at any `Assets/` subfolder and process all textures in it at once.
- **Resize**: Resample textures to a target width and height.
- **Change Format**: Apply any `TextureImporterFormat` (e.g., DXT5, RGBA32, ASTC) across the entire folder.
- **Pad to Power of Two**: Ensure every texture dimension is the nearest power-of-two.
- **Mipmap Control**: Enable or disable mipmap generation on all textures.
- **Channel Packing**: Combine up to four grayscale (or full-color) textures into a single RGBA texture — useful for ORM / mask maps.
- **Texture Atlas Builder**: Pack a folder of textures into a single atlas with configurable max resolution (512 – 8192) and per-sprite padding.
- **Operation Log**: Results and errors for every processed asset are shown in a scrollable log at the bottom of the window.

## Prerequisites

- **Unity** (2021.3 LTS or newer recommended).
- Place `Scripts/TextureToolbox.cs` inside an `Editor/` folder in your Unity project.

## Usage

1. Drop `Scripts/TextureToolbox.cs` into any `Editor/` folder (e.g., `Assets/Editor/`).
2. Open the window via **Tools > Texture Toolbox**.
3. Set the **Input Folder** (type a path, use the `...` picker, or drag a folder from the Project window).
4. Enable the desired operations and configure their settings.
5. Click **Process All Textures in Folder** to apply all enabled operations.

## Operations Reference

| Operation | Controls | Description |
|---|---|---|
| Resize | Width, Height | Resample to exact pixel dimensions |
| Change Format | Format dropdown | Set `TextureImporterFormat` for all textures |
| Pad to Power of Two | Toggle | Pad dimensions to the next power-of-two |
| Set Mipmaps | Generate Mipmaps toggle | Enable or disable mip generation |
| Channel Pack | R/G/B/A texture fields | Merge four textures into one RGBA output |
| Build Atlas | Max Size, Padding | Pack folder textures into a sprite atlas |

## Script Reference

### `Scripts/TextureToolbox.cs`
- **`ProcessFolder()`**: Iterates all texture assets in `_inputFolder`, applies enabled operations via `TextureImporter`, and triggers a reimport.
- **`DoChannelPack()`**: Reads RGBA pixel data from four source textures and writes them into a new combined `Texture2D` asset.
- **`BuildAtlas()`**: Calls `Texture2D.PackTextures()` on all textures in the folder with the configured max size and padding, then saves the result as a PNG.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
Copyright (c) 2025 ARGUS
