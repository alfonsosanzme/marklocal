---
title: "MarkLocal: por qué acabé haciéndome mi propio editor de Markdown"
date: 2026-07-23
author: Alfonso Sanz López
tags: [markdown, herramientas, claude-code, windows, kairis]
description: "La historia de MarkLocal, un editor y visor Markdown local para Windows que nació de una necesidad concreta y se construyó con Claude Code."
---

# MarkLocal: por qué acabé haciéndome mi propio editor de Markdown

Desde hace unos meses me he tenido que acostumbrar a trabajar con Markdown. Al principio —hablo del principio del principio— no le veía mucho sentido. Pero poco a poco fui entendiendo la ventaja de trasladar la información de la manera correcta, en el formato más limpio y liviano posible.

Cuantos más proyectos desarrollaba, y sobre todo cuanto más organizaba en archivos de texto todo el contexto de esos proyectos, más obvio se volvía: había que pasarlo todo a Markdown. Un archivo `.md` es texto plano, se abre en cualquier sitio, no depende de ningún programa propietario y pesa lo que pesa el texto y nada más. Para gestionar contexto —el mío y el de las herramientas de IA con las que trabajo a diario— no hay nada mejor.

El problema llegó después.

## Buscar y no encontrar

Me puse a buscar por internet un editor de Markdown que me sirviera, y no encontré una solución gratuita apropiada para algo que a mí me parecía simple y fácil. Herramientas de pago, editores en la nube con su cuenta y su sincronización, plugins para IDEs enormes que abro solo para leer dos párrafos… pero nada que fuera exactamente lo que quería: **abrir un `.md`, leerlo bien, editarlo rápido y moverme con soltura por carpetas enteras de documentos**. En local. Sin dramas.

Así que decidí hacer una prueba.

## La prueba: un *one shot* con Claude

En ese momento estaba Claude Opus 4.7. Le describí mi problema, le describí cómo quería yo que fuera el archivo, el programa, cómo debía comportarse. Y ya en la primera versión que hizo, en el primer *one shot*, conseguí algo que estaba casi al nivel.

No voy a mentir: hubo que pegarse con varias iteraciones. La cosa fue cogiendo forma después de varias interacciones y de especificaciones cada vez más técnicas. He estado varios meses trabajando con una versión beta de la aplicación. Pero ahora, por fin, tengo una versión que me complace bastante.

Lo que más me sorprendió del desarrollo con Claude Code fue precisamente eso: que casi a la primera ya funcionaba. Y era la primera aplicación para Windows que hacía en mi vida. Eso me abrió mil posibilidades más —de hecho ya he hecho alguna otra cosa que ya iré contando—.

## Por qué es local

MarkLocal se hizo en local porque lo que yo quería era ejecutar los archivos que tengo en local. Todas mis sesiones de trabajo son en local, y ya luego las sincronizo con GitHub, con mi servidor por SSH o con lo que haga falta. Pero la base tenía que ser un programa rápido y fluido que me sirviera para manejar la información, sin pedir permiso a ninguna nube.

Y de ahí salieron casi todas las funciones, cada una resolviendo un problema real que yo tenía:

- **El esquema de los Markdown de la misma carpeta.** Cuando estoy viendo un archivo, quiero ver también los demás `.md` que viven a su lado, porque casi nunca trabajo con un documento suelto.
- **El modo foco y el modo de solo lectura.** Muchas veces manejo textos densos, y quería que se vieran claros, sin distracciones alrededor.
- **Abrir la carpeta en el Explorador de Windows.** A veces abro un archivo desde una herramienta de IA, pero luego quiero abrir la carpeta entera para ver la documentación de su entorno y moverme por ahí. Ese botón está justo por eso.

Poco a poco, feature a feature, la aplicación fue ganando forma.

## ¿Para quién es esto?

Mi idea es que MarkLocal sea tan simple y fácil de usar que la pueda usar cualquiera, pero que además sea de verdad útil para los que manejamos muchos archivos. Está pensada para resolver problemas y para poder seguir creciendo sin muchas complicaciones.

Por eso la publico como **código abierto** —con licencia MIT— pero dejando claro quién está detrás. La tienes en GitHub: [github.com/alfonsosanzme/marklocal](https://github.com/alfonsosanzme/marklocal). Puedes descargar el instalador o la versión portable, o compilarla tú mismo.

## Lo que viene (y lo que cuesta)

Dentro de un tiempo me gustaría que aplicaciones como esta vinieran ya integradas en Claude Code o en Codex. Y seguramente lo que acabe haciendo sea una versión online. Pero, sinceramente, esas son cosas que ahora mismo no me quiero poner a hacer.

Mientras escribo esto tengo cinco sesiones de Claude Code abiertas a la vez, y otra de Codex. La cabeza no me da para más. Hay que centrarse en un proyecto, desarrollarlo, terminarlo y pasar al siguiente —y no estar trabajando con cinco a la vez—. Es una de las cosas que más me está costando últimamente. Pero bueno, son los tiempos que corren y nos tenemos que acostumbrar.

De momento, MarkLocal ya hace lo que necesitaba que hiciera. Y si a alguien más le sirve, mejor todavía.

---

*MarkLocal es un proyecto de [Alfonso Sanz López](https://kairis.es) — [Kairis](https://kairis.es). Desarrollado con Claude Code (Anthropic). Código abierto en [GitHub](https://github.com/alfonsosanzme/marklocal).*
