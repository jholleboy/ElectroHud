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

`ModController.ModDataFileManager.Settings.SettingsManager` fornece as opções de necessidades e combustível. O adaptador usa somente reflexão de leitura em membros públicos. Revalida o estado do controlador a cada amostra e guarda os metadados de reflexão, sem reter objetos de gameplay entre amostras. Antes de o LS RED iniciar ou após ele ser descarregado, o painel continua com os dados nativos do GTA V. A leitura opcional exige que a assembly e a instância ativa sejam visíveis no mesmo domínio gerenciado. A descoberta e o ciclo de carregamento devem ser validados na versão de RPH instalada.

## Central territorial e policial

| Informação | Fonte na RC2 |
| --- | --- |
| Região, rua, polícia e gangue | `LowerRightDisplay.GetZoneDisplay()` e `GetStreetDisplay(true)` preservam os detalhes exibidos pela HUD original, incluindo endereço, cruzamento, jurisdição adicional e reputação. As atribuições `CurrentZone.AssignedLEAgency` e `AssignedGang` completam o painel quando disponíveis. |
| Nível e busca | `Player.WantedLevel` e `Player.IsInSearchMode`. Sem LS RED, o nível vem de `GET_PLAYER_WANTED_LEVEL`. |
| Perseguição | Mesmas condições do `LowerRightDisplay` para alerta, tentativa de localização, fogo liberado, força letal, invasão da posição e perseguição ativa. Preserva a contagem de policiais quando configurada. |
| Reconhecimento e contexto do veículo | `CurrentVehicle.HasBeenSeenByPoliceDuringWanted`, com as mesmas prioridades da HUD original para veículo roubado e placa fictícia. `hud.vehicleKnownToPolice` tem fallback em inglês e tradução no catálogo central da RC2. A mesma composição publica roubo, placa falsa/procurada, nome do veículo, motor desligado, transmissão, setas, direção e alerta de trânsito. |

São ocultados apenas os textos completos confirmados pelo ElectroHud. Textos muito longos mantêm a versão original se o painel os truncar. Permanecem na HUD original as mensagens de investigação, BOLO/APB, alertas de crimes e demais avisos sem equivalente. A V5 confirma independentemente o bloco veicular, a perseguição, a rua e a zona. Os blocos reutilizam a ordem, as condições e os textos de origem da RC2. Scanner, menus, notificações e estrelas continuam funcionando de forma independente.

## Limite de velocidade

`LowerRightDisplay.GetVehicleStatusDisplay()` formata a velocidade atual e `CurrentStreet.SpeedLimitMPH` entre parênteses, sem verificar a origem. Em `LocationData.UpdateStreets()`, a RC2 procura a rua pelo nome fornecido pelo jogo. Se não encontrar, cria `new Street(CurrentStreetName, 60f, "MPH")`. Alguns registros reais da RC2, como Great Ocean Hwy, também têm 60 mph. Só o número da captura é insuficiente para saber de qual caso se trata.

A V3 já exigia que a rua fosse a própria entrada do cadastro. Também exigia o mesmo handle de veículo, uma classe nativa rodoviária e um snapshot válido. Por isso a HUD original podia mostrar 60 mph mesmo com o limite oculto no ElectroHud. A V5 usa a classificação de automóvel/motocicleta da própria RC2 e registra o motivo da ausência com `Diagnostics`. Se a entrada estiver confirmada, o valor original em mph é multiplicado por **1,609344**. Um limite cadastrado de 60 mph resulta em `Limite: 97 km/h`. O valor padrão continua restrito à HUD original quando ela está configurada para mostrá-lo.

## Aplicar a ponte V5

Use uma **cópia limpa de `ba4115b`** da RC2 PT-BR. A V5 incorpora a V4 e deve ser aplicada diretamente à RC2 limpa. Na raiz da cópia:

```powershell
$patch = 'C:\src\ElectroHud\integration\LosSantosRED-ba4115b-v5.patch'
git apply --check $patch
if ($LASTEXITCODE -ne 0) { throw 'A cópia do LS RED deve estar limpa no commit ba4115b.' }
git apply $patch
```

Compile a cópia pelo procedimento da RC2 e instale a DLL e o catálogo atualizado no ambiente de teste. O ElectroHud V5 usa a ponte V5 ou a reflexão pública quando disponível.

## Inventário da HUD RC2

| Superfície | Origem e condições da RC2 | Tratamento V5 |
| --- | --- | --- |
| Endereço, rua, cruzamento e interiores | `UI.Tick1 → LowerRightDisplay → GetStreetDisplay`. Considera interior, via rápida e modalidade com número do endereço. | Central. Reutiliza o texto completo, com quebra de linha. |
| Região, cidade, estado, condado e território | `GetZoneDisplay → Zone.GetFullLocationName`. Depende das opções de zona, dados de jurisdição, gangue, reputação e disputa. | Central. Exibe o bloco original e completa atribuições quando disponíveis. |
| Situação de procurado e perseguição | `LowerRightDisplay.GetPlayerDisplay`, `PoliceResponse`, `IsInSearchMode`, `WantedLevel`. Contagem conforme a opção original. | Central. Suprime apenas o estado operacional reproduzido. Estrelas e efeito de busca continuam como sinais visuais do jogo. |
| Investigação, BOLO/APB, relógio, intimidação e furtividade | Ramos adicionais de `GetPlayerDisplay`; alguns coexistem com perseguição. | Preservados. |
| Velocidade, limite e combustível | `LowerRightDisplay.GetVehicleStatusDisplay`, `CurrentStreet`, `VehicleSpeed*`, `FuelTank.UIText`. Uso de combustível depende de opções e compatibilidade. | Painel automotivo na âncora V4. Limite cadastrado confirmado separadamente. O valor padrão da RC2 permanece na HUD original se ela o exibir. |
| Roubo, placa falsa, reconhecimento, placa procurada e identificação | Mesmo renderizador veicular. Os quatro avisos têm precedência mútua; nome pode ser exibido ao entrar no veículo. | Central. Os avisos usam catálogo RC2 e mantêm a prioridade original. |
| Motor desligado, marcha, infração, pisca e direção | Mesmo renderizador. Depende de motor, configuração de transmissão/bússola e estado do veículo. | Central. O aviso vermelho de velocidade também aparece no número do velocímetro. |
| Fome, sede e sono | `TopRightMenu.Display → HumanState`; simulação ativada e ações/mudanças recentes. | Indicadores superiores confirmados separadamente. |
| Dinheiro e saldo recente, arma, grupo, estrelas, saúde, regeneração, sangramento, fadiga e intoxicação | `TopRightMenu`, seu seletor de arma e estrelas personalizadas, e HUD nativa. | Dinheiro e demais superfícies originais preservados. Saúde e colete também estão no painel superior. |
| Crimes, menus, radial, prompts, scanner, marcadores, notificações e avisos | `DisplayCurrentCrimes`, `UI.Tick1/Tick2`, `ProcessActionWheel`, `DisplayButtonPrompts`, `MarkerManager`, renderizadores de menus e eventos. | Preservados, incluindo os fundos próprios do radial e dos prompts. |
| Barras e pop-ups | `TimerBarController.Setup/Update` e `UI.MenuUpdate`; as barras auxiliares de busca/fadiga/intoxicação estão desativadas no código RC2. `StreetNamePopUp` é um caminho separado. | Preservados. |

O desenho dos textos inferiores chama `NativeHelper.DisplayTextOnScreen`, que aplica sombra e opcionalmente contorno. Aquelas linhas não têm um retângulo associado na RC2. Quando a confirmação suprime uma linha, a chamada de desenho daquela linha, incluindo a sombra, deixa de ocorrer. O patch não cobre outras superfícies com retângulos.

## Protocolo da ponte V5

Canal local `Local\ElectroHud.Lsr.v4.<PID>`, 8192 bytes e mutex sem espera. O produtor publica a cada 250 ms. O leitor rejeita snapshots com mais de dois segundos e confirma apenas conteúdo efetivamente desenhado. A confirmação referencia o timestamp do snapshot e expira em 750 ms. O produtor também compara o texto original e, para dados veiculares, o handle. Um bloco que exceda o espaço da mensagem permanece na RC2. Ao receber F9, descarga ou perda de comunicação, a confirmação é zerada ou expira e os renderizadores voltam a mostrar os itens originais.

| Offset | Campo |
| --- | --- |
| 0, 4, 8 | Magic `0x45485234`, handle rastreado, timestamp |
| 16, 20, 24, 28 | Percentuais de fome, sede, sono e combustível |
| 32, 36, 40 | Flags do produtor, procurado, limite cadastrado em km/h |
| 44 | Comprimento do payload, em bytes |
| 48, 56 | Timestamp da confirmação e timestamp do snapshot confirmado |
| 64 a 8183 | Registros variáveis com ID `int32`, tamanho `int32` e texto UTF-8. IDs desconhecidos podem ser ignorados. |
| 8184, 8188 | Máscara dos elementos desenhados e handle do consumidor |

IDs de texto: 1 polícia, 2 gangue, 3 região simples, 64 zona completa, 128 rua, 512 perseguição e 1024 contexto do veículo. Para itens não textuais, a confirmação usa 4 combustível, 8/16/32 necessidades, 256 velocidade e 2048 limite. As flags do produtor usam 1 menu, 2 combustível ativado, 4 modelo que usa combustível, 8/16/32 necessidades ativadas, 64 busca, 128 limite confirmado, 256 veículo rodoviário rastreado, 512 rua sem cadastro e 1024 alerta vermelho de velocidade.
