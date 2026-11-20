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
| Reconhecimento | `CurrentVehicle.HasBeenSeenByPoliceDuringWanted`, com as mesmas prioridades da HUD original para veículo roubado e placa fictícia. `hud.vehicleKnownToPolice` tem fallback em inglês e tradução no catálogo central da RC2. |

São ocultados apenas os textos completos confirmados pelo ElectroHud. Textos muito longos mantêm a versão original se o painel os truncar. Permanecem na HUD original as mensagens de investigação, BOLO/APB, alertas de crimes, placa procurada, veículo roubado, placa fictícia, motor desligado e demais avisos que a central ainda não representa. Scanner, menus, notificações e estrelas continuam funcionando de forma independente.

## Limite de velocidade

`LowerRightDisplay.GetVehicleStatusDisplay()` formata a velocidade atual e `CurrentStreet.SpeedLimitMPH` entre parênteses, sem verificar a origem. Em `LocationData.UpdateStreets()`, a RC2 procura a rua pelo nome fornecido pelo jogo. Se não encontrar, cria `new Street(CurrentStreetName, 60f, "MPH")`. Alguns registros reais da RC2, como Great Ocean Hwy, também têm 60 mph. Só o número da captura é insuficiente para saber de qual caso se trata.

A V3 já exigia que a rua fosse a própria entrada do cadastro. Também exigia o mesmo handle de veículo, uma classe nativa rodoviária e um snapshot válido. Por isso a HUD original podia mostrar 60 mph mesmo com o limite oculto no ElectroHud. A V4 usa a classificação de automóvel/motocicleta da própria RC2 e registra o motivo da ausência com `Diagnostics`. Se a entrada estiver confirmada, o valor original em mph é multiplicado por **1,609344**. Um limite cadastrado de 60 mph resulta em `Limite: 97 km/h`. O valor padrão continua restrito à HUD original quando ela está configurada para mostrá-lo.

## Aplicar a ponte V4

Use uma **cópia limpa de `ba4115b`** da RC2 PT-BR. A V4 incorpora a V3 e deve ser aplicada diretamente à RC2 limpa. Na raiz da cópia:

```powershell
$patch = 'C:\src\ElectroHud\integration\LosSantosRED-ba4115b-v4.patch'
git apply --check $patch
if ($LASTEXITCODE -ne 0) { throw 'A cópia do LS RED deve estar limpa no commit ba4115b.' }
git apply $patch
```

Compile a cópia pelo procedimento da RC2 e instale a DLL e o catálogo atualizado no ambiente de teste. O ElectroHud V4 usa a ponte V4 ou a reflexão pública quando disponível.

## Protocolo da ponte V4

Canal `Local\ElectroHud.Lsr.v3.<PID>`, 1232 bytes, protegido por mutex sem espera. O produtor escreve a cada 250 ms. O consumidor rejeita snapshots com mais de dois segundos. A confirmação referencia um snapshot desenhado e vence em 750 ms. Texto e veículo são conferidos individualmente. F9, descarga, perda do canal e `SuppressReplacedLsrIndicators=false` devolvem a exibição à RC2 após zerar a confirmação ou expirar o prazo.

| Offset | Campo |
| --- | --- |
| 0, 4, 8 | Magic `0x45485233`, handle rastreado, timestamp do snapshot |
| 16, 20, 24, 28 | Percentuais de fome, sede, sono e combustível |
| 32, 36, 40 | Flags do produtor, nível de procurado, limite confirmado em km/h |
| 64, 256, 448, 640, 840, 1032 | Região, rua, polícia, gangue, perseguição e reconhecimento. UTF-8, 192 bytes por campo. |
| 48, 832, 836, 1224 | Timestamp da confirmação, máscara do consumidor, handle e timestamp do snapshot confirmado |

Flags do produtor: 1 menu, 2 combustível ativado, 4 veículo que usa combustível, 8/16/32 fome/sede/sono ativados, 64 modo de busca, 128 limite confirmado, 256 automóvel/motocicleta rastreado, 512 rua sem cadastro. Máscara do consumidor: 4 combustível, 8/16/32 fome/sede/sono, 64 região com dados territoriais, 128 rua, 256 velocidade, 512 estado de perseguição, 1024 reconhecimento, 2048 limite.
