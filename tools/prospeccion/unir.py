# Uso: python3 -I unir.py <carpeta_ronda> <salida.csv> <existentes.txt>
import csv,glob,re,sys,unicodedata,collections
carpeta,salida,exist_f=sys.argv[1:4]
H="Empresa,Contacto,Cargo,Email,Telefono,Rubro,Ciudad,SitioWeb,ConsentimientoWhatsApp,Prioridad,Direccion,Necesidad,Fuente,Notas,Verificar".split(',')
RUB={"Salud","Educación","Inmobiliaria y construcción","Distribución, logística y agro","Logística y transporte","Gastronomía, hotelería y servicios","Farmacias","Comercio minorista","Servicios empresariales","Automotriz","Industria"}
k=lambda s: re.sub(r'(srl|sa|ltda|bolivia)$','',''.join(c for c in unicodedata.normalize('NFD',s.lower()) if c.isalnum()))
exist={k(l) for l in open(exist_f,encoding='utf-8') if l.strip()}
vistos=set();emails=set();out=[];prob=collections.Counter()
for f in sorted(glob.glob(carpeta+'/*.csv')):
    try: filas=list(csv.DictReader(open(f,encoding='utf-8-sig')))
    except Exception as ex: prob['archivo ilegible']+=1; continue
    for r in filas:
        r={h:(r.get(h) or '').strip() for h in H}
        if not r['Empresa']: continue
        key=k(r['Empresa'])
        if key in vistos or key in exist: prob['duplicada']+=1; continue
        e=re.split(r'[;,/\s]',r['Email'].lower())[0].strip()
        if e and not re.fullmatch(r'[^@\s]+@[^@\s]+\.[a-z]{2,}',e): prob['email inválido']+=1; e=''
        if e and e in emails: prob['email repetido']+=1; e=''
        r['Email']=e
        t=re.sub(r'[^\d+]','',re.split(r'[/;,]',r['Telefono'])[0])
        if t and not t.startswith('+'): t='+591'+t.lstrip('0')
        r['Telefono']=t if len(t)>=11 else ''
        if not r['Email'] and not r['Telefono']: prob['sin contacto']+=1; continue
        if r['Rubro'] not in RUB: prob['rubro corregido']+=1; r['Rubro']='Servicios empresariales'
        r['ConsentimientoWhatsApp']='no'; r['Verificar']='si'
        if r['Prioridad'] not in ('A','B','C'): r['Prioridad']='B'
        vistos.add(key)
        if e: emails.add(e)
        out.append(r)
with open(salida,'w',encoding='utf-8-sig',newline='') as fo:
    w=csv.DictWriter(fo,H); w.writeheader(); w.writerows(out)
with open(exist_f,'a',encoding='utf-8') as fe: fe.write('\n'+'\n'.join(r['Empresa'] for r in out))
print('total',len(out),'con email',sum(1 for r in out if r['Email']),dict(prob))
print(dict(collections.Counter(r['Rubro'] for r in out)))
