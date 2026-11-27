# Third-Party Notices

PipSentry itself is MIT licensed (see `LICENSE`). It is built with, and redistributes, the components listed here.

Each entry was checked against package metadata (the `.nuspec` inside each package) in the local NuGet package folders: `C:\Users\isksh\.nuget\packages` for NuGet packages, and the toolchain kept under `sdk\` and `.tools\` in this repository. Where a package does not declare an SPDX license expression, the license line says "see package metadata" and gives the URL that the package itself carries.

## SharpDX

- Version: 4.2.0
- License: MIT. The 4.2.0 package declares the license as a URL (`http://sharpdx.org/License.txt`), not as an SPDX expression, and that host now redirects to an unrelated site. The MIT terms are the ones the SharpDX project publishes; see package metadata.
- Copyright: Copyright (c) 2010-2016 Alexandre Mutel
- Used in: `src/PipSentry/Capture/GpuScaler.cs` (COM object and `Result` helpers, `SharpDX.Mathematics.Interop` types).

## SharpDX.Direct3D11

- Version: 4.2.0
- License: MIT. Declared as a URL in the package (`http://sharpdx.org/License.txt`), not as an SPDX expression; see package metadata.
- Copyright: Copyright (c) 2010-2016 Alexandre Mutel
- Used in: `src/PipSentry/Capture/WgcInterop.cs`, `src/PipSentry/Capture/WgcCaptureEngine.cs` and `src/PipSentry/Capture/GpuScaler.cs` (D3D11 device, textures and the staging texture used for read-back).

## SharpDX.DXGI

- Version: 4.2.0
- License: MIT. Declared as a URL in the package (`http://sharpdx.org/License.txt`), not as an SPDX expression; see package metadata.
- Copyright: Copyright (c) 2010-2016 Alexandre Mutel
- Used in: `src/PipSentry/Capture/WgcCaptureEngine.cs` and `src/PipSentry/Capture/GpuScaler.cs` (DXGI device and surface handling for the Windows Graphics Capture frame pool).

## SharpDX.Direct2D1

- Version: 4.2.0
- License: MIT. Declared as a URL in the package (`http://sharpdx.org/License.txt`), not as an SPDX expression; see package metadata.
- Copyright: Copyright (c) 2010-2016 Alexandre Mutel
- Used in: `src/PipSentry/Capture/GpuScaler.cs` (Direct2D render target and bitmap, used to scale a capture to the display size on the GPU). Added in 1.2.0.

## .NET 8 runtime and WPF

- Version: 8.0.34 (`Microsoft.NETCore.App.Runtime.win-x64`, `Microsoft.WindowsDesktop.App.Runtime.win-x64`; the SDK in `sdk\` is 8.0.428)
- License: MIT, declared as a license expression in both runtime packages (`https://licenses.nuget.org/MIT`). The runtime files that ship with the SDK also carry the Microsoft .NET Library license terms in `sdk/LICENSE.txt`.
- Copyright: (c) Microsoft Corporation. All rights reserved. The runtime source is copyright the .NET Foundation and contributors.
- Used in: the whole application. PipSentry is published self-contained and single-file, so the runtime and the WPF assemblies are redistributed inside `PipSentry.exe`.

## Windows SDK projections

- Version: 10.0.22621.56 (`Microsoft.Windows.SDK.NET.Ref`, matching the `net8.0-windows10.0.22621.0` target)
- License: see package metadata. The package declares a license URL (`https://aka.ms/WinSDKLicenseURL`) and no SPDX expression.
- Copyright: (c) Microsoft Corporation. All rights reserved.
- Used in: the WinRT projections the app compiles against, mainly `Windows.Graphics.Capture` in `src/PipSentry/Capture/WgcInterop.cs` and `src/PipSentry/Capture/WgcCaptureEngine.cs`.

## WiX Toolset 5

- Version: 5.0.2 (installed as a .NET tool into `.tools/wix` by `build.ps1`)
- License: MS-RL, declared as a license expression in the `wix` package (`https://licenses.nuget.org/MS-RL`).
- Copyright: Copyright (c) .NET Foundation and contributors. All rights reserved.
- Used in: build time only, to build `packaging/PipSentry.wxs` into `dist/PipSentry-<version>-x64.msi`. Nothing from the toolset is redistributed with PipSentry.

## Windows APIs called directly

PipSentry calls Windows APIs through P/Invoke rather than through a third-party wrapper. The declarations are in `src/PipSentry/Interop/Native.cs` and `src/PipSentry/Capture/WgcInterop.cs`. Examples are `SetThreadExecutionState`, `RedrawWindow`, `RegisterHotKey` and the DWM and window enumeration APIs. These are part of Windows itself and are covered by the Windows license. No third-party interop library is involved.
