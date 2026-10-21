# Instalar el sistema en un servidor (24/7 con HTTPS)

Resultado: el sistema en **https://prospectos.tibolivia.com**, encendido todo el día, con certificado HTTPS automático
y respaldo diario de la base de datos.

## 1. Contratar el servidor (VPS)

Cualquier proveedor con **Ubuntu 24.04**, **2 GB de RAM** y 1–2 vCPU es suficiente (US$5–12 al mes). Por ejemplo:
Hetzner, DigitalOcean, Vultr, Contabo o Hostinger VPS.

Al crearlo:
- Sistema operativo: **Ubuntu 24.04 LTS**.
- Región: la más cercana (EE. UU. o São Paulo).
- Acceso: contraseña de **root** (o llave SSH).
- Anote la **IP pública** del servidor.

## 2. Apuntar el dominio

En el panel donde administra el dominio `tibolivia.com`, cree un registro DNS:

| Tipo | Nombre | Valor | TTL |
|---|---|---|---|
| A | `prospectos` | IP pública del servidor | 300 |

## 3. Crear un token de GitHub (el repositorio es privado)

1. https://github.com/settings/personal-access-tokens/new
2. **Repository access:** Only select repositories → `tibolivia-prospectos`.
3. **Permissions → Repository → Contents:** Read-only.
4. **Generate token** y cópielo.

## 4. Instalar (en el servidor)

Desde Windows, en PowerShell:

```powershell
ssh root@IP_DEL_SERVIDOR
```

Ya dentro del servidor:

```bash
apt-get update && apt-get install -y git
git clone https://github.com/wmagne/tibolivia-prospectos.git /opt/tibolivia
#   Usuario: wmagne   ·   Contraseña: pegue el token del paso 3
cd /opt/tibolivia
sudo bash deploy/instalar.sh
```

El instalador:
1. Instala Docker.
2. Pide el dominio, la contraseña del administrador, la del correo y los datos de WhatsApp (Meta o TextMeBot).
3. Genera solo la contraseña de la base de datos y el Verify Token.
4. Construye e inicia el sistema con HTTPS automático.
5. Programa un respaldo diario a las 3:00 a.m. (se guardan 14 días en `/opt/tibolivia/respaldos`).

Al final muestra la URL del sistema, la de la política de privacidad y la del webhook.

## 5. Después de instalar

1. Entre a **https://prospectos.tibolivia.com** con `admin@tibolivia.com` y su contraseña.
2. En **developers.facebook.com → su app → Configuración → Básica**, ponga como política de privacidad
   `https://prospectos.tibolivia.com/privacidad` y **publique la app**.
3. En el sistema, menú **Asistente Meta**: registrar número → perfil → crear plantillas →
   conectar webhook con `https://prospectos.tibolivia.com`.
4. Menú **WhatsApp** → enviar una prueba. Menú **Correo** → enviar una prueba.
5. Importar prospectos, activar la campaña e inscribirlos.

## Mantenimiento

| Tarea | Comando (en el servidor) |
|---|---|
| Actualizar a la última versión (hace respaldo antes) | `sudo bash /opt/tibolivia/deploy/actualizar.sh` |
| Respaldo manual | `sudo bash /opt/tibolivia/deploy/respaldar.sh` |
| Ver registros del sistema | `cd /opt/tibolivia/deploy && docker compose logs -f app` |
| Cambiar datos (correo, WhatsApp, etc.) | `nano /opt/tibolivia/deploy/.env` y luego `docker compose --env-file .env up -d` |
| Reiniciar | `cd /opt/tibolivia/deploy && docker compose restart` |

En `.env` escriba los valores entre comillas simples, por ejemplo `SMTP_PASSWORD='mi$clave'`.

Restaurar un respaldo:

```bash
cd /opt/tibolivia/deploy
gunzip -c ../respaldos/ARCHIVO.sql.gz | docker compose exec -T db psql -U tibolivia -d tibolivia_prospeccion
```
