# Changelog

All notable changes to Sprite Editor Pro will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0-beta] - 2025-01-15

### 🎉 Initial Beta Release

#### Added
- **Sprite Slicer Module**
  - Grid-based sprite slicing with adjustable rows/columns
  - Automatic sprite detection algorithm
  - Batch export functionality
  - Real-time grid preview overlay
  
- **Background Eraser Module**
  - Magic wand tool with tolerance control
  - Manual brush eraser with size adjustment
  - Color picker (pipette tool)
  - Side-by-side preview (original vs result)
  
- **Rigging & Skinning System**
  - Hierarchical bone structure creation
  - 4 operation modes: Edit, Create Bone, Edit Mesh, Pose
  - Automatic mesh vertex generation with alpha detection
  - Delaunay triangulation for mesh
  - Advanced auto-weighting system with 9 customizable parameters
  - Mesh deformation with real-time preview
  - Keyframe animation system
  - Rig data export/import (.rig.json format)
  - Camera controls (pan, zoom)
  
- **Format Converter Module**
  - Support for PNG, JPG, BMP, ICO, WebP formats
  - Real-time image preview
  - Smart resizing for ICO format
  
- **Frame Animator Module**
  - Multi-frame sprite sheet playback
  - Adjustable FPS (1-60)
  - Loop mode toggle
  - GIF export functionality
  
- **Texture Packer Module**
  - Multiple sprite atlas generation
  - Configurable atlas sizes (512-8192px)
  - Padding control
  - Transparency trimming (smart crop)
  - JSON metadata export with sprite coordinates
  
- **Story Editor Module** (Experimental)
  - Visual node-based narrative editor
  - Branching dialogue system
  - Global variable system
  - Conditional logic support
  - Choice buttons with target nodes
  - Background and character image support
  - Audio/music integration placeholders
  
- **UI/UX Features**
  - Modern dark theme interface
  - Collapsible sidebar with smooth animations
  - Multilingual support (7 languages)
  - Custom window chrome with minimize/maximize/close
  - Responsive layout
  - Tooltip system
  - Custom message boxes with icons
  
- **Localization**
  - English (en-US)
  - Azərbaycan (az-AZ)
  - Русский (ru-RU)
  - Türkçe (tr-TR)
  - Español (es-ES)
  - Português (pt-BR)
  - 中文/简体 (zh-CN)

#### Technical
- Built on .NET 8.0 and WPF
- MVVM architecture with CommunityToolkit.Mvvm
- SkiaSharp for hardware-accelerated rendering
- SixLabors.ImageSharp for image processing
- Magick.NET for advanced format support
- Triangle.NET for mesh triangulation

---

## [Unreleased]

### Planned Features
- Undo/Redo system
- Keyboard shortcuts
- Drag & Drop file loading
- Auto-save functionality
- Recent files menu
- Performance optimizations for large images
- Plugin architecture
- Batch processing mode
- Cloud storage integration

### Known Issues
- Performance degradation with 8K+ images in Rigging mode
- GIF export limited to 256 colors
- ICO format max resolution 256x256
- Story Editor player mode incomplete

---

## Version History

| Version | Release Date | Status |
|---------|--------------|--------|
| 1.0.0-beta | 2025-01-15 | Current |
| 1.1.0 | TBD | Planned |
| 1.2.0 | TBD | Planned |
| 2.0.0 | TBD | Roadmap |

---

## Release Notes Format

Each release will include:
- **Added**: New features
- **Changed**: Changes in existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Vulnerability patches

---

_For detailed commit history, see the [GitHub repository](https://github.com/yourusername/SpriteEditor/commits/main)._





