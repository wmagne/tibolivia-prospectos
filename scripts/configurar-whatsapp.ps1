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
Write-Host "=== Configuracion de WhatsApp ===" -ForegroundColor Cyan
Write-Host "Deje un dato vacio (Enter) para no cambiarlo. Los datos ocultos no se ven mientras escribe."
Write-Host ""
Write-Host "Proveedor de WhatsApp:"
Write-Host "  1 = Meta (oficial, recomendado para prospectos)"
Write-Host "  2 = TextMeBot (no oficial, riesgo de bloqueo del numero)"
$opcion = Leer "Elija 1 o 2"

if ($opcion -eq "2") {
    Guardar "WhatsApp:Proveedor" "TextMeBot"
    Write-Host ""
    Write-Host "La API Key esta en el correo de TextMeBot o en su panel." -ForegroundColor Yellow
    Guardar "WhatsApp:TextMeBot:ApiKey" (Leer "API Key de TextMeBot (oculta)" $true)
    $token = Leer "Token para el webhook de respuestas (Enter = generar uno)"
    if ([string]::IsNullOrWhiteSpace($token)) { $token = [guid]::NewGuid().ToString("N").Substring(0, 16) }
    Guardar "WhatsApp:TextMeBot:TokenWebhook" $token
    Guardar "Chatbot:Url" (Leer "URL del chatbot (opcional, Enter para omitir)")
    Write-Host ""
    Write-Host "Listo. URL para recibir respuestas (configurela en el panel de TextMeBot):" -ForegroundColor Cyan
    Write-Host "  https://SU-DOMINIO/webhooks/textmebot?token=$token"
    Write-Host "Ahora ejecute 'dotnet run' y entre al menu WhatsApp para enviar un mensaje de prueba."
    exit
}

Guardar "WhatsApp:Proveedor" "Meta"
Write-Host ""
Write-Host "Donde encontrar cada dato en developers.facebook.com -> su app:" -ForegroundColor Yellow
Write-Host "  - Phone Number ID y WhatsApp Business Account ID: WhatsApp -> Configuracion de la API"
Write-Host "  - Access Token permanente: business.facebook.com -> Usuarios del sistema -> Generar token"
Write-Host "  - App Secret: Configuracion de la app -> Basica -> Clave secreta"
Write-Host ""
Guardar "WhatsApp:PhoneNumberId"     (Leer "Phone Number ID")
Guardar "WhatsApp:BusinessAccountId" (Leer "WhatsApp Business Account ID")
Guardar "WhatsApp:AccessToken"       (Leer "Access Token permanente (oculto)" $true)
Guardar "WhatsApp:AppId"             (Leer "App ID (Configuracion de la app -> Basica)")
Guardar "WhatsApp:AppSecret"         (Leer "App Secret (oculto)" $true)

$verify = Leer "Verify Token para el webhook (Enter = generar uno)"
if ([string]::IsNullOrWhiteSpace($verify)) { $verify = "tibolivia-" + [guid]::NewGuid().ToString("N").Substring(0, 12) }
Guardar "WhatsApp:VerifyToken" $verify

$chatbot = Leer "URL del chatbot (opcional, Enter para omitir)"
Guardar "Chatbot:Url" $chatbot

Write-Host ""
Write-Host "Listo. Anote su Verify Token para configurarlo en Meta: $verify" -ForegroundColor Cyan
Write-Host "Ahora ejecute 'dotnet run' y entre al menu WhatsApp para verificar la conexion."
