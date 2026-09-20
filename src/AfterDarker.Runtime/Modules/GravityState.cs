using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Original ball data observed after a completed Gravity lifecycle call.</summary>
/// <param name="Color">Guest-selected RGB COLORREF.</param>
/// <param name="X">Current horizontal center in guest pixels.</param>
/// <param name="Y">Current vertical center in guest pixels.</param>
public sealed record GravityBall(uint Color, short X, short Y);

/// <summary>Detached observations from this artifact's globals; the host does not update the physics.</summary>
/// <param name="Compatibility">Guest PREINITIALIZE compatibility flag.</param>
/// <param name="InstanceHandle">DLL data selector retained by startup.</param>
/// <param name="DeviceContext">Host drawing HDC supplied with MODULE calls.</param>
/// <param name="System">Last locked AD_SYSTEM pointer.</param>
/// <param name="Module">Last locked AD_MODULE pointer.</param>
/// <param name="BallCount">Configured number of balls.</param>
/// <param name="BallSize">Configured diameter in guest pixels.</param>
/// <param name="Bitmap">Guest-owned strip of ball images/masks.</param>
/// <param name="Sound">Sound resource handle; zero under the unavailable-audio policy.</param>
/// <param name="Balls">Original color and position values.</param>
public sealed record GravityState(ushort Compatibility, ushort InstanceHandle, ushort DeviceContext,
    FarPointer16 System, FarPointer16 Module, ushort BallCount, ushort BallSize, ushort Bitmap, ushort Sound,
    IReadOnlyList<GravityBall> Balls);
