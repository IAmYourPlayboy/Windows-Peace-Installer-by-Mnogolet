<#
.SYNOPSIS
    Создаёт виртуалку стенда для проверки мастера в WinPE.

.DESCRIPTION
    Второе поколение — потому что нам нужна прошивка UEFI, а не BIOS.
    Четыре гигабайта памяти — потому что WinPE загружает весь boot.wim
    в оперативную память целиком: 680 МБ образа, полгигабайта оперативного
    диска и среда выполнения с окном сверху.

    Безопасная загрузка включена намеренно: на настоящей машине она включена,
    и проверка с выключенной была бы мягче действительности.

    Сеть отключена: на шаге Б она не нужна, а лишнее устройство — лишние
    секунды на загрузке.

    Второй диск — пустая цель для установки. Без него в стенде виден один
    диск, сам загрузочный носитель, а установка на него запрещена по замыслу:
    дальше экрана выбора диска круг не проходит и проверять там нечего.

.EXAMPLE
    powershell -File tools/Stand/New-PeaceVm.ps1 -VhdxPath D:\WindowsPeace-Stand\peace.vhdx
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows Peace Stand',
    [Parameter(Mandatory = $true)] [string] $VhdxPath,
    [uint64] $MemoryBytes = 4GB,

    # Пустой диск, на который мастер мог бы ставить. Создаётся, если его ещё нет,
    # и дальше живёт сам: стирать его каждый круг нельзя — на шаге В там окажется
    # установленная система, ради которой круг и делался. Пустая строка — без цели.
    [string] $TargetVhdxPath = '',
    [uint64] $TargetSizeBytes = 64GB
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Нужны права администратора: Hyper-V без них не отвечает.'
}

if (-not (Test-Path $VhdxPath)) {
    throw "Виртуального диска '$VhdxPath' нет. Сначала собери носитель через Build-PeaceMedia.ps1."
}

if (Get-VM -Name $Name -ErrorAction SilentlyContinue) {
    Write-Host "Прежняя виртуалка '$Name' удаляется."
    Stop-VM -Name $Name -TurnOff -Force -ErrorAction SilentlyContinue
    Remove-VM -Name $Name -Force
}

New-VM -Name $Name -Generation 2 -MemoryStartupBytes $MemoryBytes -VHDPath $VhdxPath | Out-Null

# Снимки состояния выключаются намеренно. Иначе Hyper-V подкладывает поверх
# носителя «peace_{номер}.avhdx», и наш файл становится его родителем: круг
# перестаёт видеть, что носитель занят, а записанное в госте оседает в снимке,
# а не на носителе. Стенду снимки не нужны — он и так собирается заново.
Set-VM -Name $Name -AutomaticCheckpointsEnabled $false

Set-VMProcessor -VMName $Name -Count 2
Set-VMMemory -VMName $Name -DynamicMemoryEnabled $false
Get-VMNetworkAdapter -VMName $Name | Remove-VMNetworkAdapter -ErrorAction SilentlyContinue
Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate 'MicrosoftWindows'

# Порядок важен: пока диск один, «грузиться с жёсткого диска» значит «с носителя».
# Цель подключается после — иначе выбирать первое устройство пришлось бы из двух.
Set-VMFirmware -VMName $Name -FirstBootDevice (Get-VMHardDiskDrive -VMName $Name)

$target = ''
if ($TargetVhdxPath) {
    if (-not (Test-Path $TargetVhdxPath)) {
        New-VHD -Path $TargetVhdxPath -SizeBytes $TargetSizeBytes -Dynamic | Out-Null

        # Разметка ставится здесь, а не в госте: неразмеченный диск приходит
        # в систему отключённым, а отключённый диск мастер выбрать не даёт —
        # и правильно делает. Пустой размеченный диск ведёт себя как новый
        # диск в настоящей машине.
        try {
            $disk = Mount-VHD -Path $TargetVhdxPath -Passthru | Get-Disk
            try {
                Initialize-Disk -Number $disk.Number -PartitionStyle GPT | Out-Null
            }
            finally {
                Dismount-VHD -Path $TargetVhdxPath
            }
        }
        catch {
            # Недоделанную цель нельзя оставлять: в следующий раз её примут
            # за готовую, а неразмеченный диск приходит в гостя отключённым.
            Remove-Item $TargetVhdxPath -Force -ErrorAction SilentlyContinue
            throw
        }

        $target = ", цель {0:N0} ГБ создана" -f ($TargetSizeBytes / 1GB)
    }
    else {
        $target = ', цель подключена'
    }

    Add-VMHardDiskDrive -VMName $Name -Path $TargetVhdxPath
}

Write-Host ("Виртуалка '{0}' создана: 2 ядра, {1:N0} ГБ, безопасная загрузка включена{2}." -f $Name, ($MemoryBytes / 1GB), $target) -ForegroundColor Green
