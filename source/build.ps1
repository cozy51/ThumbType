$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x のコンパイラが見つかりません。' }
$target = Join-Path $PSScriptRoot '..\ThumbType.exe'
$sources = @('Program.cs', 'Engine.cs', 'Native.cs', 'SelfTest.cs', 'LayoutSettings.cs', 'InputRouter.cs', 'YabLayout.cs', 'AssemblyInfo.cs', 'StartupRegistration.cs', 'TrayIcon.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/resource:$PSScriptRoot\..\layouts\NICOLA.yab,ThumbType.NICOLA.yab" "/win32icon:$PSScriptRoot\..\assets\app.ico" "/resource:$PSScriptRoot\..\assets\app.ico,ThumbShiftPortable.AppIcon" "/win32manifest:$PSScriptRoot\app.manifest" "/out:$target" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }
Write-Output "作成しました: $target"
