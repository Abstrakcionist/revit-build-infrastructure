$dirs = @(
    "$PSScriptRoot\..\Install\Resources\Icons",
    "$PSScriptRoot\..\..\RevitAddIn2\source\Spp\Resources\Icons"
)

foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

Add-Type -AssemblyName System.Drawing

function Save-Png {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height = $Width
    )

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::FromArgb(255, 0, 120, 215))
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

$pluginIcons = "$PSScriptRoot\..\..\RevitAddIn2\source\Spp\Resources\Icons"
$infraIcons = "$PSScriptRoot\..\Install\Resources\Icons"

Save-Png -Path "$pluginIcons\RibbonIcon16.png" -Width 16
Save-Png -Path "$pluginIcons\RibbonIcon32.png" -Width 32
Save-Png -Path "$infraIcons\BannerImage.png" -Width 493 -Height 58
Save-Png -Path "$infraIcons\BackgroundImage.png" -Width 493 -Height 312

$icon = [System.Drawing.Icon]::ExtractAssociatedIcon("$env:SystemRoot\System32\shell32.dll")
$fileStream = [System.IO.File]::Create("$infraIcons\ShellIcon.ico")
$icon.Save($fileStream)
$fileStream.Close()

Write-Host "Placeholder icons created."
