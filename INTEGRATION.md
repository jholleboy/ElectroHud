# Integração com o Los Santos RED

Referência examinada: `jholleboy/lossantosred-ptbr`, branch `feature/pt-br-localization`, commit `ba4115b7c6b5dc0986c19a88fc121a9c9ea33af5`. O ElectroHud é um plugin separado e compila com o SDK do RAGE Plugin Hook. A construção não referencia a DLL do LS RED.

| Estatística | Origem | Interpretação |
| --- | --- | --- |
| Saúde | `Game.LocalPlayer.Character.Health` e `MaxHealth` | Usa os pontos acima do piso de 100 no modelo normal do jogador. Limita o valor apresentado a 0–100%. O `HealthManager` do LS RED também lê o pedestre. |
| Fome | `EntryPoint.ModController.Player.HumanState.Hunger.CurrentValue` | Escala original de 0 a 100. Um valor alto significa necessidade atendida. |
| Sede | `Player.HumanState.Thirst.CurrentValue` | Mesma escala. |
| Sono | `Player.HumanState.Sleep.CurrentValue` | Mesma escala. |
| Estresse | Nenhuma estatística identificada no commit estudado | Oculto. Não é derivado de atenção policial, vida ou fôlego. |
| Combustível | `Player.CurrentVehicle.Vehicle.FuelLevel`, com `RequiresFuel` e `VehicleSettings.CustomFuelSystemFuelMax` | Mesma razão usada em `FuelTank.UIText`. Exige sistema próprio ativo, veículo compatível e confirmação de que é o veículo atual do pedestre. |

`ModController.ModDataFileManager.Settings.SettingsManager` fornece as opções de necessidades e combustível. O adaptador usa somente reflexão de leitura em membros públicos. Revalida o estado do controlador a cada amostra e guarda os metadados de reflexão, sem reter objetos de gameplay entre amostras. Antes de o LS RED iniciar ou após ele ser descarregado, o painel continua com a saúde do GTA V. A leitura opcional exige que a assembly e a instância ativa sejam visíveis no mesmo domínio gerenciado. A descoberta e o ciclo de carregamento devem ser validados na versão de RPH instalada.

## Renderização e convivência

O fiber do plugin amostra dados a cada 50 ms. O callback `RawFrameRender` desenha os arcos segmentados e a moldura sem acessar nativos, arquivos ou reflexão. Ícones, logotipo, rótulos e dígitos usam texturas carregadas na inicialização. As dimensões conservam a proporção do painel e respeitam a área segura retornada pelo GTA V. A ocultação usa o menu de pausa do jogo, sinais públicos de menus e alertas do LS RED quando disponíveis, cutscenes e fades. Outros overlays de terceiros podem não expor seu estado.

`TopRightMenu` monta fome, sede e sono no mesmo trecho que inclui efeitos de perda e recuperação de vida, condicionado por `ApplyNeeds`. Essa opção governa a simulação e deve continuar ligada. `LowerRightDisplay` inclui combustível no painel do veículo junto com velocímetro e bússola. `VehicleDisplayEnabled` oculta o painel inteiro. A vida do GTA V integra o HUD nativo. O ElectroHud deixa essas superfícies intactas. Sua posição central busca deixar espaço para minimapa e HUD lateral.

## Ponte opcional para uma etapa futura

A leitura oportunista do MVP funciona quando o controlador público está acessível. Para um contrato estável entre versões ou domínios, a proposta é uma ponte **após aprovação**:

- Publicar um instantâneo versionado só de leitura com necessidades em porcentagem, respectivos sinalizadores de ativação, combustível e estado de menu. Se houver isolamento de domínios, publicar por um canal local com atualização e expiração explícitas.
- Definir um sinalizador de apresentação por indicador, ativo apenas enquanto o consumidor estiver presente. No LS RED, aplicá-lo antes de desenhar cada necessidade e o trecho de combustível, mantendo avisos de vida, velocidade, navegação, menus e notificações.
- Manter o HUD nativo do GTA V e tratar saúde como sobreposição até existir uma maneira validada de retirar só o componente pretendido.

Nenhuma ponte ou mudança no LS RED foi aplicada.
