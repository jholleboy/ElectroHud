# ElectroHud

HUD independente para **GTA V Legacy**, RAGE Plugin Hook e .NET Framework 4.8. O painel inferior central mostra saúde e, quando os dados do Los Santos RED estão disponíveis, fome, sede, sono e combustível. As barras circulares respondem aos valores reais. O painel recolhe os espaços dos indicadores indisponíveis.

A interface usa moldura ciano e magenta, fundo escuro translúcido, ícones originais com fundo transparente e percentuais coloridos. A imagem de referência não apareceu nos anexos disponíveis. O visual segue a descrição do projeto.

## Compilar e instalar

No Windows, instale o Developer Pack do .NET Framework 4.8, o Visual Studio Build Tools com MSBuild e o RAGE Plugin Hook compatível com seu GTA V Legacy. Coloque a DLL do **SDK oficial** em `<GTA V>\SDK\RagePluginHookSDK.dll`.

```powershell
.\BUILD.ps1 -GtaVDirectory 'C:\Games\Grand Theft Auto V'
.\PACKAGE.ps1
```

O build produz `bin\Release\ElectroHud.dll`. Após o build, extraia `artifacts\ElectroHud-Legacy.zip` na raiz do GTA V. O pacote instala `Plugins\ElectroHud.dll` e `Plugins\ElectroHud\` com os recursos e a configuração. Carregue `ElectroHud.dll` pelo RAGE Plugin Hook no modo história. O Los Santos RED é opcional e pode ser iniciado antes ou depois.

Use **F9** para alternar a visibilidade. Valores e diagnóstico do plugin ficam disponíveis durante a execução. Se houver falha ao carregar recursos ou ao ler o Los Santos RED, procure `[ElectroHud]` no console e no `RagePluginHook.log`.

## Configuração

Edite `Plugins\ElectroHud\Settings.xml` e recarregue o plugin. `X` e `Y` são as coordenadas normalizadas do centro do painel. `Scale` ajusta o tamanho com a resolução, e `Opacity` vai de 0 a 1. `Cyan`, `Magenta` e as cores individuais aceitam `#RRGGBB`. Cada indicador aceita `Enabled` e `CriticalPercent`. `Animate` controla interpolação e pulsação, `HideInMenus` e `HideInScenes` controlam a ocultação, e `ToggleKey` aceita nomes de teclas de `System.Windows.Forms.Keys`, como `F9` ou `F11`.

O indicador de estresse fica oculto por falta de uma fonte verificada. Sem o Los Santos RED, o plugin apresenta a saúde do GTA V. Consulte [a integração](INTEGRATION.md) e [o roteiro de teste](TESTING.md).
