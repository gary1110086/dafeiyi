$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskSource=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'Assets\Reading\whale-portrait.png'))
$taskSizes=@(16,24,32,48,64,128,256)
$taskFrames=[Collections.Generic.List[byte[]]]::new()
try {
    foreach($taskSize in $taskSizes) {
        $taskBitmap=[Drawing.Bitmap]::new($taskSize,$taskSize,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
        $taskMemory=[IO.MemoryStream]::new()
        try {
            $taskGraphics.Clear([Drawing.Color]::Transparent)
            $taskGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $taskGraphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $taskGraphics.DrawImage($taskSource,0,0,$taskSize,$taskSize)
            $taskBitmap.Save($taskMemory,[Drawing.Imaging.ImageFormat]::Png)
            $taskFrames.Add($taskMemory.ToArray())
        } finally { $taskMemory.Dispose(); $taskGraphics.Dispose(); $taskBitmap.Dispose() }
    }
    $taskStream=[IO.File]::Create((Join-Path $PSScriptRoot 'app.ico'))
    $taskWriter=[IO.BinaryWriter]::new($taskStream)
    try {
        $taskWriter.Write([uint16]0); $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]$taskSizes.Count)
        $taskOffset=6+16*$taskSizes.Count
        for($taskIndex=0;$taskIndex -lt $taskSizes.Count;$taskIndex++) {
            $taskDimension=if($taskSizes[$taskIndex] -eq 256) { 0 } else { $taskSizes[$taskIndex] }
            $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([byte]$taskDimension)
            $taskWriter.Write([byte]0); $taskWriter.Write([byte]0)
            $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]32)
            $taskWriter.Write([uint32]$taskFrames[$taskIndex].Length); $taskWriter.Write([uint32]$taskOffset)
            $taskOffset+=$taskFrames[$taskIndex].Length
        }
        foreach($taskFrame in $taskFrames) { $taskWriter.Write($taskFrame) }
    } finally { $taskWriter.Dispose(); $taskStream.Dispose() }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'app.ico') -Destination (Join-Path $PSScriptRoot 'Assets\Reading\whale-app.ico') -Force
} finally { $taskSource.Dispose() }
