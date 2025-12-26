# PrintControl

Sistema leve para monitorar impressoes em ambiente Windows sem servidor de impressao.

## Componentes
- **Agent**: servico Windows nas estacoes, le eventos de impressao e envia para o host.
- **Host**: API + interface web, armazena em SQLite e oferece filtros, metricas e exportacao CSV.

## O que registra
- Usuario, maquina, impressora, data/hora, paginas e tamanho em bytes.
- Nome do documento e opcional (desativado por padrao).

## Funcionalidades
- Filtros por data, usuario, maquina, impressora e paginas.
- Graficos simples por usuario e impressora.
- Exportacao CSV (abre no Excel).

## Observacoes
- O log `Microsoft-Windows-PrintService/Operational` precisa estar habilitado. O agente tenta habilitar automaticamente.
- Alguns drivers nao informam paginas. Nesse caso, o campo fica como 0.

## Proximos passos
Veja `docs\DEPLOYMENT.md` para publicacao e instalacao como servico.
