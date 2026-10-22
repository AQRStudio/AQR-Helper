# Downscales the 900x900 grade icons into the mod's embedded assets.
# Difficulty icons (letters+digits, -2, BYD, BYD+) -> 192px; quality icons (s+, s, a+, a..e) -> 96px.
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
)

Add-Type -AssemblyName System.Drawing

$difficultyDir = Join-Path $Destination 'difficulty'
$qualityDir = Join-Path $Destination 'quality'
New-Item -ItemType Directory -Force -Path $difficultyDir, $qualityDir | Out-Null

foreach ($file in Get-ChildItem -LiteralPath $Source -Filter *.png) {
    $isQuality = $file.BaseName -cmatch '^[a-z]\+?$'
    $size = if ($isQuality) { 96 } else { 192 }
    $target = Join-Path ($(if ($isQuality) { $qualityDir } else { $difficultyDir })) $file.Name

    $image = [System.Drawing.Image]::FromFile($file.FullName)
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($image, 0, 0, $size, $size)
    $bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose()
    $bitmap.Dispose()
    $image.Dispose()
}
