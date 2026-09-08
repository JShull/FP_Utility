param(
    [Parameter(Mandatory)][ValidateSet('prepare', 'execute', 'status', 'resume')][string]$Command,
    [Parameter(Mandatory)][string]$ProjectPath,
    [Parameter(Mandatory)][string]$InputPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ApprovedHash = ''
)
$ErrorActionPreference = 'Stop'
if ($Command -in @('execute', 'resume') -and $ApprovedHash -notmatch '^[a-f0-9]{64}$') {
    throw 'Execute/resume requires the exact reviewed manifest hash in -ApprovedHash.'
}
$inputFullPath = (Resolve-Path -LiteralPath $InputPath).Path
$outputFullPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $outputFullPath) { throw 'OutputPath must be a new file; preserve the reviewed manifest.' }
if (!(Test-Path -LiteralPath (Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'))) { throw 'ProjectPath must be a Unity project root.' }
# Base64 keeps input paths/text out of generated C# syntax and shell quoting.
$encodedInput = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($inputFullPath)))
$encodedOutput = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($outputFullPath))
$method = (Get-Culture).TextInfo.ToTitleCase($Command)
$decode = "var input = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(`"$encodedInput`")); var output = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(`"$encodedOutput`"));"
$code = if ($Command -in @('execute', 'resume')) {
    "$decode return FuzzPhyte.Utility.Editor.FPElevenLabsGenerationCli.Start(input, `"$ApprovedHash`", output);"
} else {
    "$decode var result = FuzzPhyte.Utility.Editor.FPElevenLabsGenerationCli.$method(input); using (var file = new System.IO.FileStream(output, System.IO.FileMode.CreateNew)) using (var writer = new System.IO.StreamWriter(file)) { writer.Write(result); } return result;"
}
# Use Unity CLI discovery, then the installed Pipeline JSON transport. This also supports
# Editors predating the CLI's newer /api/command-line parser, without a package upgrade.
$discovery = unity status --format json | ConvertFrom-Json
if (!$discovery.success) { throw 'Unity CLI discovery failed.' }
$projectFullPath = [IO.Path]::GetFullPath($ProjectPath).TrimEnd([char[]]'\/')
$instance = @($discovery.data.instances | Where-Object { [IO.Path]::GetFullPath($_.project).TrimEnd([char[]]'\/') -eq $projectFullPath })
if ($instance.Count -ne 1) { throw 'Expected exactly one connected Editor for ProjectPath.' }
$descriptor = Get-Content -Raw -LiteralPath (Join-Path $projectFullPath 'Library/Pipeline/.unity-pipeline-port') | ConvertFrom-Json
if ([IO.Path]::GetFullPath($descriptor.projectPath).TrimEnd([char[]]'\/') -ne $projectFullPath -or $descriptor.port -ne $instance[0].port -or $descriptor.pid -ne $instance[0].pid) { throw 'Editor descriptor does not match CLI discovery.' }
$body = @{ command = 'eval'; parameters = @{ code = $code; timeout = 30000 }; timeout = 35000 } | ConvertTo-Json -Depth 5
$response = Invoke-RestMethod -Uri "http://127.0.0.1:$($descriptor.port)/api/exec" -Method Post -Headers @{ Authorization = "Bearer $($descriptor.evalToken)" } -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 45
if (!$response.success -or !$response.result.success) { $response | ConvertTo-Json -Depth 8; throw 'Pipeline command failed. Inspect status before resuming.' }
$response.result.result
if ($Command -in @('execute', 'resume')) {
    Write-Output "Completion report: $outputFullPath (empty while running; JSON manifest or error when complete)."
}
