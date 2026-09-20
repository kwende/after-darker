# Source attribution

`src/AfterDarker.Core/Rendering/EllipseRasterizer.cs` adapts the integer boundary
walk from Alois Zingl's `plotEllipseRect` in
[Bresenham, revision ce9002f5aa1d95bdb7d71a39ebdb0987df139db5](https://github.com/zingl/Bresenham/blob/ce9002f5aa1d95bdb7d71a39ebdb0987df139db5/bresenham.c).
The upstream source identifies version V20.15, April 2020. Its
[MIT license](zingl-bresenham.txt) is included here.

The adaptation uses descriptive C# names and 64-bit integer arithmetic and
records scanline bounds instead of calling a pixel function. Our surface owns
clipping, RGB writes, change counting and the two-pixel band policy. See
[Hard Rain's rendering limits](../docs/research/hard-rain-execution.md).
This is source attribution, not an added runtime package or native library.
