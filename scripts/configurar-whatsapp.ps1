# Asistente para configurar WhatsApp (Meta) en el Sistema de Prospección TI Bolivia.
# Guarda los datos con "dotnet user-secrets": quedan fuera del proyecto y nunca se suben a GitHub.
# Uso (en PowerShell):  D:\Waldo\TIBolivia\SistemaProspectos\scripts\configurar-whatsapp.ps1

$ErrorActionPreference = "Stop"
$proyecto = Join-Path $PSScriptRoot "..\src\TiBolivia.Prospeccion"
Set-Location $proyecto

function Leer([string]$texto, [bool]$secreto = $false) {
    if ($secreto) {
        $s = Read-Host -Prompt $texto -AsSecureString
        return [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)).Trim()
    }
    return (Read-Host -Prompt $texto).Trim()
}

function Guardar([string]$clave, [string]$valor) {
    if ([string]::IsNullOrWhiteSpace($valor)) { Write-Host "  (se mantiene el valor anterior de $clave)" -ForegroundColor DarkGray; return }
    dotnet user-secrets set $clave $valor | Out-Null
    Write-Host "  OK $clave" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Configuracion de WhatsApp (Meta) ===" -ForegroundColor Cyan
Write-Host "Deje un dato vacio (Enter) para no cambiarlo. Los datos ocultos no se ven mientras escribe."
Write-Host ""
Write-Host "Donde encontrar cada dato en developers.facebook.com -> su app:" -ForegroundColor Yellow
Write-Host "  - Phone Number ID y WhatsApp Business Account ID: WhatsApp -> Configuracion de la API"
Write-Host "  - Access Token permanente: business.facebook.com -> Usuarios del sistema -> Generar token"
Write-Host "  - App Secret: Configuracion de la app -> Basica -> Clave secreta"
Write-Host ""

Guardar "WhatsApp:PhoneNumberId"     (Leer "Phone Number ID")
Guardar "WhatsApp:BusinessAccountId" (Leer "WhatsApp Business Account ID")
Guardar "WhatsApp:AccessToken"       (Leer "Access Token permanente (oculto)" $true)
Guardar "WhatsApp:AppSecret"         (Leer "App Secret (oculto)" $true)

$verify = Leer "Verify Token para el webhook (Enter = generar uno)"
if ([string]::IsNullOrWhiteSpace($verify)) { $verify = "tibolivia-" + [guid]::NewGuid().ToString("N").Substring(0, 12) }
Guardar "WhatsApp:VerifyToken" $verify

$chatbot = Leer "URL del chatbot (opcional, Enter para omitir)"
Guardar "Chatbot:Url" $chatbot

Write-Host ""
Write-Host "Listo. Anote su Verify Token para configurarlo en Meta: $verify" -ForegroundColor Cyan
Write-Host "Ahora ejecute 'dotnet run' y entre al menu WhatsApp para verificar la conexion."
