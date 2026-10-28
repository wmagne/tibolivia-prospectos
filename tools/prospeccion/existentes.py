# Uso: python3 -I existentes.py <carpeta Data/Listas> <salida existentes.txt>
# Lista los nombres de todas las empresas ya cargadas, para que la búsqueda no las repita.
import csv, glob, sys
carpeta, salida = sys.argv[1:3]
nombres = []
for f in sorted(glob.glob(carpeta + '/*.csv')):
    for r in csv.DictReader(open(f, encoding='utf-8-sig')):
        if r.get('Empresa'): nombres.append(r['Empresa'].strip())
open(salida, 'w', encoding='utf-8').write('\n'.join(nombres))
print(len(nombres), 'empresas ya cargadas')
