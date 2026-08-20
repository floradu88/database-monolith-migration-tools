param(
  [string]$Repo = ".",
  [string]$Output = "topology.md",
  [ValidateSet("LR", "RL", "TB", "BT")]
  [string]$Direction = "LR",
  [string]$Title = "YAML Repository Topology",
  [string]$Adapters = "",
  [string]$ExplainDir = "",
  [switch]$NoStubs,
  [switch]$NoExplanations
)

$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Venv = Join-Path $Here ".venv"
$Python = Join-Path $Venv "Scripts\python.exe"

if (-not (Test-Path $Python)) {
  Write-Host "Creating local Python environment (no admin required)..."
  python -m venv $Venv
}

Write-Host "Installing/updating the only Python dependency inside the local virtual environment..."
& $Python -m pip install --disable-pip-version-check -q pyyaml

Write-Host "Recursively mapping YAML under: $Repo"
$cliArgs = @(
  (Join-Path $Here "yaml-topology.py"),
  $Repo,
  "--output", $Output,
  "--direction", $Direction,
  "--title", $Title,
  "--format", "markdown"
)
if ($Adapters) {
  $cliArgs += @("--adapters", $Adapters)
}
if ($NoStubs) {
  $cliArgs += "--no-stubs"
}
if ($NoExplanations) {
  $cliArgs += "--no-explanations"
}
if ($ExplainDir) {
  $cliArgs += @("--explain-dir", $ExplainDir)
}

& $Python @cliArgs

Write-Host "Done. Generated Markdown with an embedded MermaidJS diagram: $Output"
if ($ExplainDir) {
  Write-Host "Per-file explanations written under: $ExplainDir"
}
Write-Host "GitHub and Mermaid-capable Markdown previews can render the fenced mermaid block directly."
