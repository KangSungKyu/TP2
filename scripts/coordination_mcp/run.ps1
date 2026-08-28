$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($env:TP2_COORDINATION_TOKEN)) {
    throw "Set TP2_COORDINATION_TOKEN (24+ characters) in the local process environment."
}
$bundledPython = "C:\Users\PC\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
$python = if (Test-Path -LiteralPath $bundledPython) { $bundledPython } else { "python" }
& $python "$PSScriptRoot\server.py"
