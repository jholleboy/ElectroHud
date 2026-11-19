# Integração com o Los Santos RED

Referência examinada: `jholleboy/lossantosred-ptbr`, branch `feature/pt-br-localization`, commit `ba4115b7c6b5dc0986c19a88fc121a9c9ea33af5`. O ElectroHud é um plugin separado e compila com o SDK do RAGE Plugin Hook. A construção não referencia a DLL do LS RED.

| Estatística | Origem | Interpretação |
| --- | --- | --- |
| Saúde | `Game.LocalPlayer.Character.Health` e `MaxHealth` | Usa os pontos acima do piso de 100 no modelo normal do jogador. Limita o valor apresentado a 0–100%. O `HealthManager` do LS RED também lê o pedestre. |
| Colete | `Ped.Armor` e `GET_PLAYER_MAX_ARMOUR(PLAYER_ID())` via SDK oficial | Valor atual dividido pela capacidade real. O arco fica visível em 0%. |
| Fome | `EntryPoint.ModController.Player.HumanState.Hunger.CurrentValue` | Escala original de 0 a 100. Um valor alto significa necessidade atendida. |
| Sede | `Player.HumanState.Thirst.CurrentValue` | Mesma escala. |
| Sono | `Player.HumanState.Sleep.CurrentValue` | Mesma escala. |
| Estresse | Nenhuma estatística identificada no commit estudado | Oculto. Não é derivado de atenção policial, vida ou fôlego. |
| Combustível | `Player.CurrentVehicle.Vehicle.FuelLevel`, com `RequiresFuel` e `VehicleSettings.CustomFuelSystemFuelMax` | Mesma razão usada em `FuelTank.UIText`. Exige sistema próprio ativo, veículo compatível e confirmação de que é o veículo atual do pedestre. |

`ModController.ModDataFileManager.Settings.SettingsManager` fornece as opções de necessidades e combustível. O adaptador usa somente reflexão de leitura em membros públicos. Revalida o estado do controlador a cada amostra e guarda os metadados de reflexão, sem reter objetos de gameplay entre amostras. Antes de o LS RED iniciar ou após ele ser descarregado, o painel continua com saúde e colete do GTA V. A leitura opcional exige que a assembly e a instância ativa sejam visíveis no mesmo domínio gerenciado. A descoberta e o ciclo de carregamento devem ser validados na versão de RPH instalada.

## Renderização e convivência

O fiber do plugin amostra dados a cada 50 ms. O callback `RawFrameRender` desenha os arcos segmentados e a moldura sem acessar nativos, arquivos ou reflexão. Ícones, logotipo, rótulos e dígitos usam texturas carregadas na inicialização. As dimensões conservam a proporção do painel e respeitam a área segura retornada pelo GTA V. A ocultação usa o menu de pausa do jogo, sinais públicos de menus e alertas do LS RED quando disponíveis, cutscenes e fades. Outros overlays de terceiros podem não expor seu estado.

`TopRightMenu` monta fome, sede e sono no mesmo trecho que inclui efeitos de perda e recuperação de vida, condicionado por `ApplyNeeds`. Essa opção governa a simulação e deve continuar ligada. `LowerRightDisplay` inclui combustível no painel do veículo junto com velocímetro e bússola. `VehicleDisplayEnabled` oculta o painel inteiro. A vida do GTA V integra o HUD nativo. O ElectroHud deixa essas superfícies intactas. Sua posição central busca deixar espaço para minimapa e HUD lateral.

## Ponte de leitura v1 e aprovação

No teste real, a HUD apareceu apenas com saúde. O commit de referência contém os membros públicos esperados. Esse resultado indica que a leitura de necessidades e combustível falhou no ambiente do jogador. As causas incluem isolamento de domínios gerenciados e instância ou opções ainda indisponíveis. O modo Diagnostics diferencia esses casos.

O patch [LosSantosRED-ba4115b.patch](integration/LosSantosRED-ba4115b.patch) implementa o publicador em `ModController.cs`, separado do ElectroHud. Ele ainda **não foi aplicado** ao checkout nem à branch do LS RED. Após aprovação, use uma cópia de teste da RC2 e confira a aplicação antes do build. A partir da raiz dessa cópia:

```powershell
$patch = 'C:\src\ElectroHud\integration\LosSantosRED-ba4115b.patch'
git apply --check $patch
git apply $patch
```

Compile essa cópia seguindo o procedimento da RC2, instale a DLL resultante no seu ambiente de teste e inicie o LS RED. A falha de acesso ao canal não impede a execução do LS RED. A ponte publica a cada 250 ms apenas enquanto o controlador está ativo. Ela lê o veículo real do pedestre e confere o handle com o objeto rastreado antes de publicar combustível. Nenhum save, menu, indicador original ou valor de gameplay é alterado.

A ponte usa memória mapeada e um mutex de tentativa imediata sob `Local\ElectroHud.Lsr.v1.<PID>`. O canal cruza domínios do mesmo processo, não grava arquivos e não usa rede. O ElectroHud prefere um snapshot fresco da ponte e mantém a reflexão pública como alternativa. Snapshots com mais de dois segundos são descartados. O combustível também é descartado se o handle for diferente do veículo atual.

| Offset | Campo |
| --- | --- |
| 0 | Magic v1 `0x45485231` |
| 4 | Handle do veículo rastreado, ou zero |
| 8 | Timestamp monotônico de `Stopwatch.GetTimestamp()` |
| 16, 20, 24, 28 | Fome, sede, sono e combustível em float, percentuais |
| 32 | Bits de menu, sistema de combustível ativo, veículo compatível, fome, sede e sono ativados |

O publicador não inclui supressão de HUD. O verificador `git apply --check` passou no commit de referência. A ponte ainda precisa de compilação com o SDK oficial e teste no RPH.
