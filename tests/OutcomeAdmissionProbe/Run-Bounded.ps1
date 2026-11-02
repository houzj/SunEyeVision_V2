[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppPath, [Parameter(Mandatory)][string]$LogPath, [int]$TimeoutSeconds=60)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($AppPath)
$log=[IO.Path]::GetFullPath($LogPath)
[IO.Directory]::CreateDirectory($log) | Out-Null
$exe=Join-Path $root 'OutcomeAdmissionProbe.exe'
$start=Start-Process -FilePath $exe -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $log 'stdout.log') -RedirectStandardError (Join-Path $log 'stderr.log')
$completed=$start.WaitForExit($TimeoutSeconds*1000)
if(!$completed) { $start.Kill(); $start.WaitForExit() }
$result=[ordered]@{ PID=$start.Id; Payload=$root; ExitCode=$start.ExitCode; TimedOut=(!$completed); TimeoutSeconds=$TimeoutSeconds }
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $log 'process.json') -Encoding utf8
if(!$completed -or $start.ExitCode -ne 0) { throw "所有权探针失败或超时：PID $($start.Id)，退出码 $($start.ExitCode)，日志 $log" }
