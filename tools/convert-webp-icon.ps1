# Converts a WebP (or any WIC-decodable image) to a square PNG of the given size.
# Used for the website's "unknown" grade icons, which are only published as WebP.
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination,
    [Parameter(Mandatory = $true)][int]$Size
)

Add-Type -AssemblyName PresentationCore

$input = [System.IO.File]::OpenRead($Source)
try {
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
        $input,
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $frame = $decoder.Frames[0]
    $scale = New-Object System.Windows.Media.ScaleTransform(($Size / $frame.PixelWidth), ($Size / $frame.PixelHeight))
    $scaled = New-Object System.Windows.Media.Imaging.TransformedBitmap($frame, $scale)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($scaled))
    $output = [System.IO.File]::Create($Destination)
    try {
        $encoder.Save($output)
    }
    finally {
        $output.Dispose()
    }
}
finally {
    $input.Dispose()
}
