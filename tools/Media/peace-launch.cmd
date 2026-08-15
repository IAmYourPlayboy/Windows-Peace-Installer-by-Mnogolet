@echo off
rem Запуск мастера при загрузке с носителя Windows Peace.
rem
rem Носитель ищется по описи в корне раздела, а не по букве: буквы в WinPE
rem непредсказуемы, раздел данных однажды оказался под C:. Тем же признаком
rem носитель находит и сам мастер.

set PEACE=
for %%d in (C D E F G H I J K L M N O P Q R S T U V W Y Z) do (
    if exist %%d:\windows-peace-media.json set PEACE=%%d:
)

if "%PEACE%"=="" (
    echo Носитель Windows Peace не найден ни на одном разделе.
    echo Ищется файл windows-peace-media.json в корне раздела.
    cmd.exe
    exit /b 1
)

echo Носитель найден: %PEACE%
"%PEACE%\WindowsPeace\WindowsPeace.Setup.exe"

rem Обратно в командную строку, а не в перезагрузку: winpeshl считает выход
rem последнего приложения концом работы и перезагружает машину, унося экран
rem вместе с объяснением, если оно там было.
cmd.exe
