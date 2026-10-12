# Árbol del front (fixture)

## src/

```
src/
├── shared/
│   ├── format.ts                                     formato de montos
│   └── ghost.ts                                      nunca existió
└── areas/
    └── business/
        └── roles/
            └── api/
                └── roles.ts                          funciones de la API de roles
```

## Mapa de carpetas → punteros


| Carpeta | Qué va / qué no | Fichas | Copiá de |
|---|---|---|---|
| `src/shared/` | lo común a todas las áreas; nada de una pantalla | formatos | `src/shared/format.ts` |
| `src/areas/<área>/<feature>/api/` | funciones tipadas con la API; sin interfaces a mano | formatos | `business/roles/api/roles.ts` (E4) |
| `src/futuro/` | carpeta que todavía no existe | formatos | — |
