# ElectroHud

HUD independente para **GTA V Legacy**, RAGE Plugin Hook e .NET Framework 4.8. A V4 posiciona necessidades e a central territorial e policial no canto superior direito, abaixo da área reservada para dinheiro e estrelas. O painel inferior direito mostra a velocidade em km/h ou, com dados disponíveis do Los Santos RED, também combustível e, abaixo da velocidade, o limite cadastrado para a rua.

A interface usa moldura ciano e magenta, fundo escuro translúcido, ícones com fundo transparente e percentuais coloridos. Saúde, Colete, Fome, Sede e Sono ficam em uma única linha. A escala padrão é 40% menor que a da V2.

## Compilar e instalar

No Windows, instale o Developer Pack do .NET Framework 4.8, o Visual Studio Build Tools com MSBuild e o RAGE Plugin Hook compatível com seu GTA V Legacy. Coloque a DLL do **SDK oficial** em `<GTA V>\SDK\RagePluginHookSDK.dll`.

```powershell
.\BUILD.ps1 -GtaVDirectory 'C:\Games\Grand Theft Auto V'
.\PACKAGE.ps1
```

O build produz `bin\Release\ElectroHud.dll`. Após o build, extraia `artifacts\ElectroHud-Legacy.zip` na raiz do GTA V. O pacote instala `Plugins\ElectroHud.dll` e `Plugins\ElectroHud\` com os recursos e a configuração. Carregue `ElectroHud.dll` pelo RAGE Plugin Hook no modo história. O Los Santos RED é opcional e pode ser iniciado antes ou depois. Se o RPH isolar os plugins, use a ponte opcional descrita em [INTEGRATION.md](INTEGRATION.md). Aplique o patch V4 em uma cópia limpa da RC2 e instale também o catálogo atualizado do LS RED. O painel automotivo mantém a âncora da V3.

Use **F9** para alternar a visibilidade. Ative `<Diagnostics>true</Diagnostics>` no XML e recarregue o ElectroHud para registrar a origem ou o motivo da ausência de cada indicador. Se houver falha ao carregar recursos ou ao ler o Los Santos RED, procure `[ElectroHud]` no console e no `RagePluginHook.log`.

## Configuração

Edite `Plugins\ElectroHud\Settings.xml` e recarregue o plugin. `RightMargin`, `TopOffset` e `BottomMargin` controlam os recuos adicionais da área segura, em frações da dimensão correspondente da tela. Substitua o XML da V2 para adotar a nova posição e a escala padrão `0.75`. `ShowTerritory` e `ShowVehicle` habilitam os painéis. `SuppressReplacedLsrIndicators` controla a supressão seletiva com a ponte V4. `Opacity` vai de 0 a 1. `Cyan`, `Magenta` e as cores individuais aceitam `#RRGGBB`. Cada indicador aceita `Enabled` e `CriticalPercent`. `Animate` controla interpolação e pulsação, `HideInMenus` e `HideInScenes` controlam a ocultação, e `ToggleKey` aceita nomes de teclas de `System.Windows.Forms.Keys`, como `F9` ou `F11`.

O indicador de estresse fica oculto por falta de uma fonte verificada. Sem o Los Santos RED, o plugin apresenta a saúde, o colete, o nível de procurado nativo e a velocidade do veículo. Consulte [a integração](INTEGRATION.md) e [o roteiro de teste](TESTING.md).
