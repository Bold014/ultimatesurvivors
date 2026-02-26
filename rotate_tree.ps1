Add-Type -AssemblyName System.Drawing
$imgPath = 'c:\Users\cybor\Desktop\s&box projects\ultimatesurvivors\Assets\Textures\treesolo.png'
$bmp = New-Object System.Drawing.Bitmap($imgPath)
Write-Host "Before: $($bmp.Width) x $($bmp.Height)"
$bmp.RotateFlip([System.Drawing.RotateFlipType]::Rotate90FlipNone)
Write-Host "After:  $($bmp.Width) x $($bmp.Height)"
$bmp.Save($imgPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Done."
