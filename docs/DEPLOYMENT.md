# Publicacao e instalacao

## Pre-requisitos
- Windows 10/11 ou Windows Server.
- .NET 8 SDK (para compilar) ou Runtime (para rodar).

## Host (maquina ADM)
1) Publicar:
```
dotnet publish src\Host\PrintControl.Host.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
2) Copiar a pasta de publicacao para, por exemplo, `C:\PrintControl\Host`.
3) Ajustar `appsettings.json`:
- `ListenUrls`: porta/host para acesso, ex `http://0.0.0.0:5080`
- `DatabasePath`: caminho do SQLite
- `ApiKey`: opcional (recomendado quando o host estiver acessivel na rede)
4) Instalar como servico:
```
sc.exe create PrintControl.Host binPath= "C:\PrintControl\Host\PrintControl.Host.exe" start= auto
sc.exe start PrintControl.Host
```
5) Liberar a porta no firewall se necessario (ex: 5080).

## Agent (estacoes)
1) Publicar:
```
dotnet publish src\Agent\PrintControl.Agent.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
2) Copiar a pasta para `C:\PrintControl\Agent` em cada maquina.
3) Ajustar `appsettings.json`:
- `HostUrl`: URL do host (ex: `http://ADM-PC:5080`)
- `ApiKey`: igual ao host, se configurado
- `IncludeDocumentName`: false por padrao
4) Instalar como servico:
```
sc.exe create PrintControl.Agent binPath= "C:\PrintControl\Agent\PrintControl.Agent.exe" start= auto
sc.exe start PrintControl.Agent
```

## GPO (sugestao)
- Criar um GPO que copie os binarios do Agent para `C:\PrintControl\Agent`.
- Criar uma tarefa de inicializacao com os comandos `sc.exe create` e `sc.exe start`.

## Acesso
- Abrir o navegador em `http://ADM-PC:5080`.
- Exportacao CSV fica disponivel no botao "Exportar CSV".
