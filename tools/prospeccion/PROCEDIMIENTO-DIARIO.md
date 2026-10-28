# Búsqueda diaria de prospectos (tarea programada)

Objetivo: encontrar empresas nuevas de Bolivia con contacto público, cargarlas al sistema y publicarlas.
Las campañas las inscriben solas (Campanas:AutoInscripcion en appsettings.json):
con correo → "Publicidad por rubro (correo)"; con celular → "WhatsApp por rubro (envío manual)"; solo fijo → "Llamadas por rubro".

Repositorio: wmagne/tibolivia-prospectos · rama de trabajo: claude/dazzling-meitner-6s8v1b · se publica en main.
Trabaje en una carpeta temporal (TMP) fuera del repositorio para los CSV intermedios.

1. Actualizar el repositorio (rama de trabajo) y fusionar main si hiciera falta.
2. `python3 -I tools/prospeccion/existentes.py src/TiBolivia.Prospeccion/Data/Listas TMP/existentes.txt`
3. Leer `tools/prospeccion/estado.txt` (índice N) y tomar 6 segmentos de `segmentos.txt` (líneas sin #) desde N, en forma circular.
4. Lanzar 6 subagentes en paralelo (uno por segmento). Prompt de cada uno: "Lee y sigue tools/prospeccion/BRIEF.md
   (ruta absoluta del repositorio). El archivo existentes.txt está en TMP/existentes.txt. TU SEGMENTO: <segmento>.
   Archivo de salida: TMP/ronda/NN.csv". Presupuesto: 30 búsquedas por agente (el límite de 200 es compartido).
5. Unir: `python3 -I tools/prospeccion/unir.py TMP/ronda TMP/unido.csv TMP/existentes.txt`
   Si salieron 0 filas, no publicar; solo avanzar estado.txt.
6. Copiar a `src/TiBolivia.Prospeccion/Data/Listas/web-diaria-AAAA-MM-DD.csv` (el sistema la carga sola al reiniciar, una sola vez).
7. Guardar en estado.txt el nuevo índice (N+6, circular). `dotnet build -c Release` debe compilar.
8. Commit ("Búsqueda diaria de prospectos AAAA-MM-DD: N empresas") con los trailers de atribución de la sesión,
   push a la rama de trabajo y a main.
9. Publicar: el push a main de un archivo en Data/Listas lanza solo el workflow `desplegar.yml` (instalar).
   Si tiene herramientas de GitHub (mcp__github__), espere a que termine bien.
10. Si puede, verificar con accion=diagnostico (sección "Prospectos por origen" debe mostrar la lista nueva).
11. Informar en español: empresas nuevas, cuántas con correo/celular/fijo, rubros y ciudades, y el estado de las campañas.

Reglas: solo datos públicos de empresas (nunca inventar correos ni recoger datos personales), sin duplicados,
no enviar nada a ninguna empresa desde la búsqueda: los envíos los hacen las campañas del sistema.
