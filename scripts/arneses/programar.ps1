<#
.SYNOPSIS
  Alta, baja y consulta de la tarea programada de un arnés ejecutable (Programador de tareas de Windows).
.DESCRIPTION
  La tarea corre con tu usuario, solo con la sesión iniciada, y deja el resultado en una rama arnes/<arnés>/<fecha>.
  Nunca hace push. El informe queda en .local/arneses/<arnés>/.
.EXAMPLE
  pwsh scripts/arneses/programar.ps1 -Arnes realinear-docs -Motor claude -Hora 03:00 -Dias Lun,Jue
  pwsh scripts/arneses/programar.ps1 -Arnes realinear-docs -Consulta
  pwsh scripts/arneses/programar.ps1 -Arnes realinear-docs -Baja
#>
[CmdletBinding(SupportsShouldProcess)]
param(
  [Parameter(Mandatory)][string]$Arnes,
  [ValidateSet('claude', 'codex')][string]$Motor = 'claude',
  [ValidatePattern('^\d{1,2}:\d{2}$')][string]$Hora = '03:00',
  [string[]]$Dias = @('Lun', 'Jue'),
  [string]$Raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
  [switch]$Baja,
  [switch]$Consulta
)

$ErrorActionPreference = 'Stop'
$nombreTarea = "Arnes-$(Split-Path $Raiz -Leaf)-$Arnes"

$mapaDias = @{
  lun = 'Monday'; mar = 'Tuesday'; mie = 'Wednesday'; jue = 'Thursday'; vie = 'Friday'; sab = 'Saturday'; dom = 'Sunday'
  mon = 'Monday'; tue = 'Tuesday'; wed = 'Wednesday'; thu = 'Thursday'; fri = 'Friday'; sat = 'Saturday'; sun = 'Sunday'
}

function ConvertirDias([string[]]$lista) {
  foreach ($d in $lista) {
    $clave = $d.ToLowerInvariant().Substring(0, [Math]::Min(3, $d.Length)) -replace 'é', 'e'
    if (-not $mapaDias.ContainsKey($clave)) { throw "Día no reconocido: '$d'. Usá Lun, Mar, Mie, Jue, Vie, Sab o Dom." }
    $mapaDias[$clave]
  }
}

if ($Consulta) {
  $t = Get-ScheduledTask -TaskName $nombreTarea -ErrorAction SilentlyContinue
  if (-not $t) { Write-Output "No hay tarea programada '$nombreTarea'."; return }
  $info = Get-ScheduledTaskInfo -TaskName $nombreTarea
  Write-Output "Tarea: $nombreTarea · estado $($t.State) · próxima corrida $($info.NextRunTime) · último resultado $($info.LastTaskResult)"
  $t.Actions | ForEach-Object { Write-Output "Comando: $($_.Execute) $($_.Arguments) (en $($_.WorkingDirectory))" }
  return
}

if ($Baja) {
  if (-not (Get-ScheduledTask -TaskName $nombreTarea -ErrorAction SilentlyContinue)) { Write-Output "No hay tarea programada '$nombreTarea'."; return }
  if ($PSCmdlet.ShouldProcess($nombreTarea, 'Dar de baja la tarea programada')) {
    Unregister-ScheduledTask -TaskName $nombreTarea -Confirm:$false
    Write-Output "Tarea '$nombreTarea' dada de baja."
  }
  return
}

$node = (Get-Command node -ErrorAction Stop).Source
$script = Join-Path $Raiz 'scripts\arneses\arnes.mjs'
if (-not (Test-Path $script)) { throw "No existe $script" }

$accion = New-ScheduledTaskAction -Execute $node -Argument "scripts\arneses\arnes.mjs $Arnes --motor $Motor" -WorkingDirectory $Raiz
$disparador = New-ScheduledTaskTrigger -Weekly -DaysOfWeek (ConvertirDias $Dias) -At $Hora
$ajustes = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) -AllowStartIfOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive

if ($PSCmdlet.ShouldProcess($nombreTarea, "Dar de alta: $($Dias -join ', ') a las $Hora con el motor $Motor")) {
  Register-ScheduledTask -TaskName $nombreTarea -Action $accion -Trigger $disparador -Settings $ajustes -Principal $principal -Description "Arnés ejecutable $Arnes ($Motor). Deja una rama arnes/$Arnes/<fecha>; no hace push." -Force | Out-Null
  Write-Output "Tarea '$nombreTarea' dada de alta: $($Dias -join ', ') a las $Hora con el motor $Motor."
}
