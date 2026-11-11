param([string]$OutputName = 'PreviewBlur.exe')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Compiler .NET Framework tidak ditemukan.'
}
$output = Join-Path $PSScriptRoot $OutputName
& $compiler /nologo /unsafe /target:winexe /optimize+ "/out:$output" /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'src\PreviewBlur.cs') (Join-Path $PSScriptRoot 'src\ModernControls.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build gagal.' }
Write-Output "Selesai: $output"
