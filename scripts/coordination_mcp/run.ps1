$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($env:TP2_COORDINATION_TOKEN)) {
    throw "Set TP2_COORDINATION_TOKEN (24+ characters) in the local process environment."
}
$projectPython = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.venv\Scripts\python.exe"))
$python = if (Test-Path -LiteralPath $projectPython) { $projectPython } else { "python" }
& $python "$PSScriptRoot\server.py"
