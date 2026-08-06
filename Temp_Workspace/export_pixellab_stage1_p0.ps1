param([string]$Root = (Split-Path $PSScriptRoot -Parent))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$token = [Environment]::GetEnvironmentVariable('PIXELLAB_API_TOKEN', 'Machine')
if ([string]::IsNullOrWhiteSpace($token)) { throw 'PIXELLAB_API_TOKEN is missing.' }
$headers = @{ Authorization = "Bearer $token" }

$jobs = @(
    @{ Id='35ec375a-021b-4c8e-9cf5-cc0e16079585'; Unit='ShieldSentinel'; Clip='Idle' },
    @{ Id='c1092115-2021-42ab-9987-dacc12c024ff'; Unit='ShieldSentinel'; Clip='Move' },
    @{ Id='c32ee5ed-e51a-4c8b-95b5-404a1f0ed62b'; Unit='ShieldSentinel'; Clip='Hit' },
    @{ Id='f1865836-d02e-4a18-990f-e1c208a265d8'; Unit='ShieldSentinel'; Clip='Death' },
    @{ Id='2d96fa68-dd96-40dc-b9f5-b0b8a208e045'; Unit='ShieldSentinel'; Clip='Attack6003' },
    @{ Id='4828ba61-b839-4188-9c2b-3a6c52d44c7f'; Unit='ShieldSentinel'; Clip='Attack6004' },
    @{ Id='bec0f030-bad7-465f-8a22-996767963a4d'; Unit='OrbitalMarksman'; Clip='Idle' },
    @{ Id='787090b8-66b4-4744-a959-6e4832cd6477'; Unit='OrbitalMarksman'; Clip='Move' },
    @{ Id='6273bb9f-c2b6-4c5e-b472-a41600224450'; Unit='OrbitalMarksman'; Clip='Hit' },
    @{ Id='2b0b869e-9613-4854-8b2e-376708a0d36b'; Unit='OrbitalMarksman'; Clip='Death' },
    @{ Id='b9c4913b-8b93-43bd-83b7-fe8b19ea06cf'; Unit='OrbitalMarksman'; Clip='Attack6005' },
    @{ Id='adec95fc-bc8f-4e86-b352-7b0e0b7a6b6b'; Unit='OrbitalMarksman'; Clip='Attack6006' }
)

function Get-Bitmap([string]$base64) {
    if ($base64.StartsWith('data:')) { $base64 = $base64.Substring($base64.IndexOf(',') + 1) }
    $stream = [IO.MemoryStream]::new([Convert]::FromBase64String($base64))
    try {
        $source = [Drawing.Bitmap]::new($stream)
        try { return [Drawing.Bitmap]::new($source) } finally { $source.Dispose() }
    } finally { $stream.Dispose() }
}

function Get-AlphaBounds([Drawing.Bitmap]$image) {
    $left=$image.Width; $top=$image.Height; $right=-1; $bottom=-1
    for ($y=0; $y -lt $image.Height; $y++) {
        for ($x=0; $x -lt $image.Width; $x++) {
            if ($image.GetPixel($x,$y).A -gt 0) {
                $left=[Math]::Min($left,$x); $top=[Math]::Min($top,$y)
                $right=[Math]::Max($right,$x); $bottom=[Math]::Max($bottom,$y)
            }
        }
    }
    if ($right -lt 0) { throw 'Transparent frame returned by PixelLab.' }
    [Drawing.Rectangle]::FromLTRB($left,$top,$right+1,$bottom+1)
}

foreach ($job in $jobs) {
    $result = Invoke-RestMethod -Uri "https://api.pixellab.ai/v2/background-jobs/$($job.Id)" -Headers $headers
    if ($result.status -ne 'completed') { throw "$($job.Unit)/$($job.Clip) is $($result.status)." }

    $frames = @($result.last_response.images | ForEach-Object { Get-Bitmap $_.base64 })
    try {
        $bounds = @($frames | ForEach-Object { Get-AlphaBounds $_ })
        $union = $bounds[0]
        foreach ($bound in $bounds[1..($bounds.Count-1)]) { $union = [Drawing.Rectangle]::Union($union, $bound) }
        $base = $bounds[0]
        $scale = [Math]::Min(128.0/$base.Height, [Math]::Min(120.0/$union.Width, 248.0/$union.Height))
        $scaledCanvas = [int][Math]::Round(256*$scale)
        $destX = [int][Math]::Round(64-($base.Left+$base.Width/2.0)*$scale)
        $destY = [int][Math]::Round(252-$base.Bottom*$scale)
        $unionLeft = $destX+$union.Left*$scale
        $unionRight = $destX+$union.Right*$scale
        if ($unionLeft -lt 4) { $destX += [int][Math]::Ceiling(4-$unionLeft) }
        if ($unionRight -gt 124) { $destX -= [int][Math]::Ceiling($unionRight-124) }

        $sheet = [Drawing.Bitmap]::new(128*$frames.Count, 256, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [Drawing.Graphics]::FromImage($sheet)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
                for ($i=0; $i -lt $frames.Count; $i++) {
                    $target = [Drawing.Rectangle]::new(128*$i+$destX,$destY,$scaledCanvas,$scaledCanvas)
                    $graphics.DrawImage($frames[$i],$target,0,0,256,256,[Drawing.GraphicsUnit]::Pixel)
                }
            } finally { $graphics.Dispose() }

            $dir = Join-Path $Root "Assets\Textures\Characters\Monsters\$($job.Unit)"
            [IO.Directory]::CreateDirectory($dir) | Out-Null
            $path = Join-Path $dir "$($job.Unit)_$($job.Clip).png"
            $sheet.Save($path,[Drawing.Imaging.ImageFormat]::Png)
            Write-Output "$($job.Unit)_$($job.Clip): $($frames.Count) frames -> $path"
        } finally { $sheet.Dispose() }
    } finally { $frames | ForEach-Object { $_.Dispose() } }
}
